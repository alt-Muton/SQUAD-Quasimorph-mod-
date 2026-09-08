using System;
using System.Linq;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Squad_Leader
{
    /// <summary>One cloned MercenaryBlock for an ally (or empty "+" slot).</summary>
    internal sealed class AllySlotView
    {
        public int Index { get; private set; }
        public GameObject Root { get; private set; }
        public Mercenary Mercenary { get; private set; }

        private readonly PrepareRaidScreen _screen;
        private CommonButton _selectMercButton;
        private CommonButton _mercUnblockedButton;
        private CommonButton _selectClassButton;
        private CommonButton _selectEquipmentButton;
        private CommonButton _selectImplantsButton;
        private GameObject _rankBlocked;
        private GameObject _rankUnblocked;
        private GameObject _implantsBlocked;
        private GameObject _implantsUnblocked;
        private GameObject _selectClassButtonBlocked;
        private GameObject _selectEquipmentButtonBlocked;
        private GameObject _mercBlocked;
        private GameObject _skullBlocked;
        private CreatureUIIcon _creatureIcon;
        private PerkSlot _ultimateSlot;
        private Image _rankIcon;
        private Image _classIcon;
        private Image _equipmentIcon;
        private Image _mercUnblockedIcon;
        private TextMeshProUGUI _mercNameLabel;
        private TextMeshProUGUI _classLabel;
        private TextMeshProUGUI _equipmentLabel;
        private MercenaryTooltipHandler _mercenaryTooltipHandler;
        private RankTooltipHandler _rankTooltipHandler;
        private Sprite _plusSprite;
        private Sprite _fullEquipmentSprite;
        private bool _wired;

        public AllySlotView(PrepareRaidScreen screen, GameObject root, int index)
        {
            _screen = screen;
            Root = root;
            Index = index;
            BindFromPaths(screen);
            WireButtons();
        }

        public void SetIndex(int index) => Index = index;

        public void Destroy()
        {
            UnwireButtons();
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
            Root = null;
            Mercenary = null;
        }

        public void SetMercenary(Mercenary merc, bool writeState = true)
        {
            Mercenary = merc;
            if (writeState)
                SquadSelectionState.SetAlly(Index, merc?.ProfileId);
            Refresh();
        }

        public void Refresh()
        {
            if (Root == null || !_wired)
                return;

            var flag = Mercenary != null;
            SafeSetActive(_selectMercButton, !flag);
            SafeSetActive(_rankBlocked, !flag);
            SafeSetActive(_rankUnblocked, flag);
            SafeSetActive(_selectImplantsButton, flag);
            SafeSetActive(_implantsBlocked, !flag);
            SafeSetActive(_implantsUnblocked, flag);
            SafeSetActive(_selectClassButton, flag);
            SafeSetActive(_selectClassButtonBlocked, !flag);
            SafeSetActive(_selectEquipmentButton, flag);
            SafeSetActive(_selectEquipmentButtonBlocked, !flag);
            SafeSetActive(_mercBlocked, !flag);
            SafeSetActive(_mercUnblockedButton, flag);
            SafeSetActive(_classLabel, flag);
            SafeSetActive(_equipmentLabel, flag);
            SafeSetActive(_skullBlocked, !flag || Mercenary == null || !Mercenary.CreatureData.HasUltimate);
            SafeSetActive(_ultimateSlot, flag && Mercenary != null && Mercenary.CreatureData.HasUltimate);
            SafeSetActive(_creatureIcon, flag);

            if (flag)
            {
                _creatureIcon?.Initialize(Mercenary);
                if (Mercenary.CreatureData.HasUltimate)
                    _ultimateSlot?.InitializeAsPact(Mercenary);

                if (_equipmentLabel != null && _equipmentIcon != null)
                {
                    if (Mercenary.CreatureData.Inventory.Empty)
                    {
                        _equipmentLabel.text = Localization.Get("ui.caption.selectequipment");
                        _equipmentLabel.color = Colors.Yellow;
                        if (_plusSprite != null)
                        {
                            _equipmentIcon.sprite = _plusSprite;
                            _equipmentIcon.SetNativeSize();
                        }
                    }
                    else
                    {
                        _equipmentLabel.text = Localization.Get("ui.caption.equipmentweight").ToLower().FirstLetterToUpperCase()
                                               + " " + FormatHelper.ToWeight(Mercenary.CreatureData.GetItemsWeight());
                        _equipmentLabel.color = Colors.AltGreen;
                        if (_fullEquipmentSprite != null)
                        {
                            _equipmentIcon.sprite = _fullEquipmentSprite;
                            _equipmentIcon.SetNativeSize();
                        }
                    }
                }

                if (_mercNameLabel != null)
                {
                    _mercNameLabel.text = Localization.Get("spec." + Mercenary.ProfileId + ".name");
                    _mercNameLabel.color = Colors.AltGreen;
                }

                if (_mercUnblockedIcon != null && Mercenary.CreatureData.Perks != null && Mercenary.CreatureData.Perks.Count > 0)
                {
                    var perkDesc = Data.Perks.GetRecord(Mercenary.CreatureData.Perks[0].PerkId).ContentDescriptor as PerkDescriptor;
                    if (perkDesc != null)
                        _mercUnblockedIcon.sprite = perkDesc.Icon;
                }

                _rankTooltipHandler?.Initialize(Mercenary);
                _mercenaryTooltipHandler?.Initialize(Mercenary, Data.MercenaryProfiles.GetRecord(Mercenary.ProfileId));

                if (_classLabel != null && _classIcon != null)
                {
                    if (string.IsNullOrEmpty(Mercenary.MercClassId))
                    {
                        _classLabel.text = Localization.Get("ui.caption.selectclass");
                        _classLabel.color = Colors.Yellow;
                        if (_plusSprite != null)
                        {
                            _classIcon.sprite = _plusSprite;
                            _classIcon.SetNativeSize();
                        }
                    }
                    else
                    {
                        _classLabel.text = Localization.Get("class." + Mercenary.MercClassId + ".name");
                        _classLabel.color = Colors.AltGreen;
                        var classDesc = Data.MercenaryClasses.GetRecord(Mercenary.MercClassId).ContentDescriptor as MercenaryClassDescriptor;
                        if (classDesc != null)
                        {
                            _classIcon.sprite = classDesc.SmallIcon;
                            _classIcon.SetNativeSizeX100();
                        }
                    }
                }

                if (_rankIcon != null && Mercenary.CreatureData.RankPerk != null)
                {
                    var rankDesc = Data.Perks.GetRecord(Mercenary.CreatureData.RankPerk.PerkId).ContentDescriptor as PerkDescriptor;
                    if (rankDesc != null)
                    {
                        _rankIcon.sprite = rankDesc.Icon;
                        _rankIcon.SetNativeSizeX100();
                    }
                }
            }
            else if (_mercNameLabel != null)
            {
                _mercNameLabel.text = Localization.Get("ui.caption.selectmerc");
                _mercNameLabel.color = Colors.Yellow;
            }

            if (_mercNameLabel != null)
                Localization.ActualizeFontAndSize(_mercNameLabel);
            if (_classLabel != null)
                Localization.ActualizeFontAndSize(_classLabel);
            if (_equipmentLabel != null)
                Localization.ActualizeFontAndSize(_equipmentLabel);
        }

        private void BindFromPaths(PrepareRaidScreen screen)
        {
            var tr = Traverse.Create(screen);
            var block = UiPathUtil.FindMercenaryBlock(screen.transform, tr.Field<CommonButton>("_selectMercButton").Value?.transform);
            if (block == null)
            {
                Plugin.Logger.LogError("AllySlotView: MercenaryBlock not found for path mapping.");
                return;
            }

            _plusSprite = tr.Field<Sprite>("_plusSprite").Value;
            _fullEquipmentSprite = tr.Field<Sprite>("_fullEquipmentSprite").Value;

            _selectMercButton = Map<CommonButton>(block, tr.Field<CommonButton>("_selectMercButton").Value);
            _mercUnblockedButton = Map<CommonButton>(block, tr.Field<CommonButton>("_mercUnblockedButton").Value);
            _selectClassButton = Map<CommonButton>(block, tr.Field<CommonButton>("_selectClassButton").Value);
            _selectEquipmentButton = Map<CommonButton>(block, tr.Field<CommonButton>("_selectEquipmentButton").Value);
            _selectImplantsButton = Map<CommonButton>(block, tr.Field<CommonButton>("_selectImplantsButton").Value);
            _rankBlocked = MapGo(block, tr.Field<GameObject>("_rankBlocked").Value);
            _rankUnblocked = MapGo(block, tr.Field<GameObject>("_rankUnblocked").Value);
            _implantsBlocked = MapGo(block, tr.Field<GameObject>("_implantsBlocked").Value);
            _implantsUnblocked = MapGo(block, tr.Field<GameObject>("_implantsUnblocked").Value);
            _selectClassButtonBlocked = MapGo(block, tr.Field<GameObject>("_selectClassButtonBlocked").Value);
            _selectEquipmentButtonBlocked = MapGo(block, tr.Field<GameObject>("_selectEquipmentButtonBlocked").Value);
            _mercBlocked = MapGo(block, tr.Field<GameObject>("_mercBlocked").Value);
            _skullBlocked = MapGo(block, tr.Field<GameObject>("_skullBlocked").Value);
            _creatureIcon = Map<CreatureUIIcon>(block, tr.Field<CreatureUIIcon>("_creatureIcon").Value);
            _ultimateSlot = Map<PerkSlot>(block, tr.Field<PerkSlot>("_ultimateSlot").Value);
            _rankIcon = Map<Image>(block, tr.Field<Image>("_rankIcon").Value);
            _classIcon = Map<Image>(block, tr.Field<Image>("_classIcon").Value);
            _equipmentIcon = Map<Image>(block, tr.Field<Image>("_equipmentIcon").Value);
            _mercUnblockedIcon = Map<Image>(block, tr.Field<Image>("_mercUnblockedIcon").Value);
            _mercNameLabel = Map<TextMeshProUGUI>(block, tr.Field<TextMeshProUGUI>("_mercNameLabel").Value);
            _classLabel = Map<TextMeshProUGUI>(block, tr.Field<TextMeshProUGUI>("_classLabel").Value);
            _equipmentLabel = Map<TextMeshProUGUI>(block, tr.Field<TextMeshProUGUI>("_equipmentLabel").Value);
            _mercenaryTooltipHandler = Map<MercenaryTooltipHandler>(block, tr.Field<MercenaryTooltipHandler>("_mercenaryTooltipHandler").Value);
            _rankTooltipHandler = Map<RankTooltipHandler>(block, tr.Field<RankTooltipHandler>("_rankTooltipHandler").Value);
        }

        private T Map<T>(Transform originalBlock, T original) where T : Component
        {
            if (original == null || Root == null)
                return null;
            var path = UiPathUtil.RelativePath(originalBlock, original.transform);
            if (string.IsNullOrEmpty(path))
                return null;
            var found = UiPathUtil.FindByRelativePath(Root.transform, path);
            return found != null ? found.GetComponent<T>() : null;
        }

        private GameObject MapGo(Transform originalBlock, GameObject original)
        {
            if (original == null || Root == null)
                return null;
            var path = UiPathUtil.RelativePath(originalBlock, original.transform);
            if (string.IsNullOrEmpty(path))
                return null;
            var found = UiPathUtil.FindByRelativePath(Root.transform, path);
            return found != null ? found.gameObject : null;
        }

        private void WireButtons()
        {
            if (_selectMercButton != null)
                _selectMercButton.OnClick += OnSelectMerc;
            if (_mercUnblockedButton != null)
                _mercUnblockedButton.OnClick += OnSelectMerc;
            if (_selectClassButton != null)
                _selectClassButton.OnClick += OnSelectClass;
            if (_selectEquipmentButton != null)
                _selectEquipmentButton.OnClick += OnSelectEquipment;
            if (_selectImplantsButton != null)
                _selectImplantsButton.OnClick += OnSelectImplants;
            _wired = true;
        }

        private void UnwireButtons()
        {
            if (!_wired)
                return;
            if (_selectMercButton != null)
                _selectMercButton.OnClick -= OnSelectMerc;
            if (_mercUnblockedButton != null)
                _mercUnblockedButton.OnClick -= OnSelectMerc;
            if (_selectClassButton != null)
                _selectClassButton.OnClick -= OnSelectClass;
            if (_selectEquipmentButton != null)
                _selectEquipmentButton.OnClick -= OnSelectEquipment;
            if (_selectImplantsButton != null)
                _selectImplantsButton.OnClick -= OnSelectImplants;
            _wired = false;
        }

        private void OnSelectMerc(CommonButton btn, int clicks)
        {
            SquadSelectionState.ActiveSelectSlot = Index;
            UI.Chain<SelectMercenaryScreen>().Invoke(v =>
            {
                v.Configure(SelectMercenaryScreen.Filter.SelectForMission, OnMercPicked);
            }).HideAll().Show().Fallback<PrepareRaidScreen>();
        }

        private void OnMercPicked(Mercenary merc)
        {
            var leader = Traverse.Create(_screen).Field<Mercenary>("_mercenary").Value;
            if (merc != null && SquadSelectionState.IsOccupied(merc.ProfileId, leader?.ProfileId)
                && (Mercenary == null || Mercenary.ProfileId != merc.ProfileId))
            {
                Plugin.Logger.Log("Ally slot rejected already-selected merc " + merc.ProfileId);
                SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(
                    SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
                SquadSelectionState.ActiveSelectSlot = -1;
                return;
            }

            SetMercenary(merc);
            SquadSelectionState.ActiveSelectSlot = -1;
            UI.Back();
            PrepareRaidSquadController.Instance?.SyncAfterSlotChange();
        }

        private void OnSelectClass(CommonButton btn, int clicks)
        {
            if (Mercenary == null)
                return;
            UI.Chain<SelectClassScreen>().HideAll().Invoke(v => v.Configure(Mercenary))
                .Show().Fallback<PrepareRaidScreen>();
        }

        private void OnSelectEquipment(CommonButton btn, int clicks)
        {
            if (Mercenary == null)
                return;
            UI.Chain<ArsenalScreen>().Invoke(v => v.Configure(Mercenary, showShuttle: true))
                .HideAll().Show().Fallback<PrepareRaidScreen>();
        }

        private void OnSelectImplants(CommonButton btn, int clicks)
        {
            if (Mercenary == null)
                return;
            UI.Chain<AugmentationScreen>().Invoke(v => v.Configure(Mercenary))
                .HideAll().Show().Fallback<PrepareRaidScreen>();
        }

        private static void SafeSetActive(Component c, bool active)
        {
            if (c != null)
                c.gameObject.SetActive(active);
        }

        private static void SafeSetActive(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }
    }
}
