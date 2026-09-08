using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Intercept player death: if a living squad ally exists, promote them instead of ending the raid.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnDead")]
    internal static class PlayerOnDeadHandoffPatch
    {
        private static bool Prefix(Player __instance)
        {
            if (!Plugin.Config.Enabled)
                return true;

            try
            {
                if (LeaderHandoffService.TryBeginHandoffOrSchedule(__instance))
                {
                    Plugin.Logger.Log("Player.OnDead raid-end skipped — handoff pending/done (death anim handled separately).");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("Player.OnDead handoff prefix failed.");
                Plugin.Logger.LogException(ex);
            }

            return true;
        }
    }

    /// <summary>
    /// Safety net if death still reaches the finish hook while a living squad ally remains.
    /// </summary>
    [HarmonyPatch(typeof(DungeonGameMode), "OnPlayerDied")]
    internal static class OnPlayerDiedHandoffGuardPatch
    {
        private static bool Prefix()
        {
            if (!Plugin.Config.Enabled || !SquadRaidState.Active)
                return true;

            try
            {
                var creatures = Plugin.GetCachedDungeonState()?.Get<Creatures>();
                var player = creatures?.Player;
                if (player?.CreatureData == null || !player.CreatureData.Health.Dead)
                    return true;

                if (LeaderHandoffService.IsHandoffPending)
                {
                    Plugin.Logger.Log("OnPlayerDied blocked — handoff delay in progress.");
                    return false;
                }

                if (!LeaderHandoffService.HasLivingSquadAlly(creatures))
                    return true;

                if (LeaderHandoffService.TryBeginHandoffOrSchedule(player))
                {
                    Plugin.Logger.Log("OnPlayerDied blocked — late leader handoff started/scheduled.");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }

            return true;
        }
    }

    /// <summary>
    /// While Dead, vanilla Update returns before mod hooks — recover handoff / force last death here.
    /// Also runs post-handoff rewire while Alive (Monster.OnDestroy clears shared CreatureData deps).
    /// </summary>
    [HarmonyPatch(typeof(DungeonGameMode), "Update")]
    internal static class DungeonUpdateDeadHandoffRecoveryPatch
    {
        private static void Prefix(DungeonGameMode __instance)
        {
            if (!Plugin.Config.Enabled || !SquadRaidState.Active)
                return;

            try
            {
                LeaderHandoffService.TickDungeonFrame(Plugin.GetCachedDungeonState());
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }

    /// <summary>
    /// After promoting an ally, StarvationEffect may briefly lack Inject deps and NRE every AP.
    /// </summary>
    [HarmonyPatch(typeof(StarvationEffect), nameof(StarvationEffect.ProcessActionPoint))]
    internal static class StarvationEffectNullDepsPatch
    {
        private static bool Prefix(StarvationEffect __instance)
        {
            return EffectDepsGuard.Ensure(__instance, needRaid: true, needDifficulty: true);
        }
    }

    /// <summary>
    /// PainThreshold NRE / wrong creature after Monster.OnDestroy on shared effect instances.
    /// </summary>
    [HarmonyPatch(typeof(PainThreshold), nameof(PainThreshold.ProcessActionPoint))]
    internal static class PainThresholdNullDepsPatch
    {
        private static bool Prefix(PainThreshold __instance)
        {
            return EffectDepsGuard.Ensure(__instance, needRaid: false, needDifficulty: false);
        }
    }

    /// <summary>
    /// BodyPartWound fixate/heal ticks must run on Player after handoff or wounds stay "healing"
    /// forever while still dealing damage / feeding PainThreshold.
    /// </summary>
    [HarmonyPatch(typeof(BodyPartWound), nameof(BodyPartWound.ProcessActionPoint))]
    internal static class BodyPartWoundNullDepsPatch
    {
        private static bool Prefix(BodyPartWound __instance)
        {
            return EffectDepsGuard.Ensure(__instance, needRaid: false, needDifficulty: false);
        }
    }

    /// <summary>
    /// StatusEffect DoT after handoff must bind to Player for HUD remove events to fire.
    /// </summary>
    [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.ProcessActionPoint))]
    internal static class StatusEffectNullDepsPatch
    {
        private static bool Prefix(StatusEffect __instance)
        {
            return EffectDepsGuard.Ensure(__instance, needRaid: false, needDifficulty: false);
        }
    }

    internal static class EffectDepsGuard
    {
        public static bool Ensure(BaseEffect effect, bool needRaid, bool needDifficulty)
        {
            try
            {
                var tr = Traverse.Create(effect);
                var creature = tr.Field("_creature").GetValue<Creature>();
                var player = Plugin.GetCachedDungeonState()?.Get<Creatures>()?.Player;

                // After handoff, effects on the controlled merc must use Player — not a destroyed Monster.
                if (player?.CreatureData?.EffectsController != null
                    && effect != null
                    && player.CreatureData.EffectsController.Effects.Contains(effect))
                {
                    if (!ReferenceEquals(creature, player))
                    {
                        effect.SetCreature(player);
                        creature = player;
                    }
                }
                else if (creature == null && player != null)
                {
                    effect.SetCreature(player);
                    creature = player;
                }

                if (creature == null)
                    return false;

                if (!needRaid && !needDifficulty)
                    return true;

                var factory = Singleton<EffectFactory>.Instance;
                factory?.GrabDependencies(effect);

                if (needRaid && tr.Field("_raidMetadata").GetValue<RaidMetadata>() == null)
                    return false;
                if (needDifficulty && tr.Field("_difficulty").GetValue<Difficulty>() == null)
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
