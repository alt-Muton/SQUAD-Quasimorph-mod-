using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Vanilla FollowTarget (escort VIP AI) picks targets from the leader's FoV and
    /// shoots without checking WeaponRecord.Range. Squad mercs should use their own
    /// vision and respect effective weapon range like enemy Attack AI.
    /// Escort / non-squad FollowTarget behaviour is left unchanged.
    /// </summary>
    internal static class AllyFollowCombatPatches
    {
        [HarmonyPatch(typeof(FollowTarget), "TrySelectLeaderTarget")]
        private static class SquadAllySkipLeaderVisionPatch
        {
            private static bool Prefix(FollowTarget __instance, ref bool __result)
            {
                var owner = Traverse.Create(__instance).Field("_owner").GetValue<Monster>();
                if (owner == null || !AllyMovementService.IsSquadAlly(owner))
                    return true;

                // Fall through to TrySelectVisibleTarget (own Vision).
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(FightState), "TryRangeAttack", typeof(CellPosition))]
        private static class SquadAllyWeaponRangePatch
        {
            private static bool Prefix(FightState __instance, CellPosition targetPos, ref bool __result)
            {
                if (!(__instance is FollowTarget))
                    return true;

                var owner = Traverse.Create(__instance).Field("_owner").GetValue<Monster>();
                if (owner?.CreatureData?.Inventory == null || !AllyMovementService.IsSquadAlly(owner))
                    return true;

                var weaponRecord = owner.CreatureData.Inventory.CurrentWeapon?.Record<WeaponRecord>();
                if (weaponRecord == null || weaponRecord.IsMelee)
                    return true;

                var distance = CellPosition.Distance(owner.CreatureData.Position, targetPos);
                if (weaponRecord.Range >= distance)
                    return true;

                // Out of effective range — let FollowTarget.TryAttack move closer instead.
                __result = false;
                return false;
            }
        }
    }
}
