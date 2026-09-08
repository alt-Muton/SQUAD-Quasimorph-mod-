using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MGSC;
using Newtonsoft.Json;

namespace Squad_Leader
{
    public sealed class SquadRaidSnapshot
    {
        public int Version = 1;
        public string RaidKey = "";
        public string LeaderProfileId;
        public List<string> AllyProfileIds = new List<string>();
        public Dictionary<int, string> UniqueIdToProfileId = new Dictionary<int, string>();
    }

    /// <summary>
    /// Persists SquadRaidState across mid-raid save/load (vanilla has no mod BeforeSave hook,
    /// so we write a side JSON keyed by slot+raid and restore on AfterDungeonLoaded).
    /// </summary>
    internal static class SquadRaidPersistence
    {
        private const int CurrentVersion = 1;
        private static string _activeRaidKey;
        private static State _dungeonState;

        /// <summary>
        /// Set when FinishGame is about to LoadLastSave (RestartStage). DungeonFinished must not
        /// overwrite squad JSON that still matches the autosave being reloaded.
        /// </summary>
        public static bool SuppressNextDungeonFinishedSave { get; set; }

        public static void SetDungeonState(State state)
        {
            _dungeonState = state;
        }

        public static void SaveCurrent(State state = null)
        {
            state = state ?? _dungeonState;
            if (!Plugin.Config.Enabled || state == null)
                return;
            if (!SquadRaidState.Active && SquadRaidState.AllyProfileIds.Count == 0)
                return;

            var raidKey = BuildRaidKey(state);
            if (string.IsNullOrEmpty(raidKey))
                return;

            var snapshot = new SquadRaidSnapshot
            {
                Version = CurrentVersion,
                RaidKey = raidKey,
                LeaderProfileId = SquadRaidState.LeaderProfileId,
                AllyProfileIds = SquadRaidState.AllyProfileIds.ToList(),
                UniqueIdToProfileId = new Dictionary<int, string>(SquadRaidState.UniqueIdToProfileId)
            };

            try
            {
                var path = GetPersistencePath(raidKey);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
                _activeRaidKey = raidKey;
                Plugin.Logger.Log("Squad raid saved. Key=" + raidKey
                                  + " Allies=" + snapshot.AllyProfileIds.Count
                                  + " Map=" + snapshot.UniqueIdToProfileId.Count);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Failed to save squad raid state.");
                Plugin.Logger.LogException(ex);
            }
        }

        public static bool TryRestore(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return false;

            var raidKey = BuildRaidKey(state);
            if (string.IsNullOrEmpty(raidKey))
                return false;

            var path = GetPersistencePath(raidKey);
            if (!File.Exists(path))
                return false;

            SquadRaidSnapshot snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<SquadRaidSnapshot>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Failed to read squad raid state.");
                Plugin.Logger.LogException(ex);
                return false;
            }

            if (snapshot == null || snapshot.Version != CurrentVersion || snapshot.RaidKey != raidKey)
                return false;

            SquadRaidState.RestoreFromSnapshot(
                snapshot.LeaderProfileId,
                snapshot.AllyProfileIds,
                snapshot.UniqueIdToProfileId);

            RemapUniqueIdsFromCreatures(state);
            RebindAllyCreatureData(state);

            _activeRaidKey = raidKey;
            _dungeonState = state;
            Plugin.Logger.Log("Squad raid restored. " + SquadRaidState.StatusLine());
            return true;
        }

        public static void DeleteCurrent(State state = null)
        {
            state = state ?? _dungeonState;
            var raidKey = _activeRaidKey ?? BuildRaidKey(state);
            _activeRaidKey = null;
            if (string.IsNullOrEmpty(raidKey))
                return;

            try
            {
                var path = GetPersistencePath(raidKey);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }

        /// <summary>
        /// When squad JSON is missing after ExitToMainMenu/load, rebuild ally list from living
        /// PlayerAlliance monsters whose LocalizationId matches a roster merc (not InRaid).
        /// Skips VIP/NPC escorts that are not Magnum mercenaries.
        /// </summary>
        public static bool TryRecoverFromDungeonCreatures(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return false;

            var creatures = state.Get<Creatures>();
            var mercenaries = state.Get<Mercenaries>();
            if (creatures?.Monsters == null || mercenaries == null)
                return false;

            var inRaidId = mercenaries.MercenaryInRaid?.ProfileId
                           ?? creatures.Player?.Mercenary?.ProfileId;
            var allies = new List<string>();
            var map = new Dictionary<int, string>();

            foreach (var monster in creatures.Monsters)
            {
                if (monster?.CreatureData == null || monster.CreatureData.Health.Dead)
                    continue;
                if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                    continue;
                if (monster.CreatureData.CreatureAlliance != CreatureAlliance.PlayerAlliance)
                    continue;

                var locId = monster.CreatureData.LocalizationId;
                if (string.IsNullOrEmpty(locId) || locId == inRaidId)
                    continue;

                var merc = mercenaries.Get(locId);
                if (merc == null || merc.State == MercenaryState.InRaid)
                    continue;
                if (ReferenceEquals(merc.CreatureData, creatures.Player?.CreatureData))
                    continue;
                if (ReferenceEquals(merc, mercenaries.MercenaryInRaid))
                    continue;

                if (!allies.Contains(locId))
                    allies.Add(locId);
                map[monster.CreatureData.UniqueId] = locId;
            }

            if (allies.Count == 0)
                return false;

            SquadRaidState.RestoreFromSnapshot(inRaidId, allies, map);
            Plugin.Logger.Log("Recovered squad from dungeon creatures. " + SquadRaidState.StatusLine());
            return true;
        }

        /// <summary>
        /// After load, dungeon Monster.CreatureData and Mercenary.CreatureData are separate copies.
        /// Rebind so perk XP / death / inventory stay on the live creature.
        /// </summary>
        public static void RebindAllyCreatureData(State state)
        {
            if (state == null || SquadRaidState.UniqueIdToProfileId.Count == 0)
                return;

            var creatures = state.Get<Creatures>();
            var mercenaries = state.Get<Mercenaries>();
            if (creatures == null || mercenaries == null)
                return;

            foreach (var kv in SquadRaidState.UniqueIdToProfileId.ToList())
            {
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                var merc = mercenaries.Get(kv.Value);
                if (monster?.CreatureData == null || merc == null)
                    continue;

                // Never attach the controlled operative's data to a Monster (ghost).
                if (merc.State == MercenaryState.InRaid
                    || ReferenceEquals(merc, mercenaries.MercenaryInRaid)
                    || ReferenceEquals(merc.CreatureData, creatures.Player?.CreatureData)
                    || AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                {
                    Plugin.Logger.LogWarning("Skip rebind for InRaid/shared/quest merc " + kv.Value);
                    SquadRaidState.UnregisterAllyCreature(kv.Key);
                    continue;
                }

                var locId = monster.CreatureData.LocalizationId;
                if (!string.IsNullOrEmpty(locId) && locId != kv.Value)
                {
                    Plugin.Logger.LogWarning("Skip rebind: LocalizationId=" + locId
                                            + " != profile=" + kv.Value);
                    SquadRaidState.UnregisterAllyCreature(kv.Key);
                    continue;
                }

                if (!ReferenceEquals(merc.CreatureData, monster.CreatureData))
                {
                    // Prefer dungeon copy (live combat + inventory); keep progression flags.
                    merc.EnabledProgression = true;
                    merc.CreatureData = monster.CreatureData;
                    Plugin.Logger.Log("Rebound ally CreatureData " + kv.Value + " uid=" + kv.Key);
                }

                AllyPerkProgressService.EnsureProgressionEnabled(merc);
                AllyMovementService.DisableHunger(monster.CreatureData);
                AllyBackpackLayout.EnsureVipCompatibleLayout(monster.CreatureData.Inventory);
            }
        }

        /// <summary>
        /// If saved UniqueIds miss loaded creatures, rebuild map via LocalizationId == ProfileId.
        /// </summary>
        public static void RemapUniqueIdsFromCreatures(State state)
        {
            if (state == null || SquadRaidState.AllyProfileIds.Count == 0)
                return;

            var creatures = state.Get<Creatures>();
            if (creatures == null)
                return;

            var needed = new HashSet<string>(SquadRaidState.AllyProfileIds);
            var found = new Dictionary<int, string>();

            foreach (var kv in SquadRaidState.UniqueIdToProfileId)
            {
                if (!needed.Contains(kv.Value))
                    continue;
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (monster != null && !monster.CreatureData.Health.Dead
                    && !AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                {
                    found[kv.Key] = kv.Value;
                    needed.Remove(kv.Value);
                }
            }

            if (needed.Count > 0)
            {
                var playerUid = creatures.Player?.CreatureData?.UniqueId ?? 0;
                foreach (var monster in creatures.Monsters)
                {
                    if (monster?.CreatureData == null || monster.CreatureData.Health.Dead)
                        continue;
                    if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                        continue;
                    if (monster.CreatureData.CreatureAlliance != CreatureAlliance.PlayerAlliance)
                        continue;
                    if (playerUid != 0 && monster.CreatureData.UniqueId == playerUid)
                        continue;
                    var locId = monster.CreatureData.LocalizationId;
                    if (string.IsNullOrEmpty(locId) || !needed.Contains(locId))
                        continue;
                    // One profile → one living monster; ignore duplicate LocId matches.
                    if (found.ContainsValue(locId))
                        continue;
                    found[monster.CreatureData.UniqueId] = locId;
                    needed.Remove(locId);
                    Plugin.Logger.Log("Remapped ally " + locId + " -> uid=" + monster.CreatureData.UniqueId);
                }
            }

            SquadRaidState.ReplaceUniqueMap(found);
        }

        public static string BuildRaidKey(State state)
        {
            if (state == null)
                return null;

            var raid = state.Get<RaidMetadata>();
            if (raid == null)
                return null;

            var save = state.Get<SavedGameMetadata>();
            var slot = save?.Slot ?? -1;
            var missions = state.Get<Missions>();
            var mission = missions?.Get(raid);
            var missionTicks = mission != null ? mission.CreationTime.Ticks : 0L;

            var sb = new StringBuilder(128);
            sb.Append("slot=").Append(slot)
                .Append("|type=").Append(raid.RaidType)
                .Append("|station=").Append(raid.StationId ?? "")
                .Append("|bram=").Append(raid.BramfaturaId ?? "")
                .Append("|group=").Append(raid.FreeGroupIndex)
                .Append("|stage=").Append(raid.CustomStage ?? "")
                .Append("|rev=").Append(raid.IsReversedMission ? 1 : 0)
                .Append("|mission=").Append(missionTicks);
            return sb.ToString();
        }

        private static string GetPersistencePath(string raidKey)
        {
            var folder = Plugin.ConfigDirectories?.ModPersistenceFolder ?? ".";
            var safe = new string(raidKey.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) || ch == '|' ? '_' : ch).ToArray());
            if (safe.Length > 120)
                safe = safe.Substring(0, 120);
            return Path.Combine(folder, "squad_raid_" + safe + ".json");
        }
    }
}
