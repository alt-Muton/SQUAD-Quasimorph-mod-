using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// Squad allies use player-like movement modes (Slow/Normal/Run) for AP,
    /// and mirror the leader's mode while following.
    /// </summary>
    internal static class AllyMovementService
    {
        public static bool IsSquadAlly(Monster monster)
        {
            if (monster?.CreatureData == null || !Plugin.Config.Enabled)
                return false;
            // Escort VIP / quest NPCs use FollowTarget too — never treat them as squad.
            if (monster.CreatureData.IsQuestCreature)
                return false;
            return SquadRaidState.UniqueIdToProfileId.ContainsKey(monster.CreatureData.UniqueId);
        }

        /// <summary>
        /// True for escort VIP and other quest-tagged allies that must keep vanilla FollowTarget.
        /// </summary>
        public static bool IsProtectedQuestAlly(CreatureData data)
        {
            return data != null && data.IsQuestCreature;
        }

        public static bool IsFollowingPlayer(Monster monster)
        {
            if (monster?.Behaviour == null)
                return false;
            if (!monster.Behaviour.TryGetState<FollowTarget>(out var follow) || follow == null)
                return false;
            return follow.HasFollowTarget;
        }

        public static int GetModeActionPoints(CreatureData data, CreatureMovementState mode)
        {
            if (data == null)
                return 2;
            var ap = MoveSystem.GetMaxActionPoints(mode, data);
            return ap < 1 ? 1 : ap;
        }

        public static int GetContenderSlotCount(CreatureData data)
        {
            if (data == null)
                return 2;
            var slots = MoveSystem.GetMaxPossibleActionPoints(data);
            return slots < 1 ? 2 : slots;
        }

        public static int GetModeActionPointsWithAllyBonuses(Monster monster, CreatureMovementState mode)
        {
            var ap = GetModeActionPoints(monster.CreatureData, mode);
            try
            {
                var creatures = UI.Resolve<Creatures>();
                var player = creatures?.Player;
                if (player != null && monster.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance)
                    ap += PerkSystem.GetPerkParameterSumInt(player.CreatureData, "IAllyAddedAP");
            }
            catch
            {
                // UI/state may be unavailable in some hooks
            }

            ap += Mathf.RoundToInt(monster.CreatureData.EffectsController.SumEffectsValue((WoundEffectAddedAP w) => w.Value));
            ap += Mathf.RoundToInt(monster.CreatureData.EffectsController.SumEffectsValue((AdrenalinInjectionBuff b) => b.AddedAp));
            ap += Mathf.RoundToInt(monster.CreatureData.EffectsController.SumEffectsValue((SurgeBuff b) => b.AddedAp));
            return ap < 1 ? 1 : ap;
        }

        public static void ApplyBaseActionPoints(CreatureData data)
        {
            if (data == null)
                return;
            data.BaseActionPoints = GetModeActionPoints(data, CreatureMovementState.Normal);
        }

        public static void SyncMovementFromPlayer(Monster monster, Player player)
        {
            if (monster == null || player == null)
                return;

            var desired = player.ForcedNextState != CreatureMovementState.None
                ? player.ForcedNextState
                : player.MovementState;
            if (desired == CreatureMovementState.None)
                desired = CreatureMovementState.Normal;

            ApplyMovementState(monster, desired);
        }

        public static void ApplyNormalMovement(Monster monster)
        {
            ApplyMovementState(monster, CreatureMovementState.Normal);
        }

        public static void ApplyFollowOrIdleMovement(Monster monster, Player player)
        {
            if (monster == null)
                return;
            if (player != null && IsFollowingPlayer(monster))
                SyncMovementFromPlayer(monster, player);
            else
                ApplyNormalMovement(monster);
        }

        private static void ApplyMovementState(Monster monster, CreatureMovementState desired)
        {
            if (monster.MovementState == desired)
                return;

            monster.MovementState = desired;
            monster.RefreshAnimationsSpeed();
            CreatureSystem.RefreshStarvationDelta(monster.CreatureData);
            Plugin.Logger.Log("Ally " + monster.CreatureData.UniqueId
                              + " movement -> " + desired
                              + " (AP=" + GetModeActionPointsWithAllyBonuses(monster, desired) + ")");
        }

        public static void EnsureTurnSlots(Monster monster, TurnController turnController)
        {
            if (monster == null || turnController == null)
                return;

            var slots = GetContenderSlotCount(monster.CreatureData);
            monster.ActionPoints = slots;
            turnController.RemoveContender(monster);
            for (var i = 0; i < slots; i++)
                turnController.AddContender(monster);
        }

        public static void PrepareMercCreatureData(Mercenary merc, string aiMobClassId)
        {
            if (merc?.CreatureData == null)
                return;
            // Always bind the squad AI MobClass (prefers CanUseItems for HealSelf/FixateSelf).
            if (!string.IsNullOrEmpty(aiMobClassId))
                merc.CreatureData.MobClassId = aiMobClassId;
            ApplyBaseActionPoints(merc.CreatureData);
            // Raid prep / first bind: start full. Floor reaffirm must not refill (see DisableHunger).
            DisableHunger(merc.CreatureData, refillSatiety: true);
        }

        /// <summary>
        /// Squad allies use full merc starvation, but AI never eats. Disable hunger for the raid
        /// (same flag vanilla uses for normal monsters). Clear via <see cref="RestoreHunger"/> after raid.
        /// When Hungry_NPC is present it clears IgnoreStarvation in a postfix — do not refill on
        /// stage reaffirm / persistence rebind or satiety jumps to full every floor.
        /// </summary>
        /// <param name="refillSatiety">
        /// True only for raid start / merc prepare / resurrect prep. False preserves current level.
        /// </param>
        public static void DisableHunger(CreatureData data, bool refillSatiety = false)
        {
            if (data == null)
                return;
            data.IgnoreStarvation = true;
            // Restore() is additive — set absolute full for raid-start / resurrect.
            if (refillSatiety && data.Starvation != null && data.Starvation.MaxLevel > 0)
                data.Starvation.SetLevelForDebugPurposes(data.Starvation.MaxLevel);
        }

        public static void RestoreHunger(CreatureData data)
        {
            if (data == null)
                return;
            data.IgnoreStarvation = false;
        }

        public static void RestoreHungerForProfileIds(Mercenaries mercenaries, System.Collections.Generic.IEnumerable<string> profileIds)
        {
            if (mercenaries == null || profileIds == null)
                return;
            foreach (var profileId in profileIds)
            {
                if (string.IsNullOrEmpty(profileId))
                    continue;
                var merc = mercenaries.Get(profileId);
                if (merc?.CreatureData != null)
                    RestoreHunger(merc.CreatureData);
            }
        }

        public static void SyncAllSquadAllyMovement(State state)
        {
            if (!Plugin.Config.Enabled || state == null || SquadRaidState.UniqueIdToProfileId.Count == 0)
                return;

            var creatures = state.Get<Creatures>();
            var player = creatures?.Player;

            foreach (var kv in SquadRaidState.UniqueIdToProfileId)
            {
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (monster == null || monster.CreatureData.Health.Dead)
                    continue;
                ApplyFollowOrIdleMovement(monster, player);
            }
        }

        public static IEnumerable<Monster> EnumerateSquadAllies(Creatures creatures)
        {
            if (creatures == null)
                yield break;
            foreach (var kv in SquadRaidState.UniqueIdToProfileId)
            {
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (monster == null || monster.CreatureData.Health.Dead)
                    continue;
                if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                    continue;
                if (creatures.Player?.CreatureData != null
                    && ReferenceEquals(monster.CreatureData, creatures.Player.CreatureData))
                    continue;
                yield return monster;
            }
        }
    }
}
