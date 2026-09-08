using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Fills remaining Player-only perk XP gaps for squad allies (starvation, pain, consumables, ultimates, etc.).
    /// </summary>
    internal static class AllyPerkProgressGapsPatches
    {
        private static Creature _healActionOwner;
        private static readonly Dictionary<int, bool> SkipStealthPending = new Dictionary<int, bool>();
        private static readonly HashSet<int> EnemySpottedThisTurn = new HashSet<int>();

        // Soft-ref: skip our eat XP only when Hungry_NPC is loaded AND Enabled (not merely DLL present).
        private static readonly MethodInfo HungryNpcIsModActiveMethod =
            AccessTools.Method(AccessTools.TypeByName("Hungry_NPC.HungryNpcSystem"), "IsModActive");

        private static bool IsHungryNpcGrantingEatXp()
        {
            if (HungryNpcIsModActiveMethod == null)
                return false;
            try
            {
                return (bool)HungryNpcIsModActiveMethod.Invoke(null, null);
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(PainThreshold), "PainReaction")]
        private static class PainReactionPatch
        {
            private static void Postfix(PainThreshold __instance)
            {
                var creature = __instance._creature;
                if (creature == null || creature is Player)
                    return;
                if (__instance.CurrentLevel < __instance.MaxLevel)
                    return;
                AllyPerkProgressService.Raise(creature, PerkLevelUpActionType.OnPainLimitBreak);
            }
        }

        [HarmonyPatch(typeof(StarvationEffect), nameof(StarvationEffect.ProcessActionPoint))]
        private static class StarvationProcessPatch
        {
            private static void Postfix(StarvationEffect __instance)
            {
                if (__instance.Regen >= 0)
                    return;
                var creature = __instance._creature;
                if (creature == null || creature is Player)
                    return;
                AllyPerkProgressService.Raise(creature, PerkLevelUpActionType.ReceiveStarvationDamage);
            }
        }

        [HarmonyPatch(typeof(BodyPartWound), nameof(BodyPartWound.Fixate))]
        private static class WoundFixatePatch
        {
            private static void Postfix(BodyPartWound __instance)
            {
                if (!__instance.IsFixated)
                    return;
                var creature = __instance._creature;
                if (creature == null || creature is Player)
                    return;
                AllyPerkProgressService.ApplyWoundFixateActions(creature, __instance);
            }
        }

        [HarmonyPatch(typeof(ItemInteractionSystem), "ApplyConsumableProperty")]
        private static class ConsumablePropertyPatch
        {
            private static void Postfix(ConsumableRecord record, Creature creature)
            {
                if (creature == null || creature is Player || record == null)
                    return;
                // EatingFood / meds perk XP: Hungry_NPC grants this when Config.Enabled.
                // Skip only then — DLL present with Enabled=false must not drop XP.
                if (!IsHungryNpcGrantingEatXp())
                    AllyPerkProgressService.ApplyLevelUpOnConsumeItem(creature, record.ItemClass);
                AllyPerkProgressService.ApplyConsumableSideEffectsForAlly(record, creature);
            }
        }

        [HarmonyPatch(typeof(ItemInteractionSystem), nameof(ItemInteractionSystem.ApplyConsumable))]
        private static class ApplyConsumablePatch
        {
            private static void Postfix(BasePickupItem item, Creature creature, bool __result)
            {
                if (!__result || creature == null || creature is Player)
                    return;

                if (item != null && item.Id == "quasi_medical_kit_1")
                    AllyPerkProgressService.Raise(creature, PerkLevelUpActionType.EatingGavvakh);

                if (_healActionOwner != null
                    && _healActionOwner != creature
                    && AllyPerkProgressService.TryGetAllyContext(_healActionOwner, out _, out _))
                {
                    AllyPerkProgressService.Raise(_healActionOwner, PerkLevelUpActionType.OnAllyHeal);
                }
            }
        }

        [HarmonyPatch]
        private static class HealTeammateOwnerPatch
        {
            private static bool Prepare()
            {
                return AccessTools.TypeByName("MGSC.HealTeammate") != null;
            }

            private static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(AccessTools.TypeByName("MGSC.HealTeammate"), "ProcessActionPoint");
            }

            private static void Prefix(object __instance)
            {
                _healActionOwner = Traverse.Create(__instance).Field("_owner").GetValue<Creature>();
            }

            private static void Finalizer()
            {
                _healActionOwner = null;
            }
        }

        [HarmonyPatch(typeof(HealthHealAILogic), nameof(HealthHealAILogic.Heal))]
        private static class HealthHealOwnerPatch
        {
            private static void Prefix(HealthHealAILogic __instance)
            {
                _healActionOwner = __instance._owner;
            }

            private static void Finalizer()
            {
                _healActionOwner = null;
            }
        }

        [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateUltimatePerk))]
        private static class UltimateActivatePatch
        {
            private static void Postfix(Creature __instance, bool __result)
            {
                if (!__result)
                    return;
                AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.UltimateActivation);
            }
        }

        [HarmonyPatch(typeof(Monster), nameof(Monster.OnQmorphStageChanged))]
        private static class AllyQmorphPatch
        {
            private static void Postfix(Monster __instance, bool stageDirty)
            {
                if (!stageDirty)
                    return;
                AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnQmorphStageChanged);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ActivateOnAllyDeathTrigger))]
        private static class AllyDeathFanoutPatch
        {
            private static void Postfix()
            {
                AllyPerkProgressService.RaiseForAllSquadAllies(PerkLevelUpActionType.OnAllyDeath);
            }
        }

        [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
        private static class EnemyAlertAllyPatch
        {
            private static void Postfix(Monster __instance)
            {
                if (!Plugin.Config.Enabled || !SquadRaidState.Active || __instance == null)
                    return;
                try
                {
                    var creatures = UI.Resolve<Creatures>();
                    var player = creatures?.Player;
                    if (player == null || __instance.IsAlly(player))
                        return;
                    if (__instance.Behaviour?.Vision == null)
                        return;

                    foreach (var kv in SquadRaidState.UniqueIdToProfileId)
                    {
                        var ally = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                        if (ally == null || ally.CreatureData.Health.Dead)
                            continue;
                        if (__instance.Behaviour.Vision.IsVisible(ally.CreatureData.Position))
                            AllyPerkProgressService.Raise(ally, PerkLevelUpActionType.OnEnemyAlert);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.RaisePerkAction))]
        private static class NoticeHiddenFanoutPatch
        {
            private static void Postfix(PerkLevelUpActionType actionType, int exp)
            {
                if (actionType != PerkLevelUpActionType.NoticeHiddenEnemy)
                    return;
                AllyPerkProgressService.RaiseForAllSquadAllies(actionType, exp);
            }
        }

        [HarmonyPatch(typeof(FirearmSystem), nameof(FirearmSystem.ThrowGrenade))]
        private static class ThrowGrenadePatch
        {
            private static void Postfix(Creature user)
            {
                AllyPerkProgressService.Raise(user, PerkLevelUpActionType.ThrowGrenade);
            }
        }

        [HarmonyPatch(typeof(Monster), nameof(Monster.NextTurnStarted))]
        private static class AllySkipStealthAndSpottedResetPatch
        {
            private static void Prefix(Monster __instance)
            {
                if (!AllyMovementService.IsSquadAlly(__instance))
                    return;

                var uid = __instance.CreatureData.UniqueId;
                EnemySpottedThisTurn.Remove(uid);
                SkipStealthPending[uid] =
                    __instance.MovementState == CreatureMovementState.Slow
                    && __instance.ActionPointsProcessed <= 0;
            }

            private static void Postfix(Monster __instance)
            {
                if (!AllyMovementService.IsSquadAlly(__instance))
                    return;
                var uid = __instance.CreatureData.UniqueId;
                if (!SkipStealthPending.TryGetValue(uid, out var skip) || !skip)
                    return;
                SkipStealthPending.Remove(uid);
                AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.SkipTurnStealth);
            }
        }

        [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
        private static class AllyEnemySpottedPatch
        {
            private static void Postfix(Monster __instance)
            {
                if (!AllyMovementService.IsSquadAlly(__instance))
                    return;
                var uid = __instance.CreatureData.UniqueId;
                if (EnemySpottedThisTurn.Contains(uid))
                    return;
                try
                {
                    var creatures = UI.Resolve<Creatures>();
                    var player = creatures?.Player;
                    if (player == null || __instance.Behaviour?.Vision == null)
                        return;
                    foreach (var m in creatures.Monsters)
                    {
                        if (m == null || m.CreatureData.Health.Dead || m.IsAlly(player))
                            continue;
                        if (!__instance.Behaviour.Vision.IsVisible(m.CreatureData.Position))
                            continue;
                        EnemySpottedThisTurn.Add(uid);
                        AllyPerkProgressService.Raise(__instance, PerkLevelUpActionType.OnEnemySpotted);
                        return;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Player-only UI actions that still award XP to the acting merc when that merc is a roster ally
        /// inspected/edited outside Player control (repair on Spaceship uses arbitrary Mercenary).
        /// Fan-out PlaceTurret / Loot / Disassemble / Craft only when the acting creature is a squad ally —
        /// these vanilla call sites always use Player, so also credit if player IS the squad leader
        /// (already handled). Extra: map CreateThrowingWeapon owner if ever used by allies.
        /// </summary>
        [HarmonyPatch(typeof(MapEntityFactory), nameof(MapEntityFactory.CreateThrowingWeapon),
            typeof(Creature3dView), typeof(BasePickupItem), typeof(CellPosition), typeof(CellPosition), typeof(int))]
        private static class ThrowMeleeWeaponPatch
        {
            private static void Postfix(Creature3dView creature3dView)
            {
                try
                {
                    var creatures = UI.Resolve<Creatures>();
                    if (creatures == null || creature3dView == null)
                        return;
                    foreach (var kv in SquadRaidState.UniqueIdToProfileId)
                    {
                        var ally = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                        if (ally != null && ally.Creature3dView == creature3dView)
                        {
                            AllyPerkProgressService.Raise(ally, PerkLevelUpActionType.ThrowMeleeWeapon);
                            return;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
