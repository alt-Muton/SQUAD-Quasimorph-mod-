using System.Collections.Generic;
using System.Linq;
using MGSC;

namespace Squad_Leader
{
    /// <summary>Prep-screen squad: index 0 = leader (vanilla PrepareRaidScreen), rest = allies.</summary>
    public static class SquadSelectionState
    {
        /// <summary>-1 = selecting for leader (vanilla), 0+ = ally slot index in AllyProfileIds.</summary>
        public static int ActiveSelectSlot { get; set; } = -1;

        public static readonly List<string> AllyProfileIds = new List<string>();

        public static void Clear()
        {
            AllyProfileIds.Clear();
            ActiveSelectSlot = -1;
        }

        public static IEnumerable<string> AllProfileIds(string leaderProfileId)
        {
            if (!string.IsNullOrEmpty(leaderProfileId))
                yield return leaderProfileId;
            foreach (var id in AllyProfileIds)
            {
                if (!string.IsNullOrEmpty(id))
                    yield return id;
            }
        }

        public static bool IsOccupiedByOther(string profileId, int allySlotIndex)
        {
            if (string.IsNullOrEmpty(profileId))
                return false;

            for (var i = 0; i < AllyProfileIds.Count; i++)
            {
                if (i == allySlotIndex)
                    continue;
                if (AllyProfileIds[i] == profileId)
                    return true;
            }
            return false;
        }

        public static bool IsOccupied(string profileId, string leaderProfileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return false;
            if (profileId == leaderProfileId)
                return true;
            return AllyProfileIds.Any(id => id == profileId);
        }

        public static void SetAlly(int index, string profileId)
        {
            while (AllyProfileIds.Count <= index)
                AllyProfileIds.Add(null);
            AllyProfileIds[index] = profileId;
        }

        public static void RemoveAllyAt(int index)
        {
            if (index < 0 || index >= AllyProfileIds.Count)
                return;
            AllyProfileIds.RemoveAt(index);
        }

        public static void CompactTrailingNulls()
        {
            while (AllyProfileIds.Count > 0 && string.IsNullOrEmpty(AllyProfileIds[AllyProfileIds.Count - 1]))
                AllyProfileIds.RemoveAt(AllyProfileIds.Count - 1);
        }

        public static List<string> SnapshotFilledAllies()
        {
            return AllyProfileIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        public static bool AlliesReadyForStart(Mercenaries mercenaries)
        {
            foreach (var id in AllyProfileIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                var merc = mercenaries.Get(id);
                if (merc == null || string.IsNullOrEmpty(merc.MercClassId))
                    return false;
            }
            return true;
        }
    }
}
