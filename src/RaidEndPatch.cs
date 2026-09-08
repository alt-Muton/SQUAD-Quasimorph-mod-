using System.Linq;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    [HarmonyPatch(typeof(MissionSystem), nameof(MissionSystem.ProcessFinishedDungeonData))]
    internal static class RaidEndPatch
    {
        private static void Postfix(
            Mercenaries mercenaries,
            MagnumCargo magnumCargo,
            MagnumProgression magnumSpaceship,
            MagnumProjects magnumProjects,
            SpaceTime spaceTime,
            Difficulty difficulty,
            PerkFactory perkFactory)
        {
            if (!Plugin.Config.Enabled)
                return;
            if (SquadRaidState.AllyProfileIds.Count == 0)
            {
                SquadRaidState.Clear();
                return;
            }

            AllyRaidEndService.ProcessAllies(
                mercenaries,
                magnumCargo,
                magnumSpaceship,
                magnumProjects,
                spaceTime,
                difficulty,
                perkFactory);
        }
    }
}
