using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    internal static class SquadCapture
    {
        public static void FromLeader(Mercenary leader)
        {
            if (leader == null)
                return;
            // Keep AllyProfileIds as-is; raid begin happens on StartMission.
            Plugin.Logger.Log("Squad capture prep. Leader=" + leader.ProfileId
                              + " Allies=[" + string.Join(",", SquadSelectionState.SnapshotFilledAllies()) + "]");
        }
    }

    [HarmonyPatch(typeof(PrepareRaidScreen))]
    internal static class PrepareRaidPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PrepareRaidScreen.Configure), typeof(Station))]
        private static void ConfigureStationPostfix(PrepareRaidScreen __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            PrepareRaidSquadController.Ensure(__instance).OnConfigure();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PrepareRaidScreen.Configure), typeof(Mission), typeof(bool))]
        private static void ConfigureMissionPostfix(PrepareRaidScreen __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            PrepareRaidSquadController.Ensure(__instance).OnConfigure();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PrepareRaidScreen.SelectMercenary))]
        private static void SelectMercenaryPostfix(PrepareRaidScreen __instance, Mercenary mercenary)
        {
            if (!Plugin.Config.Enabled)
                return;

            if (mercenary != null)
            {
                for (var i = SquadSelectionState.AllyProfileIds.Count - 1; i >= 0; i--)
                {
                    if (SquadSelectionState.AllyProfileIds[i] == mercenary.ProfileId)
                        SquadSelectionState.AllyProfileIds.RemoveAt(i);
                }
            }
            else
            {
                SquadSelectionState.Clear();
            }

            PrepareRaidSquadController.Ensure(__instance).SyncFromScreen();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PrepareRaidScreen.RefreshMercenary))]
        private static void RefreshMercenaryPostfix(PrepareRaidScreen __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            var ctrl = PrepareRaidSquadController.Ensure(__instance);
            ctrl.SyncFromScreen();
            ctrl.RefreshAll();
            ctrl.RefreshStartButton();
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnEnable")]
        private static void OnEnablePostfix(PrepareRaidScreen __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            Plugin.Logger.Log("PrepareRaidScreen.OnEnable — syncing squad UI");
            var ctrl = PrepareRaidSquadController.Ensure(__instance);
            ctrl.SyncFromScreen();
            ctrl.RefreshStartButton();
        }

        [HarmonyPrefix]
        [HarmonyPatch("StartOperationButtonOnClick")]
        private static void StartOperationPrefix(PrepareRaidScreen __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            var leader = Traverse.Create(__instance).Field<Mercenary>("_mercenary").Value;
            SquadCapture.FromLeader(leader);
        }
    }

    [HarmonyPatch(typeof(BriefingScreen), nameof(BriefingScreen.Configure))]
    internal static class BriefingCapturePatch
    {
        private static void Prefix(Mercenary mercenary)
        {
            if (!Plugin.Config.Enabled)
                return;
            SquadCapture.FromLeader(mercenary);
        }
    }

    [HarmonyPatch(typeof(SpaceGameMode), "StartMission")]
    internal static class StartMissionPatch
    {
        private static void Prefix(SpaceModeFinishedData data)
        {
            if (!Plugin.Config.Enabled)
                return;
            SquadRaidState.BeginRaid(data?.mercProfileId, SquadSelectionState.SnapshotFilledAllies());
        }
    }

    [HarmonyPatch(typeof(SpaceGameMode), "VisitStation")]
    internal static class VisitStationPatch
    {
        private static void Prefix(SpaceModeFinishedData data)
        {
            if (!Plugin.Config.Enabled)
                return;
            SquadRaidState.BeginRaid(data?.mercProfileId, SquadSelectionState.SnapshotFilledAllies());
        }
    }
}
