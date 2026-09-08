using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    // --- Creature.Activate* (empty base used by Monster; Player overrides are separate) ---

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateAttackedTrigger))]
    internal static class AllyPerkAttackedPatch
    {
        private static void Postfix(Creature __instance, bool wasHit)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.PlayerAttacked);
            if (wasHit)
                AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.PlayerHitted);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateDodgedTrigger))]
    internal static class AllyPerkDodgedPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.EnemyAttackMissed);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateCanibalizmTrigger))]
    internal static class AllyPerkCannibalPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.EatRawHumanMeat);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateMeleeKillTrigger))]
    internal static class AllyPerkMeleeKillPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.MeleeKill);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateThrowKillTrigger))]
    internal static class AllyPerkThrowKillPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.AnyThrowKill);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateReloadTrigger))]
    internal static class AllyPerkReloadPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnWeaponReload);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateExplosionKillTrigger))]
    internal static class AllyPerkExplosionKillPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.AnyKill);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateExplosionHitTrigger))]
    internal static class AllyPerkExplosionHitPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ExplosionHitOnEnemy);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateBarrelExplosionTrigger))]
    internal static class AllyPerkBarrelPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ExplodeBarrel);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateBrokeObstacleTrigger))]
    internal static class AllyPerkBrokeObstaclePatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.BrokeObstacles);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateAmputationTrigger))]
    internal static class AllyPerkAmputationPatch
    {
        private static void Postfix(Creature __instance, bool victim)
        {
            AllyPerkProgressService.Raise(__instance,
                victim ? PerkLevelUpActionType.GetAmputationInCombat : PerkLevelUpActionType.RipLimbInCombat);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateWoundInflictionTrigger))]
    internal static class AllyPerkWoundInflictPatch
    {
        private static void Postfix(Creature __instance, DamageHitInfo hitInfo)
        {
            AllyPerkProgressService.RouteWoundInfliction(__instance, hitInfo);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivatePhysicalWoundInflictionTrigger))]
    internal static class AllyPerkPhysWoundPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnPhysicalWoundInflict);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateElementalWoundInflictionTrigger))]
    internal static class AllyPerkElemWoundPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnElementalWoundInflict);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateMeleeWoundInflictionTrigger))]
    internal static class AllyPerkMeleeWoundPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnMeleeWoundInflict);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateDmgAbsorbedTrigger))]
    internal static class AllyPerkDmgAbsorbPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.DmgAbsorbedByResist);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivatePhysicalDmgAbsorbedTrigger))]
    internal static class AllyPerkPhysAbsorbPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.PhysicalDmgAbsorbedByResist);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateElementalDmgAbsorbedTrigger))]
    internal static class AllyPerkElemAbsorbPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ElementalDmgAbsorbedByResist);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateOnAllyDeathTrigger))]
    internal static class AllyPerkAllyDeathPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnAllyDeath);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateLastBulletTrigger))]
    internal static class AllyPerkLastBulletPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ShotLastBullet);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateFirstBulletTrigger))]
    internal static class AllyPerkFirstBulletPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ShotFirstBullet);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateBelowAmmoTrigger))]
    internal static class AllyPerkBelowAmmoPatch
    {
        private static void Postfix(Creature __instance, PerkLevelUpActionType actionType)
        {
            AllyPerkProgressService.Raise(__instance, actionType);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivatePutFireTrigger))]
    internal static class AllyPerkPutFirePatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.PutFireOnEnemy);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateTakeBurnTrigger))]
    internal static class AllyPerkTakeBurnPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnTakingBurn);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateImplant))]
    internal static class AllyPerkImplantPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.ImplantActivation);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.OnCrit))]
    internal static class AllyPerkOnCritPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnCrit);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.OnHit))]
    internal static class AllyPerkOnHitPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.ApplyOnHitWeaponActions(__instance);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.OnBlindHit))]
    internal static class AllyPerkBlindHitPatch
    {
        private static void Postfix(Creature __instance)
        {
            AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnRangedBlindHit);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.OnWoundAdded))]
    internal static class AllyPerkWoundAddedPatch
    {
        private static void Postfix(Creature __instance, BodyPartWound bodyPartWound)
        {
            AllyPerkProgressService.ApplyReceivedWoundActions(__instance, bodyPartWound);
        }
    }

    [HarmonyPatch(typeof(Creature), "HealthOnValueChanged")]
    internal static class AllyPerkHealthChangedPatch
    {
        private static void Postfix(Creature __instance, int obj)
        {
            AllyPerkProgressService.ApplyHealthThresholdActions(__instance, obj);
        }
    }

    [HarmonyPatch(typeof(Creature), "ProcessDamage")]
    internal static class AllyPerkProcessDamagePatch
    {
        private static void Postfix(Creature __instance, DamageHitInfo hitInfo)
        {
            AllyPerkProgressService.ApplyKillFromDamage(__instance, hitInfo);
        }
    }

    /// <summary>Monster melee never calls OnHit / MeleeHitOnEnemy like Player does.</summary>
    [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessMeleeAttackOnEnemy))]
    internal static class AllyPerkMonsterMeleePatch
    {
        private static void Postfix(Monster __instance, DamageHitInfo dmgHit, BasePickupItem weapon)
        {
            AllyPerkProgressService.ApplyMeleeHitActions(__instance, dmgHit, weapon);
        }
    }

    [HarmonyPatch(typeof(Monster), "OnMoved")]
    internal static class AllyPerkMonsterMovedPatch
    {
        private static void Postfix(Monster __instance)
        {
            AllyPerkProgressService.ApplyMovementActions(__instance);
        }
    }
}
