using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// VIP inspect panel is ~4 columns wide; merc backpacks are typically 6 wide.
    /// During raid, reshape squad-ally backpacks to 4×taller so every cell fits the panel.
    /// Native width is restored on handoff / raid end.
    /// </summary>
    internal static class AllyBackpackLayout
    {
        public const int VipPanelWidth = 4;

        public static bool IsSquadAllyInventory(Inventory inventory)
        {
            if (inventory?.BackpackStore == null || !Plugin.Config.Enabled || !SquadRaidState.Active)
                return false;

            var creatures = Plugin.GetCachedDungeonState()?.Get<Creatures>();
            if (creatures == null)
                return false;

            foreach (var monster in AllyMovementService.EnumerateSquadAllies(creatures))
            {
                if (monster?.CreatureData?.Inventory != null
                    && ReferenceEquals(monster.CreatureData.Inventory, inventory))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Force BackpackStore to 4 columns with the same cell count as the equipped backpack
        /// (no bonus empty rows). Example: 6×4 → 4×6.
        /// </summary>
        public static void EnsureVipCompatibleLayout(Inventory inventory)
        {
            if (inventory?.BackpackStore == null || !Plugin.Config.Enabled)
                return;

            var store = inventory.BackpackStore;
            if (store.Width <= 0)
                return;

            try
            {
                var nativeCapacity = GetNativeCapacity(inventory);
                var targetHeight = Mathf.Max(1, Mathf.CeilToInt(nativeCapacity / (float)VipPanelWidth));

                if (store.Width == VipPanelWidth && store.Height == targetHeight)
                    return;

                // Never shrink below what items currently need (2-wide packing).
                var itemCells = 0;
                foreach (var item in store.Items)
                    itemCells += Mathf.Max(1, item.InventoryWidthSize);
                targetHeight = Mathf.Max(targetHeight, Mathf.CeilToInt(itemCells / (float)VipPanelWidth));

                if (store.Width == VipPanelWidth && store.Height == targetHeight)
                    return;

                var excess = new List<BasePickupItem>();
                store.ResizeAndReshuffle(VipPanelWidth, targetHeight, ref excess);
                AbsorbExcess(store, excess, maxExtraRows: 4);

                // If AbsorbExcess grew past capacity due to packing, trim only empty trailing rows
                // when possible — keep at least targetHeight.
                TrimEmptyTrailingRows(store, targetHeight);

                Plugin.Logger.Log("Ally backpack reshaped to " + store.Width + "x" + store.Height
                                  + " (nativeCells=" + nativeCapacity
                                  + " items=" + store.Items.Count + ").");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("Ally backpack VIP layout failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        private static int GetNativeCapacity(Inventory inventory)
        {
            var backpackItem = inventory.BackpackSlot?.First;
            if (backpackItem != null)
            {
                var record = backpackItem.Record<BackpackRecord>();
                if (record != null)
                {
                    var h = ItemInteractionSystem.GetBackpackHeight(backpackItem);
                    return Mathf.Max(1, record.Width * Mathf.Max(1, h));
                }
            }

            var store = inventory.BackpackStore;
            // No backpack item: keep current cell count (already-VIP grids stay as-is).
            if (store.Width > 0 && store.Height > 0)
                return store.Width * store.Height;

            return 6;
        }

        private static void TrimEmptyTrailingRows(ItemStorage store, int minHeight)
        {
            if (store == null || store.Width != VipPanelWidth)
                return;

            minHeight = Mathf.Max(1, minHeight);
            while (store.Height > minHeight)
            {
                var lastRow = store.Height - 1;
                var rowEmpty = true;
                for (var x = 0; x < store.Width; x++)
                {
                    if (store.GetItem(x, lastRow) != null)
                    {
                        rowEmpty = false;
                        break;
                    }
                }

                if (!rowEmpty)
                    break;

                store.Resize(store.Width, store.Height - 1);
            }
        }

        /// <summary>Restore width/height from the equipped backpack item (player / Magnum UI).</summary>
        public static void RestoreNativeBackpackLayout(Inventory inventory)
        {
            if (inventory?.BackpackStore == null)
                return;

            try
            {
                int width;
                int height;
                var backpackItem = inventory.BackpackSlot?.First;
                if (backpackItem != null)
                {
                    var record = backpackItem.Record<BackpackRecord>();
                    if (record == null)
                        return;
                    width = record.Width;
                    height = ItemInteractionSystem.GetBackpackHeight(backpackItem);
                }
                else
                {
                    width = 6;
                    height = 1;
                }

                if (inventory.BackpackStore.Width == width && inventory.BackpackStore.Height == height)
                    return;

                var excess = new List<BasePickupItem>();
                inventory.BackpackStore.ResizeAndReshuffle(width, height, ref excess);
                AbsorbExcess(inventory.BackpackStore, excess);

                Plugin.Logger.Log("Ally backpack restored to native " + width + "x" + height + ".");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("Ally backpack native restore failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        private static void AbsorbExcess(ItemStorage store, List<BasePickupItem> excess, int maxExtraRows = 48)
        {
            if (store == null || excess == null || excess.Count == 0)
                return;

            for (var guard = 0; excess.Count > 0 && guard < maxExtraRows; guard++)
            {
                store.ExpandHeight(1);
                for (var i = excess.Count - 1; i >= 0; i--)
                {
                    if (store.TryPutItem(excess[i], CellPosition.Zero))
                        excess.RemoveAt(i);
                }
            }

            if (excess.Count > 0)
                Plugin.Logger.LogWarning("Ally backpack still has " + excess.Count + " excess items after reshape.");
        }
    }

    [HarmonyPatch(typeof(Inventory), "BackpackSlotOnItemAdded")]
    internal static class AllyBackpackAddedLayoutPatch
    {
        private static void Postfix(Inventory __instance)
        {
            if (AllyBackpackLayout.IsSquadAllyInventory(__instance))
                AllyBackpackLayout.EnsureVipCompatibleLayout(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), "BackpackSlotOnOnItemSwitched")]
    internal static class AllyBackpackSwitchedLayoutPatch
    {
        private static void Postfix(Inventory __instance)
        {
            if (AllyBackpackLayout.IsSquadAllyInventory(__instance))
                AllyBackpackLayout.EnsureVipCompatibleLayout(__instance);
        }
    }
}
