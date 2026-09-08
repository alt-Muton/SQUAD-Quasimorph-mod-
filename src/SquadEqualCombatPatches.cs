using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Match enemy Attack.Tactic for squad FollowTarget: shoot when the line is clear,
    /// hold (then vanilla TryFollow) when in range but blocked. No flank/hide.
    /// </summary>
    internal static class SquadEqualCombatPatches
    {
        [HarmonyPatch(typeof(FollowTarget), "TryAttack")]
        [HarmonyPriority(Priority.First)]
        private static class SquadTryAttackHoldWhenBlockedPatch
        {
            private static bool Prefix(FollowTarget __instance, ref bool __result)
            {
                var owner = Traverse.Create(__instance).Field("_owner").GetValue<Monster>();
                if (!SquadShotGeometry.IsActive(owner))
                    return true;

                try
                {
                    AccessTools.Method(typeof(FollowTarget), "RefreshTarget")?.Invoke(__instance, null);
                    if (!__instance.HasTarget)
                        return true;

                    var current = __instance.CurrentTarget;
                    if (current.Type == AiTargetType.None || current.Value == null)
                        return true;

                    var targetPos = current.GetPosition(owner.CreatureData.Position);
                    AccessTools.Method(typeof(FollowTarget), "PickBestWeapon", new[] { typeof(CellPosition) })
                        ?.Invoke(__instance, new object[] { targetPos });

                    var weapon = owner.CreatureData.Inventory?.CurrentWeapon?.Record<WeaponRecord>();
                    if (weapon == null || weapon.IsMelee)
                        return true;

                    var distance = CellPosition.Distance(owner.CreatureData.Position, targetPos);
                    if (weapon.Range < distance)
                        return true;

                    if (SquadShotGeometry.CanFire(owner, targetPos))
                        return true;

                    Plugin.Logger.Log("EqualAI: hold, blocked shot uid=" + owner.CreatureData.UniqueId
                                      + " target=" + targetPos + " dist=" + distance);
                    __result = false;
                    return false;
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogWarning("EqualAI: TryAttack prefix failed.");
                    Plugin.Logger.LogException(ex);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(AiBehaviour), nameof(AiBehaviour.TryShoot))]
        [HarmonyPriority(Priority.First)]
        private static class SquadTryShootFriendlyFirePatch
        {
            private static bool Prefix(AiBehaviour __instance, CellPosition targetPos, ref bool __result)
            {
                var owner = __instance?.Monster;
                if (!SquadShotGeometry.IsActive(owner))
                    return true;

                if (!SquadShotGeometry.HasAllyOnCenterPath(owner, targetPos))
                    return true;

                Plugin.Logger.Log("EqualAI: block TryShoot (ally/player on path) uid="
                                  + owner.CreatureData.UniqueId + " target=" + targetPos);
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(FirearmSystem), nameof(FirearmSystem.CanMonsterBeAllyCover))]
        private static class SquadPlayerNeverCoverPatch
        {
            private static void Postfix(Creature shooter, Creature creature, ref bool __result)
            {
                if (!__result || creature == null || !(shooter is Monster monster))
                    return;
                if (!SquadShotGeometry.IsActive(monster))
                    return;
                if (creature is Player)
                    __result = false;
            }
        }
    }
}
