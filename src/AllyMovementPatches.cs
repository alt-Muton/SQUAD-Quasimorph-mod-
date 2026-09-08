using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>Squad allies use movement-mode AP like the player.</summary>
    [HarmonyPatch(typeof(Monster), nameof(Monster.GetActualActionPoints))]
    internal static class MonsterActualApPatch
    {
        private static void Postfix(Monster __instance, ref int __result)
        {
            if (!AllyMovementService.IsSquadAlly(__instance))
                return;
            __result = AllyMovementService.GetModeActionPointsWithAllyBonuses(__instance, __instance.MovementState);
        }
    }

    /// <summary>Skip leftover turn slots once mode AP is spent (same as Player.ProcessActionPoint).</summary>
    [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
    internal static class MonsterProcessApPatch
    {
        private static bool Prefix(Monster __instance)
        {
            if (!AllyMovementService.IsSquadAlly(__instance))
                return true;

            try
            {
                var player = UI.Resolve<Creatures>()?.Player;
                AllyMovementService.ApplyFollowOrIdleMovement(__instance, player);
            }
            catch
            {
                // ignore resolve failures
            }

            var max = __instance.GetActualActionPoints();
            if (__instance.ActionPointsProcessed >= max)
            {
                __instance.IsProcessing = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Monster), nameof(Monster.NextTurnStarted))]
    internal static class MonsterNextTurnPatch
    {
        private static void Postfix(Monster __instance)
        {
            if (!AllyMovementService.IsSquadAlly(__instance))
                return;
            try
            {
                var player = UI.Resolve<Creatures>()?.Player;
                AllyMovementService.ApplyFollowOrIdleMovement(__instance, player);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>When the player cycles walk/sneak/run, following allies match immediately.</summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), "SwapMoveMode")]
    internal static class PlayerSwapMoveModePatch
    {
        private static void Postfix(Creatures creatures)
        {
            if (!Plugin.Config.Enabled || creatures?.Player == null)
                return;
            foreach (var monster in AllyMovementService.EnumerateSquadAllies(creatures))
                AllyMovementService.ApplyFollowOrIdleMovement(monster, creatures.Player);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.NextTurnStarted))]
    internal static class PlayerNextTurnMovementPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!Plugin.Config.Enabled || __instance == null)
                return;
            try
            {
                var creatures = UI.Resolve<Creatures>();
                if (creatures == null)
                    return;
                foreach (var monster in AllyMovementService.EnumerateSquadAllies(creatures))
                    AllyMovementService.ApplyFollowOrIdleMovement(monster, __instance);
            }
            catch
            {
                // ignore
            }
        }
    }
}
