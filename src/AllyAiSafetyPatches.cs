using System;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Squad allies use merc inventories that can have empty CurrentWeapon.
    /// Vanilla ReloadManeuver.AllowUse NREs on null weapon and leaves IsProcessing=true → turn softlock.
    /// </summary>
    [HarmonyPatch(typeof(ReloadManeuver), nameof(ReloadManeuver.AllowUse))]
    internal static class ReloadManeuverNullWeaponPatch
    {
        private static bool Prefix(ReloadManeuver __instance, ref bool __result, out float weight)
        {
            weight = 0f;
            try
            {
                var owner = Traverse.Create(__instance).Field("_owner").GetValue<Monster>();
                if (owner == null || !AllyMovementService.IsSquadAlly(owner))
                    return true;

                var weapon = owner.CreatureData?.Inventory?.CurrentWeapon;
                if (weapon == null)
                {
                    __result = false;
                    return false;
                }
            }
            catch
            {
                // fall through to original
            }
            return true;
        }
    }

    /// <summary>If ally AI throws during its AP, force-end processing so TurnController can advance.</summary>
    [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
    internal static class AllyProcessApSafetyPatch
    {
        private static Exception Finalizer(Monster __instance, Exception __exception)
        {
            if (__exception == null)
                return null;
            if (!AllyMovementService.IsSquadAlly(__instance))
                return __exception;

            Plugin.Logger.LogWarning("Ally ProcessActionPoint threw; clearing IsProcessing. " + __exception.GetType().Name
                                     + ": " + __exception.Message);
            try
            {
                __instance.IsProcessing = false;
            }
            catch
            {
                // ignore
            }
            // Swallow so TurnController.Tick can continue.
            return null;
        }
    }
}
