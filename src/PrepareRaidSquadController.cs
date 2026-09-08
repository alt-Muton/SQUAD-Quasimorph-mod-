using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using UnityEngine;
using UnityEngine.UI;

namespace Squad_Leader
{
    /// <summary>Manages cloned MercenaryBlock slots on PrepareRaidScreen.</summary>
    internal sealed class PrepareRaidSquadController
    {
        public static PrepareRaidSquadController Instance { get; private set; }

        private readonly PrepareRaidScreen _screen;
        private readonly List<AllySlotView> _slots = new List<AllySlotView>();
        private Transform _mercenaryBlock;
        private Transform _contentParent;

        private PrepareRaidSquadController(PrepareRaidScreen screen)
        {
            _screen = screen;
        }

        public static PrepareRaidSquadController Ensure(PrepareRaidScreen screen)
        {
            if (Instance != null && Instance._screen == screen)
                return Instance;
            Instance?.Teardown();
            Instance = new PrepareRaidSquadController(screen);
            Instance.Initialize();
            return Instance;
        }

        public static void ClearInstance()
        {
            Instance?.Teardown();
            Instance = null;
        }

        private void Initialize()
        {
            var tr = Traverse.Create(_screen);
            var selectMerc = tr.Field<CommonButton>("_selectMercButton").Value;
            _mercenaryBlock = UiPathUtil.FindMercenaryBlock(_screen.transform, selectMerc?.transform);
            if (_mercenaryBlock == null)
            {
                Plugin.Logger.LogError("PrepareRaidSquadController: MercenaryBlock not found. Ally UI disabled.");
                Plugin.Logger.LogError("Screen hierarchy tip: " + UiPathUtil.DescribeHierarchy(_screen.transform, 5));
                return;
            }
            _contentParent = _mercenaryBlock.parent;
            Plugin.Logger.Log("PrepareRaidSquadController ready. Block=" + _mercenaryBlock.name
                              + " parent=" + _contentParent.name
                              + " sibling=" + _mercenaryBlock.GetSiblingIndex()
                              + " children=" + _contentParent.childCount);
        }

        public void OnConfigure()
        {
            ClearSlots();
            SquadSelectionState.Clear();
        }

        public void SyncFromScreen()
        {
            if (!Plugin.Config.Enabled)
            {
                Plugin.Logger.Log("SyncFromScreen skipped: mod disabled");
                return;
            }
            if (_mercenaryBlock == null || _contentParent == null)
            {
                Plugin.Logger.LogWarning("SyncFromScreen skipped: MercenaryBlock not bound");
                return;
            }

            var leader = Traverse.Create(_screen).Field<Mercenary>("_mercenary").Value;
            if (leader == null)
            {
                Plugin.Logger.Log("SyncFromScreen: no leader — clearing ally slots");
                ClearSlots();
                SquadSelectionState.Clear();
                return;
            }

            // Drop allies that match new leader or are invalid.
            for (var i = SquadSelectionState.AllyProfileIds.Count - 1; i >= 0; i--)
            {
                var id = SquadSelectionState.AllyProfileIds[i];
                if (string.IsNullOrEmpty(id) || id == leader.ProfileId)
                    SquadSelectionState.AllyProfileIds.RemoveAt(i);
            }

            RebuildSlotsToMatchState();
            EnsureTrailingAddSlot(leader);
            Plugin.Logger.Log("SyncFromScreen: leader=" + leader.ProfileId
                              + " allySlots=" + _slots.Count
                              + " filled=[" + string.Join(",", SquadSelectionState.SnapshotFilledAllies()) + "]");
            RefreshStartButton();
        }

        public void SyncAfterSlotChange()
        {
            var leader = Traverse.Create(_screen).Field<Mercenary>("_mercenary").Value;
            if (leader == null)
            {
                ClearSlots();
                SquadSelectionState.Clear();
                RefreshStartButton();
                return;
            }

            // Compact: remove empty middle slots by rebuilding from filled ids + trailing add.
            var filled = SquadSelectionState.SnapshotFilledAllies();
            SquadSelectionState.AllyProfileIds.Clear();
            SquadSelectionState.AllyProfileIds.AddRange(filled);
            RebuildSlotsToMatchState();
            EnsureTrailingAddSlot(leader);
            RefreshStartButton();
        }

        private void RebuildSlotsToMatchState()
        {
            while (_slots.Count > SquadSelectionState.AllyProfileIds.Count)
            {
                var last = _slots[_slots.Count - 1];
                _slots.RemoveAt(_slots.Count - 1);
                last.Destroy();
            }

            var mercenaries = UI.Resolve<Mercenaries>();
            for (var i = 0; i < SquadSelectionState.AllyProfileIds.Count; i++)
            {
                if (i >= _slots.Count)
                    _slots.Add(CreateSlot(i));
                else
                    _slots[i].SetIndex(i);

                var id = SquadSelectionState.AllyProfileIds[i];
                Mercenary merc = null;
                if (!string.IsNullOrEmpty(id) && mercenaries != null)
                    merc = mercenaries.Get(id);
                _slots[i].SetMercenary(merc, writeState: false);
            }
        }

        private void EnsureTrailingAddSlot(Mercenary leader)
        {
            if (leader == null)
                return;

            var needAdd = _slots.Count == 0 || _slots[_slots.Count - 1].Mercenary != null;
            if (!needAdd)
            {
                Plugin.Logger.Log("EnsureTrailingAddSlot: trailing empty slot already present (" + _slots.Count + ")");
                return;
            }

            var index = _slots.Count;
            SquadSelectionState.SetAlly(index, null);
            var slot = CreateSlot(index);
            _slots.Add(slot);
            slot.SetMercenary(null, writeState: false);
            Plugin.Logger.Log("EnsureTrailingAddSlot: created empty + slot index=" + index
                              + " active=" + slot.Root.activeInHierarchy
                              + " sibling=" + slot.Root.transform.GetSiblingIndex());
        }

        private AllySlotView CreateSlot(int index)
        {
            var clone = Object.Instantiate(_mercenaryBlock.gameObject, _contentParent, false);
            clone.name = "MercenaryBlock_Ally_" + index;
            clone.SetActive(true);

            var siblingIndex = _mercenaryBlock.GetSiblingIndex() + 1 + index;
            if (siblingIndex >= _contentParent.childCount)
                clone.transform.SetAsLastSibling();
            else
                clone.transform.SetSiblingIndex(siblingIndex);

            var rt = clone.transform as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);

            Plugin.Logger.Log("CreateSlot: " + clone.name + " parent=" + _contentParent.name
                              + " sibling=" + clone.transform.GetSiblingIndex()
                              + " size=" + (rt != null ? rt.sizeDelta.ToString() : "?"));
            return new AllySlotView(_screen, clone, index);
        }

        private void ClearSlots()
        {
            foreach (var slot in _slots)
                slot.Destroy();
            _slots.Clear();
        }

        public void RefreshAll()
        {
            foreach (var slot in _slots)
                slot.Refresh();
            RefreshStartButton();
        }

        public void RefreshStartButton()
        {
            if (_screen == null)
                return;
            var tr = Traverse.Create(_screen);
            var leader = tr.Field<Mercenary>("_mercenary").Value;
            var itemsBlock = tr.Field<bool>("_itemsRequirementBlock").Value;
            var mercenaries = UI.Resolve<Mercenaries>();
            var alliesOk = mercenaries == null || SquadSelectionState.AlliesReadyForStart(mercenaries);
            var active = leader != null
                         && !string.IsNullOrEmpty(leader.MercClassId)
                         && !itemsBlock
                         && alliesOk;
            tr.Method("RefreshStartOperationButton", active).GetValue();
        }

        private void Teardown()
        {
            ClearSlots();
        }
    }
}
