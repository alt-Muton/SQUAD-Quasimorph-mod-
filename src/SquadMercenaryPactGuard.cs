using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Pact / ultimate must only run on the controlled MercenaryInRaid Player body.
    /// Squad ally monsters (including leader ghosts) must never activate pact.
    /// </summary>
    internal static class SquadMercenaryPactGuard
    {
        public static void SanitizeMercenaryPacts(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return;

            Mercenaries mercenaries;
            try
            {
                mercenaries = state.Get<Mercenaries>();
            }
            catch
            {
                return;
            }

            if (mercenaries?.Values == null || mercenaries.MercToUltimateSkulls == null)
                return;

            var inRaidId = mercenaries.MercenaryInRaid?.ProfileId;
            if (string.IsNullOrEmpty(inRaidId))
                return;

            var skullToOwner = new Dictionary<string, string>();
            foreach (var merc in mercenaries.Values)
            {
                if (merc?.CreatureData == null || !merc.CreatureData.HasUltimate)
                    continue;
                var skullId = merc.CreatureData.UltimateSkullItemId;
                if (string.IsNullOrEmpty(skullId))
                    continue;
                if (!skullToOwner.ContainsKey(skullId))
                    skullToOwner[skullId] = merc.ProfileId;
            }

            var keys = mercenaries.MercToUltimateSkulls.Keys.ToList();
            foreach (var profileId in keys)
            {
                var mappedSkull = mercenaries.MercToUltimateSkulls[profileId];
                var merc = mercenaries.Get(profileId);

                if (merc?.CreatureData == null || !merc.CreatureData.HasUltimate)
                {
                    mercenaries.MercToUltimateSkulls.Remove(profileId);
                    Plugin.Logger.LogWarning("Pact guard: removed stale MercToUltimateSkulls " + profileId);
                    continue;
                }

                if (merc.CreatureData.UltimateSkullItemId != mappedSkull)
                {
                    mercenaries.MercToUltimateSkulls[profileId] = merc.CreatureData.UltimateSkullItemId;
                    Plugin.Logger.LogWarning("Pact guard: fixed skull map for " + profileId);
                    continue;
                }

                if (skullToOwner.TryGetValue(mappedSkull, out var ownerId)
                    && ownerId != profileId
                    && profileId != inRaidId)
                {
                    mercenaries.MercToUltimateSkulls.Remove(profileId);
                    Plugin.Logger.LogWarning("Pact guard: removed duplicate skull owner " + profileId
                                              + " (owner=" + ownerId + ")");
                }
            }
        }

        public static bool IsBlockedUltimateCreature(Creature creature)
        {
            if (!Plugin.Config.Enabled || creature == null || creature is Player)
                return false;

            if (creature is Monster monster && AllyMovementService.IsSquadAlly(monster))
                return true;

            Mercenaries mercenaries;
            try
            {
                mercenaries = UI.Resolve<Mercenaries>();
            }
            catch
            {
                return false;
            }

            var inRaidId = mercenaries?.MercenaryInRaid?.ProfileId;
            var locId = creature.CreatureData?.LocalizationId;
            if (string.IsNullOrEmpty(inRaidId) || string.IsNullOrEmpty(locId))
                return false;

            // Leader ghost at elevator: PlayerAlliance monster with leader LocalizationId.
            if (locId == inRaidId && creature.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.ActivateUltimatePerk))]
    internal static class SquadAllyUltimateBlockPatch
    {
        private static bool Prefix(Creature __instance)
        {
            if (SquadMercenaryPactGuard.IsBlockedUltimateCreature(__instance))
            {
                Plugin.Logger.LogWarning("Pact guard: blocked ultimate on non-player creature uid="
                                          + __instance.CreatureData?.UniqueId);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ActivateUltimatePerk))]
    internal static class PlayerUltimateInRaidGuardPatch
    {
        private static bool Prefix(Player __instance)
        {
            if (!Plugin.Config.Enabled || __instance?.Mercenary == null)
                return true;

            Mercenaries mercenaries;
            try
            {
                mercenaries = UI.Resolve<Mercenaries>();
            }
            catch
            {
                return true;
            }

            var inRaid = mercenaries?.MercenaryInRaid;
            if (inRaid == null)
                return true;

            if (!ReferenceEquals(__instance.Mercenary, inRaid))
            {
                Plugin.Logger.LogWarning("Pact guard: blocked ultimate — player merc "
                                          + __instance.Mercenary.ProfileId
                                          + " != InRaid " + inRaid.ProfileId);
                return false;
            }

            if (__instance.CreatureData != null
                && inRaid.CreatureData != null
                && !ReferenceEquals(__instance.CreatureData, inRaid.CreatureData))
            {
                Plugin.Logger.LogWarning("Pact guard: blocked ultimate — CreatureData mismatch for "
                                          + inRaid.ProfileId);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveGame))]
    internal static class SaveGamePactSanitizePatch
    {
        private static void Prefix(SaveManager __instance)
        {
            if (!Plugin.Config.Enabled)
                return;
            try
            {
                var state = Traverse.Create(__instance).Field("_state").GetValue<State>()
                            ?? Plugin.GetCachedDungeonState();
                if (state?.Get<DungeonGameMode>() == null)
                    return;
                SquadMercenaryPactGuard.SanitizeMercenaryPacts(state);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
