using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Squad allies are Monsters, so vanilla death strips gear onto a separate corpse inventory.
    /// Raid-end then runs <see cref="MercenarySystem.DropOnDeathPenalty"/> on an empty merc —
    /// even DropPenalty.None ("equipment not lost") yields a naked clone.
    ///
    /// Fix: for None/Bag, keep Player-style shared corpse inventory; before dungeon teardown
    /// dumps the corpse, detach so Magnum merc keeps remaining gear for DropOnDeathPenalty.
    /// FullDrop keeps vanilla strip (gear stays on the floor corpse and is abandoned).
    /// </summary>
    internal static class AllyDifficultyLoot
    {
        public static bool IsSquadOperativeMonster(Monster monster)
        {
            if (monster?.CreatureData == null || !Plugin.Config.Enabled || !SquadRaidState.Active)
                return false;

            if (SquadRaidState.TryGetAllyProfileId(monster.CreatureData.UniqueId, out _))
                return true;

            var locId = monster.CreatureData.LocalizationId;
            if (string.IsNullOrEmpty(locId))
                return false;

            if (locId == SquadRaidState.LeaderProfileId)
                return true;

            return SquadRaidState.AllyProfileIds.Contains(locId);
        }

        public static bool ShouldKeepGearOnMerc(Difficulty difficulty)
        {
            if (difficulty?.Preset == null)
                return true;
            // FullDrop = lose everything with the body; None/Bag = recover via DropOnDeathPenalty.
            return difficulty.Preset.DropPenalty != DropPenalty.FullDrop;
        }

        public static Difficulty ResolveDifficulty(Monster monster)
        {
            try
            {
                var fromMonster = Traverse.Create(monster).Field("_difficulty").GetValue<Difficulty>();
                if (fromMonster != null)
                    return fromMonster;
            }
            catch
            {
                // ignore
            }

            try
            {
                return Plugin.GetCachedDungeonState()?.Get<Difficulty>() ?? UI.Resolve<Difficulty>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// If this corpse still references a living Magnum merc inventory, swap in an empty one
        /// so OnMapObstacleDestroyed cannot dump (and wipe) roster gear.
        /// </summary>
        public static void DetachIfSharedWithRosterMerc(CreatureData corpseData)
        {
            if (corpseData?.Inventory == null || !Plugin.Config.Enabled)
                return;

            Mercenaries mercenaries;
            try
            {
                mercenaries = Plugin.GetCachedDungeonState()?.Get<Mercenaries>() ?? UI.Resolve<Mercenaries>();
            }
            catch
            {
                return;
            }

            if (mercenaries?.Values == null)
                return;

            foreach (var merc in mercenaries.Values)
            {
                if (merc?.CreatureData?.Inventory == null)
                    continue;
                if (!ReferenceEquals(merc.CreatureData.Inventory, corpseData.Inventory))
                    continue;

                corpseData.Inventory = new Inventory();
                Plugin.Logger.Log("Detached shared corpse inventory from merc " + merc.ProfileId
                                  + " before corpse destroy.");
                return;
            }
        }
    }

    /// <summary>
    /// Keep Magnum gear on the merc when difficulty says equipment is not fully lost.
    /// </summary>
    [HarmonyPatch(typeof(Monster), nameof(Monster.CloneInventoryForCorpse))]
    internal static class SquadAllyCloneInventoryForCorpsePatch
    {
        private static bool Prefix(Monster __instance, ref Inventory __result)
        {
            if (!Plugin.Config.Enabled || __instance?.CreatureData?.Inventory == null)
                return true;

            if (!AllyDifficultyLoot.IsSquadOperativeMonster(__instance))
                return true;

            var difficulty = AllyDifficultyLoot.ResolveDifficulty(__instance);
            if (!AllyDifficultyLoot.ShouldKeepGearOnMerc(difficulty))
                return true; // FullDrop: vanilla strip onto corpse

            // Same as Player.CloneInventoryForCorpse — lootable corpse, gear stays on roster merc.
            __result = __instance.CreatureData.Inventory;
            Plugin.Logger.Log("Squad ally corpse shares inventory (DropPenalty="
                              + (difficulty?.Preset?.DropPenalty.ToString() ?? "?")
                              + ") uid=" + __instance.CreatureData.UniqueId);
            return false;
        }
    }

    /// <summary>
    /// KillDungeon destroys corpses before raid-end DropOnDeathPenalty — must not wipe merc gear.
    /// </summary>
    [HarmonyPatch(typeof(CorpseStorage), nameof(CorpseStorage.OnMapObstacleDestroyed))]
    internal static class SquadAllyCorpseDestroyDetachPatch
    {
        private static void Prefix(CorpseStorage __instance)
        {
            if (!Plugin.Config.Enabled || __instance == null)
                return;

            try
            {
                AllyDifficultyLoot.DetachIfSharedWithRosterMerc(__instance.CreatureData);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("Corpse detach before destroy failed.");
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
