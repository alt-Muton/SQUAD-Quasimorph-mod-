using System.Collections.Generic;

namespace Squad_Leader
{
    /// <summary>Active raid squad (session). Leader is vanilla MercenaryInRaid; allies listed here are NOT InRaid.</summary>
    public static class SquadRaidState
    {
        public static string LeaderProfileId { get; set; }
        public static readonly List<string> AllyProfileIds = new List<string>();
        public static readonly Dictionary<int, string> UniqueIdToProfileId = new Dictionary<int, string>();
        public static bool Active { get; private set; }

        /// <summary>Re-assert Active after mid-raid recovery (e.g. defib when state was cleared).</summary>
        public static void EnsureActive()
        {
            Active = AllyProfileIds.Count > 0 || !string.IsNullOrEmpty(LeaderProfileId);
        }

        public static void BeginRaid(string leaderProfileId, IEnumerable<string> allyProfileIds)
        {
            Clear(deletePersistence: false);
            LeaderProfileId = leaderProfileId;
            if (allyProfileIds != null)
            {
                foreach (var id in allyProfileIds)
                {
                    if (!string.IsNullOrEmpty(id) && id != leaderProfileId)
                        AllyProfileIds.Add(id);
                }
            }
            Active = AllyProfileIds.Count > 0 || !string.IsNullOrEmpty(leaderProfileId);
            Plugin.Logger.Log("Raid squad begun. Leader=" + leaderProfileId
                              + " Allies=[" + string.Join(",", AllyProfileIds) + "]");
            // Persistence file is written after spawn / on SaveGame (RaidMetadata is ready then).
        }

        public static void RegisterAllyCreature(int uniqueId, string profileId)
        {
            UniqueIdToProfileId[uniqueId] = profileId;
        }

        /// <summary>
        /// Drop live-creature mapping only. Profile stays in AllyProfileIds for raid-end death processing.
        /// </summary>
        public static void UnregisterAllyCreature(int uniqueId)
        {
            if (!UniqueIdToProfileId.TryGetValue(uniqueId, out var profileId))
                return;
            UniqueIdToProfileId.Remove(uniqueId);
            Plugin.Logger.Log("Unregistered ally uid=" + uniqueId + " profile=" + profileId);
        }

        public static void UnregisterAllyByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return;
            var removed = false;
            var keys = new List<int>();
            foreach (var kv in UniqueIdToProfileId)
            {
                if (kv.Value == profileId)
                    keys.Add(kv.Key);
            }
            foreach (var key in keys)
            {
                UniqueIdToProfileId.Remove(key);
                removed = true;
            }
            if (removed)
                Plugin.Logger.Log("Unregistered ally profile map entries for " + profileId);
        }

        public static bool TryGetAllyProfileId(int uniqueId, out string profileId)
        {
            return UniqueIdToProfileId.TryGetValue(uniqueId, out profileId);
        }

        /// <summary>
        /// After leader death handoff: new leader leaves the ally list; dead leader joins it for raid-end processing.
        /// </summary>
        public static void OnLeaderHandoff(string deadLeaderProfileId, string newLeaderProfileId)
        {
            if (!string.IsNullOrEmpty(newLeaderProfileId))
            {
                UnregisterAllyByProfileId(newLeaderProfileId);
                AllyProfileIds.Remove(newLeaderProfileId);
            }

            if (!string.IsNullOrEmpty(deadLeaderProfileId) && deadLeaderProfileId != newLeaderProfileId
                && !AllyProfileIds.Contains(deadLeaderProfileId))
            {
                AllyProfileIds.Add(deadLeaderProfileId);
            }

            LeaderProfileId = newLeaderProfileId;
            Active = AllyProfileIds.Count > 0 || !string.IsNullOrEmpty(LeaderProfileId);
            Plugin.Logger.Log("Leader handoff state. " + StatusLine());
        }

        public static void RestoreFromSnapshot(
            string leaderProfileId,
            IEnumerable<string> allyProfileIds,
            Dictionary<int, string> uniqueIdToProfileId)
        {
            LeaderProfileId = leaderProfileId;
            AllyProfileIds.Clear();
            UniqueIdToProfileId.Clear();
            if (allyProfileIds != null)
            {
                foreach (var id in allyProfileIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        AllyProfileIds.Add(id);
                }
            }
            if (uniqueIdToProfileId != null)
            {
                foreach (var kv in uniqueIdToProfileId)
                    UniqueIdToProfileId[kv.Key] = kv.Value;
            }
            Active = AllyProfileIds.Count > 0 || !string.IsNullOrEmpty(leaderProfileId);
        }

        public static void ReplaceUniqueMap(Dictionary<int, string> map)
        {
            UniqueIdToProfileId.Clear();
            if (map == null)
                return;
            foreach (var kv in map)
                UniqueIdToProfileId[kv.Key] = kv.Value;
        }

        public static void Clear(bool deletePersistence = true)
        {
            if (deletePersistence)
                SquadRaidPersistence.DeleteCurrent();
            LeaderProfileId = null;
            AllyProfileIds.Clear();
            UniqueIdToProfileId.Clear();
            Active = false;
            LeaderHandoffService.ClearPendingHandoff();
        }

        public static string StatusLine()
        {
            if (!Active && AllyProfileIds.Count == 0 && string.IsNullOrEmpty(LeaderProfileId))
                return "No active raid squad.";
            return "Leader=" + (LeaderProfileId ?? "?")
                   + " Allies=[" + string.Join(",", AllyProfileIds) + "]"
                   + " UniqueMap=" + UniqueIdToProfileId.Count;
        }
    }
}
