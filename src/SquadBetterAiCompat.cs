using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Better AI adds flank/hide/scatter on the same FollowTarget/TryShoot hooks.
    /// Equal AI must own squad combat, so those Better AI prefixes are skipped for operatives.
    /// </summary>
    internal static class SquadBetterAiCompat
    {
        public static void Patch(Harmony harmony)
        {
            PatchSkip(harmony,
                "Better_AI.CombatShotPatches+TryShootPatch",
                AccessTools.DeclaredMethod(typeof(SquadBetterAiCompat), nameof(SkipTryShootIfSquad)));
            PatchSkip(harmony,
                "Better_AI.CombatShotPatches+FollowTryAttackPatch",
                AccessTools.DeclaredMethod(typeof(SquadBetterAiCompat), nameof(SkipTryAttackIfSquad)));
        }

        private static void PatchSkip(Harmony harmony, string typeName, System.Reflection.MethodInfo skip)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                var method = type == null ? null : AccessTools.DeclaredMethod(type, "Prefix");
                if (method == null || skip == null)
                    return;

                harmony.Patch(method, prefix: new HarmonyMethod(skip) { priority = Priority.First });
                Plugin.Logger.Log("EqualAI: skipping Better AI " + typeName + " for squad operatives.");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("EqualAI: Better AI skip failed for " + typeName);
                Plugin.Logger.LogException(ex);
            }
        }

        public static bool SkipTryShootIfSquad(AiBehaviour __instance)
        {
            return !SquadShotGeometry.IsActive(__instance?.Monster);
        }

        public static bool SkipTryAttackIfSquad(FollowTarget __instance)
        {
            var owner = Traverse.Create(__instance).Field("_owner").GetValue<Monster>();
            return !SquadShotGeometry.IsActive(owner);
        }
    }
}
