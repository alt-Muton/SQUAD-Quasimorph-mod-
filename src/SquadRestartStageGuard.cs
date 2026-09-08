using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// RestartStage / ReloadDungeonAfterDeath loads the last autosave but does not SaveGame on death.
    /// If we overwrite squad_raid_*.json after handoff (or on DungeonFinished), the next floor reload
    /// restores a post-handoff roster onto a pre-handoff dungeon — SpawnAllies then creates a Monster
    /// on the InRaid merc's CreatureData (ghost, broken pact / weapon swap).
    ///
    /// Rule: persistence must only advance with SaveGame (and safe dungeon boot). Never on handoff/death.
    /// </summary>
    internal static class SquadRestartStageGuard
    {
        /// <summary>
        /// Set when FinishGame schedules LoadLastSave. While set, squad JSON must not advance on
        /// DungeonStarted and roster must be rebuilt from the autosave mercenary list.
        /// </summary>
        public static bool PendingRestartStageReload { get; set; }

        public static bool IsFloorReloadDeath(State state)
        {
            if (state == null)
                return false;
            try
            {
                var difficulty = state.Get<Difficulty>();
                if (difficulty?.Preset != null && difficulty.Preset.DeathPenalty == DeathPenalty.RestartStage)
                {
                    var player = state.Get<Creatures>()?.Player;
                    if (player?.CreatureData != null && player.CreatureData.Health.Dead)
                        return true;
                }

                var scenarios = state.Get<Scenarios>();
                var dungeonScenario = scenarios?.First<BaseDungeonScenario>();
                if (dungeonScenario != null && dungeonScenario.ReloadDungeonAfterDeath)
                {
                    var player = state.Get<Creatures>()?.Player;
                    if (player?.CreatureData != null && player.CreatureData.Health.Dead)
                        return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        /// <summary>
        /// After any dungeon load/start: leader = MercenaryInRaid; never treat InRaid as an ally spawn target.
        /// </summary>
        public static void AlignSquadWithLoadedMercenaries(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return;

            LeaderHandoffService.ClearPendingHandoff();
            LeaderHandoffService.ClearPostHandoffRewire();

            Mercenaries mercenaries;
            try
            {
                mercenaries = state.Get<Mercenaries>();
            }
            catch
            {
                return;
            }

            var inRaid = mercenaries?.MercenaryInRaid;
            if (inRaid == null || string.IsNullOrEmpty(inRaid.ProfileId))
                return;

            var leaderId = inRaid.ProfileId;
            if (SquadRaidState.LeaderProfileId != leaderId)
            {
                Plugin.Logger.Log("RestartStage guard: align LeaderProfileId "
                                  + (SquadRaidState.LeaderProfileId ?? "null") + " -> " + leaderId);
                SquadRaidState.LeaderProfileId = leaderId;
            }

            // Never spawn / track the controlled merc as an ally monster.
            if (SquadRaidState.AllyProfileIds.Remove(leaderId))
                Plugin.Logger.Log("RestartStage guard: removed InRaid from AllyProfileIds " + leaderId);
            SquadRaidState.UnregisterAllyByProfileId(leaderId);

            // Drop missing profiles from a desynced snapshot.
            for (var i = SquadRaidState.AllyProfileIds.Count - 1; i >= 0; i--)
            {
                var id = SquadRaidState.AllyProfileIds[i];
                if (mercenaries.Get(id) == null)
                {
                    SquadRaidState.AllyProfileIds.RemoveAt(i);
                    SquadRaidState.UnregisterAllyByProfileId(id);
                    Plugin.Logger.Log("RestartStage guard: dropped missing ally profile " + id);
                }
            }

            // Clear stale body-swap flag that breaks weapon UI / pact after bad loads.
            try
            {
                if (mercenaries.ChangedMercenary != null)
                {
                    Plugin.Logger.Log("RestartStage guard: clearing ChangedMercenary="
                                      + mercenaries.ChangedMercenary.ProfileId);
                    mercenaries.ChangedMercenary = null;
                }
            }
            catch
            {
                // ignore
            }

            if (PendingRestartStageReload)
                ReconcileSquadAfterRestartLoad(state);

            PurgeLeaderGhostMonsters(state);
            SquadMercenaryPactGuard.SanitizeMercenaryPacts(state);
        }

        /// <summary>
        /// After RestartStage the side JSON may describe a post-handoff roster while the autosave
        /// rewound mercenary State / InRaid. Rebuild ally ids from living non-InRaid mercs only.
        /// </summary>
        public static void ReconcileSquadAfterRestartLoad(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return;

            Mercenaries mercenaries;
            Creatures creatures;
            try
            {
                mercenaries = state.Get<Mercenaries>();
                creatures = state.Get<Creatures>();
            }
            catch
            {
                return;
            }

            var inRaid = mercenaries?.MercenaryInRaid;
            if (inRaid == null || string.IsNullOrEmpty(inRaid.ProfileId))
                return;

            var leaderId = inRaid.ProfileId;
            var player = creatures?.Player;
            var beforeAllies = SquadRaidState.AllyProfileIds.Count;
            var beforeMap = SquadRaidState.UniqueIdToProfileId.Count;

            SquadRaidState.LeaderProfileId = leaderId;
            SquadRaidState.UniqueIdToProfileId.Clear();

            var reconciled = new List<string>();
            foreach (var profileId in SquadRaidState.AllyProfileIds)
            {
                if (string.IsNullOrEmpty(profileId) || profileId == leaderId)
                    continue;
                if (reconciled.Contains(profileId))
                    continue;

                var merc = mercenaries.Get(profileId);
                if (merc == null)
                    continue;
                if (merc.State == MercenaryState.InRaid || ReferenceEquals(merc, inRaid))
                    continue;
                if (merc.CreatureData?.Health == null || merc.CreatureData.Health.Dead)
                    continue;
                if (player?.CreatureData != null
                    && ReferenceEquals(merc.CreatureData, player.CreatureData))
                    continue;

                reconciled.Add(profileId);
            }

            SquadRaidState.AllyProfileIds.Clear();
            SquadRaidState.AllyProfileIds.AddRange(reconciled);
            SquadRaidState.EnsureActive();

            Plugin.Logger.Log("RestartStage reconcile: leader=" + leaderId
                              + " allies " + beforeAllies + "->" + reconciled.Count
                              + " cleared uid map " + beforeMap);
        }

        /// <summary>
        /// Despawn PlayerAlliance bodies that duplicate the controlled operative (ghost at elevator).
        /// </summary>
        public static void PurgeLeaderGhostMonsters(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return;

            Mercenaries mercenaries;
            Creatures creatures;
            TurnController turnController;
            try
            {
                mercenaries = state.Get<Mercenaries>();
                creatures = state.Get<Creatures>();
                turnController = state.Get<TurnController>();
            }
            catch
            {
                return;
            }

            if (creatures?.Monsters == null || mercenaries == null || turnController == null)
                return;

            var player = creatures.Player;
            var leaderId = mercenaries.MercenaryInRaid?.ProfileId ?? player?.Mercenary?.ProfileId;
            if (string.IsNullOrEmpty(leaderId))
                return;

            foreach (var creature in creatures.Monsters.ToList())
            {
                var monster = creature as Monster;
                if (monster == null)
                    continue;
                if (ReferenceEquals(creature, player))
                    continue;
                if (monster?.CreatureData == null || monster.CreatureData.Health.Dead)
                    continue;
                if (monster.CreatureData.CreatureAlliance != CreatureAlliance.PlayerAlliance)
                    continue;
                if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                    continue;

                var locId = monster.CreatureData.LocalizationId;
                var isLeaderGhost = locId == leaderId
                                    || (!string.IsNullOrEmpty(player?.Mercenary?.ProfileId)
                                        && locId == player.Mercenary.ProfileId);
                var sharesPlayerBody = player?.CreatureData != null
                                       && ReferenceEquals(monster.CreatureData, player.CreatureData);

                if (!isLeaderGhost && !sharesPlayerBody)
                    continue;

                if (sharesPlayerBody)
                {
                    Plugin.Logger.LogWarning("RestartStage guard: leader ghost shares player CreatureData — skip despawn.");
                    continue;
                }

                var ghostUid = monster.CreatureData.UniqueId;
                Plugin.Logger.LogWarning("RestartStage guard: purge leader ghost uid="
                                          + ghostUid
                                          + " loc=" + (locId ?? "?"));

                try
                {
                    monster.IsTransferable = false;
                    monster.Behaviour?.StopFollowing();
                    if (!ReferenceEquals(monster.CreatureData, player?.CreatureData))
                    {
                        var merc = mercenaries.Get(locId);
                        if (merc != null && ReferenceEquals(merc.CreatureData, monster.CreatureData))
                        {
                            // Monster.OnDestroy clears shared CreatureData — detach dummy first.
                            monster.CreatureData = LeaderHandoffService.CreateDespawnDummyCreatureData();
                        }
                    }

                    CreatureSystem.DespawnCreature(turnController, creatures, monster);
                    if (ghostUid != 0)
                        SquadRaidState.UnregisterAllyCreature(ghostUid);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError("RestartStage guard: failed to purge ghost.");
                    Plugin.Logger.LogException(ex);
                }
            }
        }
    }

    /// <summary>
    /// When FinishGame turns a death into LoadLastSave (restart floor), do not rewrite squad JSON.
    /// </summary>
    [HarmonyPatch(typeof(DungeonGameMode), "FinishGame")]
    internal static class FinishGameRestartStagePersistPatch
    {
        private static void Prefix(DungeonGameMode __instance, DungeonFinishedData dungeonFinishedData)
        {
            if (!Plugin.Config.Enabled || dungeonFinishedData == null)
                return;

            try
            {
                var state = Traverse.Create(__instance).Field("_state").GetValue<State>()
                            ?? Plugin.GetCachedDungeonState();
                if (state == null)
                    return;

                // Vanilla rewrites Reason to LoadLastSave inside FinishGame for RestartStage;
                // also catch already-set LoadLastSave from scenario reload.
                var difficulty = state.Get<Difficulty>();
                var scenarios = state.Get<Scenarios>();
                var dungeonScenario = scenarios?.First<BaseDungeonScenario>();
                var isRestart = dungeonFinishedData.Reason == GameFinishedReason.LoadLastSave
                                || (dungeonFinishedData.Reason == GameFinishedReason.PlayerDead
                                    && difficulty?.Preset != null
                                    && difficulty.Preset.DeathPenalty == DeathPenalty.RestartStage
                                    && dungeonFinishedData.DeathReason != HealthInfo.DeathReason.AbortMission)
                                || (dungeonFinishedData.Reason == GameFinishedReason.PlayerDead
                                    && dungeonScenario != null
                                    && dungeonScenario.ReloadDungeonAfterDeath);

                if (!isRestart)
                    return;

                // Mark so DungeonFinished hook skips SaveCurrent (FinishGame may still mutate Reason).
                SquadRaidPersistence.SuppressNextDungeonFinishedSave = true;
                SquadRestartStageGuard.PendingRestartStageReload = true;
                LeaderHandoffService.ClearPendingHandoff();
                LeaderHandoffService.ClearPostHandoffRewire();
                Plugin.Logger.Log("RestartStage/LoadLastSave: suppress squad persistence overwrite. "
                                  + SquadRaidState.StatusLine());
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
