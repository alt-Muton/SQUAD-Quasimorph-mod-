using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    [HarmonyPatch(typeof(SelectMercenaryScreen), "PanelOnSelected")]
    internal static class SelectMercenaryFilterPatch
    {
        private static bool Prefix(SelectMercenaryScreen __instance, string mercId)
        {
            if (!Plugin.Config.Enabled)
                return true;

            var leader = UI.Get<PrepareRaidScreen>() != null
                ? Traverse.Create(UI.Get<PrepareRaidScreen>()).Field<Mercenary>("_mercenary").Value
                : null;

            var leaderId = leader?.ProfileId;
            var slot = SquadSelectionState.ActiveSelectSlot;

            // Ally slot selection: block mercs already in squad (except replacing self).
            if (slot >= 0)
            {
                string current = null;
                if (slot < SquadSelectionState.AllyProfileIds.Count)
                    current = SquadSelectionState.AllyProfileIds[slot];

                if (mercId == leaderId || (SquadSelectionState.IsOccupiedByOther(mercId, slot) && mercId != current))
                {
                    SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(
                        SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
                    return false;
                }
                return true;
            }

            // Leader selection: block mercs already chosen as allies.
            if (SquadSelectionState.AllyProfileIds.Contains(mercId))
            {
                SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(
                    SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
                return false;
            }

            return true;
        }
    }

    /// <summary>Track when vanilla leader picker opens so ActiveSelectSlot stays -1.</summary>
    [HarmonyPatch(typeof(PrepareRaidScreen), "SelectMercButtonOnClick")]
    internal static class LeaderSelectOpensPatch
    {
        private static void Prefix()
        {
            SquadSelectionState.ActiveSelectSlot = -1;
        }
    }

    [HarmonyPatch(typeof(PrepareRaidScreen), "MercUnblockedButtonOnClick")]
    internal static class LeaderReselectOpensPatch
    {
        private static void Prefix()
        {
            SquadSelectionState.ActiveSelectSlot = -1;
        }
    }
}
