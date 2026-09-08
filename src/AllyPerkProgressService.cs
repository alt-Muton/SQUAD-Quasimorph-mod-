using System;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// Forwards perk XP actions to squad-ally Mercenary roster entries.
    /// Allies are Monsters (not Player), so vanilla RaisePerkAction never runs for them.
    /// </summary>
    internal static class AllyPerkProgressService
    {
        private static Action<PerkRecord> _notifyHandler;

        public static bool TryGetAllyContext(Creature creature, out Mercenary merc, out PerkFactory perkFactory)
        {
            merc = null;
            perkFactory = null;
            if (creature?.CreatureData == null || !Plugin.Config.Enabled || !SquadRaidState.Active)
                return false;
            if (!(creature is Monster))
                return false;
            if (!SquadRaidState.UniqueIdToProfileId.TryGetValue(creature.CreatureData.UniqueId, out var profileId))
                return false;
            if (string.IsNullOrEmpty(profileId))
                return false;

            try
            {
                merc = UI.Resolve<Mercenaries>()?.Get(profileId);
                perkFactory = UI.Resolve<PerkFactory>();
            }
            catch
            {
                return false;
            }

            return merc != null && perkFactory != null;
        }

        public static void Raise(Creature creature, PerkLevelUpActionType actionType, int exp = -1)
        {
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;
            Raise(merc, perkFactory, actionType, exp);
        }

        public static void Raise(Mercenary merc, PerkFactory perkFactory, PerkLevelUpActionType actionType, int exp = -1)
        {
            if (merc == null || perkFactory == null)
                return;
            try
            {
                if (!merc.EnabledProgression)
                    merc.EnabledProgression = true;
                PerkSystem.RaisePerkAction(actionType, merc, perkFactory, exp);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Ally perk raise failed: " + actionType);
                Plugin.Logger.LogException(ex);
            }
        }

        /// <summary>Credit every living squad ally (e.g. shared events like NoticeHiddenEnemy / OnAllyDeath).</summary>
        public static void RaiseForAllSquadAllies(PerkLevelUpActionType actionType, int exp = -1, int excludeUniqueId = -1)
        {
            if (!Plugin.Config.Enabled || !SquadRaidState.Active)
                return;
            Creatures creatures;
            PerkFactory perkFactory;
            Mercenaries mercenaries;
            try
            {
                creatures = UI.Resolve<Creatures>();
                perkFactory = UI.Resolve<PerkFactory>();
                mercenaries = UI.Resolve<Mercenaries>();
            }
            catch
            {
                return;
            }
            if (creatures == null || perkFactory == null || mercenaries == null)
                return;

            foreach (var kv in SquadRaidState.UniqueIdToProfileId)
            {
                if (excludeUniqueId >= 0 && kv.Key == excludeUniqueId)
                    continue;
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (monster == null || monster.CreatureData.Health.Dead)
                    continue;
                var merc = mercenaries.Get(kv.Value);
                if (merc != null)
                    Raise(merc, perkFactory, actionType, exp);
            }
        }

        public static void ApplyLevelUpOnConsumeItem(Creature creature, ItemClass itemClass)
        {
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            // Mirror Player.ApplyLevelUpOnConsumeItem (ItemClass enum layout).
            if ((uint)(itemClass - 10) > 2u)
            {
                switch (itemClass)
                {
                    default:
                        return;
                    case ItemClass.Pills:
                        Raise(merc, perkFactory, PerkLevelUpActionType.EatingMouthMeds);
                        break;
                    case ItemClass.Syringe:
                        Raise(merc, perkFactory, PerkLevelUpActionType.UsingSyringeMeds);
                        break;
                    case ItemClass.Medpack:
                        break;
                }
                Raise(merc, perkFactory, PerkLevelUpActionType.UsingAnyMeds);
                Raise(merc, perkFactory, PerkLevelUpActionType.UseDrugs);
            }
            else
            {
                if (itemClass == ItemClass.Alcohol)
                    Raise(merc, perkFactory, PerkLevelUpActionType.UseDrugs);
                Raise(merc, perkFactory, PerkLevelUpActionType.EatingFood);
            }
        }

        public static void ApplyConsumableSideEffectsForAlly(ConsumableRecord record, Creature creature)
        {
            if (record == null || !TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            if (record.StarvationValue != 0)
            {
                try
                {
                    int restore = MercenarySystem.GetStarvationRestoreValue(merc, record.StarvationValue);
                    creature.CreatureData.Starvation.Restore(restore);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogException(ex);
                }
            }

            if (record.EffectChance == null || record.EffectChance.Count == 0)
                return;

            foreach (var kv in record.EffectChance)
            {
                if (StatusEffectsSystem.ShouldIgnoreStatusEffect(creature.CreatureData, kv.Key))
                    continue;

                float chance = kv.Value;
                if (kv.Key.EndsWith("Addiction"))
                {
                    Raise(merc, perkFactory, PerkLevelUpActionType.EatingAddictives);
                    chance += creature.CreatureData.GetAddictionChanceBonus();
                }

                TryApplyStatusEffectOnAlly(kv.Key, chance, creature, merc, perkFactory);
            }
        }

        public static void ApplyWoundFixateActions(Creature creature, BodyPartWound wound)
        {
            if (wound == null || !TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;
            Raise(merc, perkFactory, PerkLevelUpActionType.OnWoundFixate);
            var nature = wound.WoundSlotRecord?.NatureType;
            if (nature == "human")
                Raise(merc, perkFactory, PerkLevelUpActionType.OnHumanWoundFixate);
            else if (nature == "cyborg")
                Raise(merc, perkFactory, PerkLevelUpActionType.OnCyborgWoundFixate);
        }

        private static void TryApplyStatusEffectOnAlly(
            string id,
            float applyChance,
            Creature creature,
            Mercenary merc,
            PerkFactory perkFactory)
        {
            try
            {
                var effects = creature.CreatureData.EffectsController;
                var badLuck = StatusEffectsSystem.GetOrCreateBadLuck(effects);
                float adjusted = StatusEffectsSystem.GetAdjustedChance(effects, id, applyChance);
                if (UnityEngine.Random.Range(0f, 1f) > adjusted)
                {
                    badLuck.IncrementCounter(id);
                    return;
                }

                var record = Data.StatusEffects.GetRecord(id);
                if (record != null && record.ProgressionType == StatusEffectProgressionType.None)
                    StatusEffectsSystem.ApplyStatusEffect(id, effects, 0);
                else
                    StatusEffectsSystem.ApplyStatusEffect(id, effects);

                badLuck.ResetCounter(id);
                if (id == "infectionEffect")
                    Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveInfection);
                if (id.EndsWith("Addiction"))
                    Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveAddiction);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }

        public static void EnsureProgressionEnabled(Mercenary merc)
        {
            if (merc == null)
                return;
            merc.EnabledProgression = true;
            EnsureLevelUpNotify(merc);
        }

        public static void EnsureLevelUpNotify(Mercenary merc)
        {
            if (merc == null)
                return;
            if (_notifyHandler == null)
            {
                _notifyHandler = record =>
                {
                    try
                    {
                        UI.Staff?.NotificationPanel?.AddPerkLevelUpNotify(record);
                    }
                    catch
                    {
                        // HUD may be unavailable
                    }
                };
            }

            merc.OnPerkLevelUp -= _notifyHandler;
            merc.OnPerkLevelUp += _notifyHandler;
        }

        /// <summary>Mirror Player.OnHit weapon perk XP (melee ammo / shot ammo / barehand).</summary>
        public static void ApplyOnHitWeaponActions(Creature creature)
        {
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            var weapon = creature.CreatureData?.Inventory?.CurrentWeapon;
            if (weapon == null)
                return;

            try
            {
                var wc = weapon.Comp<WeaponComponent>();
                if (wc == null)
                    return;

                if (wc.Melee)
                    ApplyLevelUpActionMelee(merc, perkFactory, weapon);
                else
                    ApplyLevelUpActionShot(merc, perkFactory, creature, weapon);

                if (weapon.Record<WeaponRecord>().IsImplicit)
                    Raise(merc, perkFactory, PerkLevelUpActionType.OnBarehandHit);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }

        public static void ApplyMeleeHitActions(Creature creature, DamageHitInfo dmgHit, BasePickupItem weapon)
        {
            if (dmgHit == null || dmgHit.wasMiss)
                return;
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            Raise(merc, perkFactory, PerkLevelUpActionType.MeleeHitOnEnemy);
            if (StatusEffectsSystem.HasAssociatedStatusEffect(dmgHit.info.damage))
                Raise(merc, perkFactory, PerkLevelUpActionType.HitWithStatusEffect);
            if (creature.MovementState == CreatureMovementState.Slow)
                Raise(merc, perkFactory, PerkLevelUpActionType.OnStealthAttack);
            if (dmgHit.wasCrit)
                Raise(merc, perkFactory, PerkLevelUpActionType.OnCrit);

            if (weapon != null)
            {
                try
                {
                    ApplyLevelUpActionMelee(merc, perkFactory, weapon);
                    if (weapon.Record<WeaponRecord>().IsImplicit)
                        Raise(merc, perkFactory, PerkLevelUpActionType.OnBarehandHit);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogException(ex);
                }
            }
        }

        public static void ApplyMovementActions(Creature creature)
        {
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            if (creature.MovementState == CreatureMovementState.Run)
                Raise(merc, perkFactory, PerkLevelUpActionType.Run);

            float itemsWeight = creature.CreatureData.GetItemsWeight();
            if (itemsWeight >= 10f)
            {
                int exp = Mathf.RoundToInt(itemsWeight / 10f);
                Raise(merc, perkFactory, PerkLevelUpActionType.WeightMove, exp);
                if (creature.MovementState == CreatureMovementState.Slow)
                    Raise(merc, perkFactory, PerkLevelUpActionType.WeightStealth, exp);
            }
        }

        public static void ApplyReceivedWoundActions(Creature creature, BodyPartWound bodyPartWound)
        {
            if (bodyPartWound == null)
                return;
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            bool damaging = false;
            bool bleeding = false;
            var effects = creature.CreatureData.EffectsController;
            foreach (int subWound in bodyPartWound.SubWounds)
            {
                var effect = effects.First(subWound);
                if (effect == null)
                    continue;
                if (effect is WoundEffectActionPointDamage)
                {
                    bleeding = true;
                    damaging = true;
                    break;
                }
                if (effect is WoundEffect we && we.WoundEffectType == WoundEffectType.Damage)
                    damaging = true;
            }

            if (bleeding)
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveBleedingWound);
            if (damaging)
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveDamagingWound);
            if (!bodyPartWound.IsMinor && bodyPartWound.WoundSlotRecord.NatureType == "human")
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveHumanWound);
            if (!bodyPartWound.IsMinor && bodyPartWound.WoundSlotRecord.NatureType == "cyborg")
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveCyborgWound);
            if (bodyPartWound.CanBeInfected
                && bodyPartWound.HasAttachedEffect
                && bodyPartWound.AttachedEffectId == "infectionEffect"
                && !creature.CreatureData.IgnoreInfection)
            {
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveInfection);
            }
            if (!bodyPartWound.IsMinor)
                Raise(merc, perkFactory, PerkLevelUpActionType.ReceiveWound);
        }

        public static void ApplyHealthThresholdActions(Creature creature, int delta)
        {
            if (delta >= 0)
                return;
            if (!TryGetAllyContext(creature, out var merc, out var perkFactory))
                return;

            var health = creature.CreatureData.Health;
            int prevValue = health.Value - delta;
            int max = health.MaxValue;
            if (max <= 0)
                return;

            float prevPercent = (float)prevValue / max;
            float currPercent = health.Percent;

            Check(0.5f, PerkLevelUpActionType.HealthBelow50Percent);
            Check(0.4f, PerkLevelUpActionType.HealthBelow40Percent);
            Check(0.3f, PerkLevelUpActionType.HealthBelow30Percent);
            Check(0.2f, PerkLevelUpActionType.HealthBelow20Percent);

            void Check(float t, PerkLevelUpActionType action)
            {
                if (prevPercent > t && currPercent <= t)
                    Raise(merc, perkFactory, action);
            }
        }

        /// <summary>
        /// ProcessDamage only awards AnyKill when dealer is Player. Credit squad allies the same way.
        /// </summary>
        public static void ApplyKillFromDamage(Creature victim, DamageHitInfo hitInfo)
        {
            if (victim?.CreatureData == null || hitInfo?.damageDealer == null)
                return;
            if (!victim.CreatureData.Health.Dead)
                return;
            if (hitInfo.damageDealer is Player)
                return;
            if (!TryGetAllyContext(hitInfo.damageDealer, out var merc, out var perkFactory))
                return;

            Raise(merc, perkFactory, PerkLevelUpActionType.AnyKill);
            if (victim.CanBeBackstabbed)
                Raise(merc, perkFactory, PerkLevelUpActionType.IdleKill);
            if (hitInfo.wasThrown)
                Raise(merc, perkFactory, PerkLevelUpActionType.AnyThrowKill);
        }

        public static void RouteWoundInfliction(Creature dealer, DamageHitInfo hitInfo)
        {
            if (hitInfo == null)
                return;
            if (!TryGetAllyContext(dealer, out _, out _))
                return;

            if (hitInfo.isPhysical)
                dealer.ActivatePhysicalWoundInflictionTrigger();
            else
                dealer.ActivateElementalWoundInflictionTrigger();

            if (hitInfo.wasMeleeHit)
                dealer.ActivateMeleeWoundInflictionTrigger();
            else
                dealer.ActivateRangedWoundInflictionTrigger();

            Raise(dealer, PerkLevelUpActionType.OnWoundInflict);
        }

        private static void ApplyLevelUpActionMelee(Mercenary merc, PerkFactory perkFactory, BasePickupItem weapon)
        {
            if (weapon.Record<WeaponRecord>().RequiredAmmo == "BatteryCells")
                Raise(merc, perkFactory, PerkLevelUpActionType.OnAttackWithBatteryAmmo);
        }

        private static void ApplyLevelUpActionShot(Mercenary merc, PerkFactory perkFactory, Creature creature, BasePickupItem weapon)
        {
            var weaponRecord = weapon.Record<WeaponRecord>();
            var weaponComponent = weapon.Comp<WeaponComponent>();
            switch (weaponRecord.RequiredAmmo)
            {
                case "Shells":
                    Raise(merc, perkFactory, PerkLevelUpActionType.ShotShellsAmmo);
                    break;
                case "Bullets":
                case "Medium":
                    Raise(merc, perkFactory, PerkLevelUpActionType.Shot9mm42Ammo);
                    break;
                case "Heavy":
                    Raise(merc, perkFactory, PerkLevelUpActionType.ShotHeavyAmmo);
                    break;
                case "SuperHeavy":
                    Raise(merc, perkFactory, PerkLevelUpActionType.ShotSuperHeavyAmmo);
                    break;
                case "BatteryCells":
                    Raise(merc, perkFactory, PerkLevelUpActionType.ShotBatteryAmmo);
                    Raise(merc, perkFactory, PerkLevelUpActionType.OnAttackWithBatteryAmmo);
                    break;
            }

            if (StatusEffectsSystem.HasAssociatedStatusEffect(weaponComponent.OverridenAmmo.DmgType))
                Raise(merc, perkFactory, PerkLevelUpActionType.HitWithStatusEffect);

            switch (weaponComponent.OverridenAmmo.DmgType)
            {
                case "fire":
                case "beam":
                case "plasma":
                    Raise(merc, perkFactory, PerkLevelUpActionType.OnFireBeamAttack);
                    break;
            }

            if (weaponRecord.WeaponClass == WeaponClass.GrenadeLauncher
                || weaponRecord.WeaponClass == WeaponClass.RocketLauncher)
            {
                Raise(merc, perkFactory, PerkLevelUpActionType.ThrowGrenade);
            }

            if (creature.MovementState == CreatureMovementState.Slow)
                Raise(merc, perkFactory, PerkLevelUpActionType.OnStealthAttack);

            Raise(merc, perkFactory, PerkLevelUpActionType.ShotRangedWeapon);
        }
    }
}
