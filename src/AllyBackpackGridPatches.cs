using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// VIP inspect panel is ~4 columns; reshape squad-ally backpacks to 4×N and
    /// materialize every row so lower cells stay visible/usable.
    /// </summary>
    internal static class AllyBackpackGridUtil
    {
        private static readonly MethodInfo CreateItemSlotMethod =
            AccessTools.Method(typeof(ItemGrid), "CreateItemSlot", new[] { typeof(int), typeof(int), typeof(bool).MakeByRefType() });

        public static bool IsSquadAllyBackpackStore(ItemStorage storage)
        {
            if (storage == null || storage.Source != ItemStorageSource.Backpack || !Plugin.Config.Enabled)
                return false;
            if (!SquadRaidState.Active || SquadRaidState.UniqueIdToProfileId.Count == 0)
                return false;

            var creatures = Plugin.GetCachedDungeonState()?.Get<Creatures>();
            if (creatures == null)
                return false;

            foreach (var monster in AllyMovementService.EnumerateSquadAllies(creatures))
            {
                if (monster?.CreatureData?.Inventory != null
                    && ReferenceEquals(monster.CreatureData.Inventory.BackpackStore, storage))
                    return true;
            }

            return false;
        }

        public static void FixMonsterInspectBackpackGrid(MonsterInspectWindow window)
        {
            if (window == null || !Plugin.Config.Enabled || !SquadRaidState.Active)
                return;

            try
            {
                if (!window.ShouldShowAllyInventory())
                    return;

                var tr = Traverse.Create(window);
                var creature = tr.Field<Creature>("_inspectedCreature").Value;
                var inventory = tr.Field<Inventory>("_inventory").Value;
                var itemGrid = tr.Field<ItemGrid>("_itemGrid").Value;
                var scrollbar = tr.Field<CommonScrollBar>("_scrollbar").Value;

                if (!(creature is Monster monster) || !AllyMovementService.IsSquadAlly(monster))
                    return;
                if (inventory?.BackpackStore == null || itemGrid == null)
                    return;

                AllyBackpackLayout.EnsureVipCompatibleLayout(inventory);

                var store = inventory.BackpackStore;
                itemGrid.Initialize(store);
                itemGrid.InitFakeGrid(AllyBackpackLayout.VipPanelWidth, Mathf.Max(4, store.Height));

                if (itemGrid.ItemGridScroll != null)
                    itemGrid.ItemGridScroll.VerticalNormalizedPosition = 1f;

                if (scrollbar != null)
                {
                    scrollbar.Value = 1f;
                    scrollbar.UpdateVisualDeferred();
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("Ally backpack grid fix failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        /// <summary>
        /// Same as vanilla CullSlotsOutsideViewport, but for every backpack row.
        /// </summary>
        public static void CullAllRows(ItemGrid grid)
        {
            if (grid == null || CreateItemSlotMethod == null)
                return;

            var storage = grid.Storage;
            if (storage == null || storage.Height <= 0)
                return;

            var tr = Traverse.Create(grid);
            var slotsExist = tr.Field<List<bool>>("_slotsExist").Value;
            var slots = tr.Field<List<ItemSlot>>("_slots").Value;
            var slotsToReuse = tr.Field<List<ItemSlot>>("_slotsToReuse").Value;
            if (slotsExist == null || slots == null || slotsToReuse == null)
                return;

            const int startRow = 0;
            var endRow = storage.Height - 1;

            slotsToReuse.Clear();
            for (var i = slots.Count - 1; i >= 0; i--)
            {
                var cellPos = slots[i].CellPos;
                if (cellPos.Y > endRow || cellPos.Y < startRow)
                {
                    if (cellPos.Y >= 0 && cellPos.Y < slotsExist.Count)
                        slotsExist[cellPos.Y] = false;
                    slotsToReuse.Add(slots[i]);
                }
            }

            for (var row = startRow; row <= endRow; row++)
            {
                if (row < 0 || row >= slotsExist.Count || row >= storage.Height || slotsExist[row])
                    continue;

                slotsExist[row] = true;
                for (var col = 0; col < storage.Width; col++)
                {
                    var args = new object[] { col, row, false };
                    CreateItemSlotMethod.Invoke(grid, args);
                    if ((bool)args[2])
                        col++;
                }
            }

            foreach (var slot in slotsToReuse)
            {
                slot.ConfigureEmptyHintTag(string.Empty);
                UI.Pools.ItemSlots.Put(slot.gameObject, setParent: true);
                slots.Remove(slot);
            }

            slotsToReuse.Clear();
        }
    }

    [HarmonyPatch(typeof(ItemGrid), "CullSlotsOutsideViewport")]
    internal static class AllyBackpackFullCullPatch
    {
        private static bool Prefix(ItemGrid __instance)
        {
            if (!AllyBackpackGridUtil.IsSquadAllyBackpackStore(__instance.Storage))
                return true;

            AllyBackpackGridUtil.CullAllRows(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MonsterInspectWindow), "RefreshBackpackPage")]
    internal static class AllyBackpackRefreshPagePatch
    {
        private static void Postfix(MonsterInspectWindow __instance)
        {
            AllyBackpackGridUtil.FixMonsterInspectBackpackGrid(__instance);
        }
    }

    /// <summary>
    /// Parameterless RefreshSlots only — the private RefreshSlots(ItemSlot…) overload is ambiguous.
    /// </summary>
    [HarmonyPatch(typeof(MonsterInspectWindow), nameof(MonsterInspectWindow.RefreshSlots), new System.Type[0])]
    internal static class AllyBackpackRefreshSlotsPatch
    {
        private static void Postfix(MonsterInspectWindow __instance)
        {
            AllyBackpackGridUtil.FixMonsterInspectBackpackGrid(__instance);
        }
    }
}
