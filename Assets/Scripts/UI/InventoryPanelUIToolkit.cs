using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// Skill equip screen (UI V2). Scrim fades + sheet pops in, slots and cards cascade in,
    /// the slot the picked skill goes in pulses, equipping punches the slot. Cards and slots
    /// carry the RIASEC kind colour (vc_UITheme.Riasec) and silver/gold Lv badges.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryPanelUIToolkit : MonoBehaviour
    {
        private VisualElement _overlay;
        private VisualElement _panel;
        private VisualElement _slotsGrid;
        private ScrollView    _cardsScroll;
        private bool          _open;
        private vc_UIAnim.Handle _readyPulse;
        private VisualElement    _readySlot;
        private readonly Label[] _slotLv = new Label[vc_SkillManager.SlotCount];

        private readonly VisualElement[] _slotIcons   = new VisualElement[vc_SkillManager.SlotCount];
        private readonly VisualElement[] _slotFrames  = new VisualElement[vc_SkillManager.SlotCount];
        private readonly Label[]         _slotEmpties = new Label[vc_SkillManager.SlotCount];
        private readonly Label[]         _slotNames   = new Label[vc_SkillManager.SlotCount];

        // Detail pane elements
        private VisualElement _detailEmpty;
        private VisualElement _detailFilled;
        private VisualElement _detailIcon;
        private Label         _detailName;
        private VisualElement _detailTag;
        private Label         _detailTagLetter;
        private Label         _detailTagName;
        private Label         _detailDesc;
        private Label         _detailAction;

        private vc_SkillData      _selectedSkill;
        private VisualElement     _selectedCard;
        private vc_CheatUnlockAll _cheat;

        // Assign any GameObjects you want hidden while inventory is open (e.g. the GameHUD UIDocument GameObject)
        [SerializeField] private GameObject[] _hideWhenOpen;

        private bool _inventoryEventBound;

        private static readonly StyleBackground NoBackground =
            new StyleBackground(StyleKeyword.None);

        private static readonly Dictionary<string, string> RiasecNames = new()
        {
            { "r", "Realistic" },
            { "i", "Investigative" },
            { "a", "Artistic" },
            { "s", "Social" },
            { "e", "Enterprising" },
            { "c", "Conventional" }
        };

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _overlay     = root.Q<VisualElement>("overlay");
            _panel       = root.Q<VisualElement>("inv-panel");
            _slotsGrid   = root.Q<VisualElement>("inv-slots");
            _cardsScroll = root.Q<ScrollView>("cards-scroll");

            // Detail pane
            _detailEmpty     = root.Q<VisualElement>("detail-empty");
            _detailFilled    = root.Q<VisualElement>("detail-filled");
            _detailIcon      = root.Q<VisualElement>("detail-icon");
            _detailName      = root.Q<Label>("detail-name");
            _detailTag       = root.Q<VisualElement>("detail-tag");
            _detailTagLetter = root.Q<Label>("detail-tag-letter");
            _detailTagName   = root.Q<Label>("detail-tag-name");
            _detailDesc      = root.Q<Label>("detail-desc");
            _detailAction    = root.Q<Label>("detail-action");

            root.Q<Button>("btn-back")
                ?.RegisterCallback<ClickEvent>(_ => Hide());

            // "Add all" cheat: hidden for now (kept wired so it can be shown again by
            // removing the display line).
            Button cheatAll = root.Q<Button>("btn-cheat-all");
            cheatAll?.RegisterCallback<ClickEvent>(_ => OnCheatClicked());
            if (cheatAll != null) cheatAll.style.display = DisplayStyle.None;

            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                int idx = i;
                _slotFrames[i]  = root.Q<Button>($"slot-{i}");
                _slotIcons[i]   = root.Q<VisualElement>($"slot-icon-{i}");
                _slotEmpties[i] = root.Q<Label>($"slot-empty-{i}");
                _slotNames[i]   = root.Q<Label>($"slot-name-{i}");
                _slotFrames[i]?.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

                // kind ring + a Lv badge holder on each slot
                if (_slotFrames[i] != null)
                {
                    vc_UITheme.Border(_slotFrames[i], 4f, vc_UITheme.Riasec(vc_SkillManager.CategoryLetterForSlot(i)));
                    _slotFrames[i].style.borderBottomWidth = 7f;
                    if (_slotLv[i] == null && _slotFrames[i].parent != null)
                    {
                        _slotLv[i] = new Label { name = $"slot-lv-{i}", pickingMode = PickingMode.Ignore };
                        _slotLv[i].AddToClassList("sn-badge");
                        _slotLv[i].AddToClassList("slot-lv");
                        _slotLv[i].style.display = DisplayStyle.None;
                        _slotFrames[i].parent.Add(_slotLv[i]);
                    }
                }
            }

            vc_UIAnim.PressAll(root);
            HideNow();      // start closed, no animation

            vc_PlayerOptions.Changed += OnOptionsChanged;
        }

        private void Start()
        {
            _cheat = Object.FindFirstObjectByType<vc_CheatUnlockAll>(FindObjectsInactive.Include);
            TryBindInventoryEvent();
        }

        private void OnDisable()
        {
            vc_PlayerOptions.Changed -= OnOptionsChanged;

            if (_inventoryEventBound && vc_PlayerInventory.Instance != null)
            {
                vc_PlayerInventory.Instance.OnSkillAdded -= OnSkillAdded;
                _inventoryEventBound = false;
            }
        }

        private void TryBindInventoryEvent()
        {
            if (_inventoryEventBound || vc_PlayerInventory.Instance == null) return;
            vc_PlayerInventory.Instance.OnSkillAdded += OnSkillAdded;
            _inventoryEventBound = true;
        }

        public void Show()
        {
            TryBindInventoryEvent();
            bool wasOpen = _open;
            _open = true;
            ClearSelection();
            PopulateCards();
            RefreshSlots();
            SetSlotsReady(false);
            SetHideTargets(false);

            if (!wasOpen)
            {
                vc_UIAnim.Show(_overlay, vc_UIAnim.Enter.Fade);
                vc_UIAnim.Show(_panel, vc_UIAnim.Enter.Pop, 0.03f);
                if (_slotsGrid != null)
                {
                    var wrappers = new List<VisualElement>();
                    foreach (VisualElement row in _slotsGrid.Children())
                        foreach (VisualElement w in row.Children()) wrappers.Add(w);
                    vc_UIAnim.Stagger(wrappers, vc_UIAnim.Enter.Pop, 0.04f, 0.12f);
                }
                StaggerCards(0.3f);
            }
            else
            {
                _overlay.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            if (!_open) { HideNow(); return; }
            _open = false;
            // sheet shrinks out, scrim fades with it
            AnimHide(_panel, vc_UIAnim.Exit.Shrink);
            AnimHide(_overlay, vc_UIAnim.Exit.Fade);
            ClearSelection();
            SetSlotsReady(false);
            SetHideTargets(true);
        }

        /// <summary>Instant close (scene start / disable): no animation.</summary>
        private void HideNow()
        {
            _open = false;
            SnapHidden(_overlay);
            if (_panel != null) { vc_UIAnim.StopAll(_panel); ClearInline(_panel); _panel.style.display = DisplayStyle.Flex; }
            ClearSelection();
            SetSlotsReady(false);
            SetHideTargets(true);
        }

        private static void AnimHide(VisualElement ve, vc_UIAnim.Exit kind)
        {
            if (ve == null) return;
            if (ve.resolvedStyle.display == DisplayStyle.None) { SnapHidden(ve); return; }
            vc_UIAnim.Hide(ve, null, kind);
        }

        private static void SnapHidden(VisualElement ve)
        {
            if (ve == null) return;
            vc_UIAnim.StopAll(ve);
            ClearInline(ve);
            ve.style.display = DisplayStyle.None;
        }

        private static void ClearInline(VisualElement ve)
        {
            ve.style.opacity = StyleKeyword.Null;
            ve.style.scale = StyleKeyword.Null;
            ve.style.translate = StyleKeyword.Null;
        }

        private void StaggerCards(float delay)
        {
            if (_cardsScroll == null) return;
            vc_UIAnim.Stagger(_cardsScroll.contentContainer.Children(), vc_UIAnim.Enter.SlideUp, 0.03f, delay);
        }

        private void SetHideTargets(bool active)
        {
            if (_hideWhenOpen == null) return;
            foreach (var go in _hideWhenOpen)
                if (go != null) go.SetActive(active);
        }

        private void ClearSelection()
        {
            _selectedSkill = null;
            _selectedCard  = null;
            ShowEmptyDetail();
        }

        private void ShowEmptyDetail()
        {
            bool wasHidden = _detailEmpty != null && _detailEmpty.style.display == DisplayStyle.None;
            if (_detailFilled != null) { vc_UIAnim.StopAll(_detailFilled); ClearInline(_detailFilled); _detailFilled.style.display = DisplayStyle.None; }
            if (_detailEmpty == null) return;
            if (wasHidden && _open) vc_UIAnim.Show(_detailEmpty, vc_UIAnim.Enter.Fade);
            else _detailEmpty.style.display = DisplayStyle.Flex;
        }

        private void PopulateCards()
        {
            if (_cardsScroll == null) return;
            _cardsScroll.contentContainer.Clear();

            var skills = vc_PlayerInventory.Instance?.GatheredSkills;
            if (skills == null) return;

            foreach (var skill in skills)
            {
                if (skill == null) continue;
                _cardsScroll.Add(CreateRow(skill));
            }
        }

        private VisualElement CreateRow(vc_SkillData skill)
        {
            var row = new VisualElement();
            row.AddToClassList("skill-row");

            string letter = (skill.riaSecLetter ?? "?").ToUpper();
            bool riasec = letter.Length == 1 && "RIASEC".IndexOf(letter[0]) >= 0;
            Color kind = riasec ? vc_UITheme.Riasec(letter) : vc_UITheme.Brown;

            // kind strip along the top
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.AddToClassList("row-strip");
            strip.style.backgroundColor = kind;
            row.Add(strip);

            // Icon disc, ringed in the kind colour
            var iconFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            iconFrame.AddToClassList("row-icon-frame");
            vc_UITheme.Border(iconFrame, 3f, kind);
            iconFrame.style.borderBottomWidth = 5f;
            var icon = new VisualElement();
            icon.AddToClassList("row-icon");
            icon.style.backgroundImage = vc_RiasecSymbols.Background(skill.riaSecLetter);
            iconFrame.Add(icon);
            row.Add(iconFrame);

            // Text block
            var textBlock = new VisualElement();
            textBlock.AddToClassList("row-textblock");

            int lv = vc_SkillLevels.Level(skill);
            var nameLabel = new Label(skill.DisplayName);
            nameLabel.AddToClassList("row-name");
            textBlock.Add(nameLabel);

            string riasecName = RiasecNames.TryGetValue(letter.ToLower(), out var n) ? n : "-";
            var metaLabel = new Label(riasecName);
            metaLabel.AddToClassList("row-meta");
            textBlock.Add(metaLabel);

            row.Add(textBlock);

            // RIASEC tag square
            var tag = new VisualElement();
            tag.AddToClassList("row-tag");
            tag.AddToClassList($"riasec-{letter.ToLower()}");
            var tagLetter = new Label(letter);
            tagLetter.AddToClassList("row-tag-letter");
            tag.Add(tagLetter);
            row.Add(tag);

            // Lv badge: silver Lv2, gold Lv3
            if (lv > 1)
            {
                var lvBadge = new Label($"Lv{lv}") { pickingMode = PickingMode.Ignore };
                lvBadge.AddToClassList("sn-badge");
                lvBadge.AddToClassList(lv >= vc_SkillLevels.MaxLevel ? "sn-badge--gold" : "sn-badge--silver");
                lvBadge.AddToClassList("row-lv");
                row.Add(lvBadge);
            }

            row.RegisterCallback<ClickEvent>(_ => OnRowClicked(row, skill));
            vc_UIAnim.Press(row, 0.94f);
            return row;
        }

        private void OnRowClicked(VisualElement row, vc_SkillData skill)
        {
            if (_selectedCard != null)
                _selectedCard.RemoveFromClassList("selected");

            _selectedCard  = row;
            _selectedSkill = skill;
            row.AddToClassList("selected");

            PopulateDetail(skill);
            SetCategorySlotReady(skill);
        }

        private static void Punch(VisualElement ve, float amount = 0.18f) => vc_UIAnim.Punch(ve, amount);

        private void PopulateDetail(vc_SkillData skill)
        {
            if (_detailEmpty != null) { vc_UIAnim.StopAll(_detailEmpty); ClearInline(_detailEmpty); _detailEmpty.style.display = DisplayStyle.None; }
            if (_detailFilled != null)
            {
                if (_open) vc_UIAnim.Show(_detailFilled, vc_UIAnim.Enter.SlideUp, 0f, null, 0.22f);
                else _detailFilled.style.display = DisplayStyle.Flex;
            }

            if (_detailIcon != null)
                _detailIcon.style.backgroundImage = vc_RiasecSymbols.Background(skill.riaSecLetter);

            // ring the detail disc in the kind colour
            VisualElement detailFrame = _detailIcon?.parent;
            if (detailFrame != null)
            {
                string l = (skill.riaSecLetter ?? "").Trim().ToUpperInvariant();
                bool known = l.Length == 1 && "RIASEC".IndexOf(l[0]) >= 0;
                vc_UITheme.Border(detailFrame, 4f, known ? vc_UITheme.Riasec(l) : vc_UITheme.Brown);
                detailFrame.style.borderBottomWidth = 7f;
            }

            if (_detailName != null)
                _detailName.text = skill.DisplayName ?? "Unnamed";

            string letter = (skill.riaSecLetter ?? "?").ToUpper();
            string letterLower = letter.ToLower();
            string riasecName = RiasecNames.TryGetValue(letterLower, out var n) ? n : "-";

            if (_detailTagLetter != null) _detailTagLetter.text = letter;
            if (_detailTagName != null)   _detailTagName.text   = riasecName;

            if (_detailTag != null)
            {
                foreach (var k in new[] { "r","i","a","s","e","c" })
                    _detailTag.RemoveFromClassList($"riasec-{k}");
                _detailTag.AddToClassList($"riasec-{letterLower}");
            }

            if (_detailDesc != null)   _detailDesc.text   = (string.IsNullOrEmpty(skill.description) ? "-" : skill.description) + LevelLine(skill);
            if (_detailAction != null) _detailAction.text = string.IsNullOrEmpty(skill.DisplayName) ? "-" : skill.DisplayName;
        }

        /// <summary>Skill system v2: the skill's level and XP to the next one.</summary>
        private static string LevelLine(vc_SkillData skill)
        {
            int lv = vc_SkillLevels.Level(skill), next = vc_SkillLevels.XpToNext(skill);
            return lv >= vc_SkillLevels.MaxLevel ? $"\nLv{lv} (max)" : $"\nLv{lv} - {vc_SkillLevels.Xp(skill)} XP, {next} to Lv{lv + 1}";
        }

        private void OnSlotClicked(int slotIndex)
        {
            Debug.Log($"[Inventory] OnSlotClicked({slotIndex}) - selectedSkill={_selectedSkill?.skillName ?? "NULL"}, SkillManager={(vc_SkillManager.Instance == null ? "NULL" : "OK")}");
            if (_selectedSkill == null || vc_SkillManager.Instance == null) return;

            if (vc_SkillManager.Instance.GetSkillInSlot(slotIndex) == _selectedSkill)
            {
                vc_DialogPanel.Instance?.ShowMessage("Equipped",
                    "Already in this slot.", null);
                return;
            }

            int homeSlot = vc_SkillManager.SlotIndexForLetter(_selectedSkill.riaSecLetter);
            if (homeSlot != slotIndex)
            {
                string categoryName = vc_SkillManager.CategoryNameForSlot(homeSlot);
                vc_FloatingMessage.Instance?.Show($"Goes in the {categoryName} slot.");
                if (slotIndex >= 0 && slotIndex < _slotFrames.Length) vc_UIAnim.Shake(_slotFrames[slotIndex]);
                return;
            }

            vc_SkillManager.Instance.AssignSkillToSlot(slotIndex, _selectedSkill);

            _selectedCard?.RemoveFromClassList("selected");
            _selectedCard  = null;
            _selectedSkill = null;
            ShowEmptyDetail();

            RefreshSlots();
            SetSlotsReady(false);

            // equip feedback: the slot bounces
            if (_slotFrames[slotIndex] != null) Punch(_slotFrames[slotIndex], 0.2f);
            if (_slotIcons[slotIndex] != null) Punch(_slotIcons[slotIndex], 0.3f);
        }

        private void RefreshSlots()
        {
            if (vc_SkillManager.Instance == null) return;

            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                var data = vc_SkillManager.Instance.GetSkillInSlot(i);

                if (_slotIcons[i] != null)
                    _slotIcons[i].style.backgroundImage = data != null
                        ? vc_RiasecSymbols.Background(data.riaSecLetter)
                        : NoBackground;

                if (_slotEmpties[i] != null)
                    _slotEmpties[i].style.display =
                        data == null ? DisplayStyle.Flex : DisplayStyle.None;

                if (_slotNames[i] != null)
                    _slotNames[i].text = data != null
                        ? (data.DisplayName ?? "-")
                        : "-";

                if (_slotLv[i] != null)
                {
                    int lv = data != null ? vc_SkillLevels.Level(data) : 1;
                    _slotLv[i].text = $"Lv{lv}";
                    _slotLv[i].EnableInClassList("sn-badge--gold", lv >= vc_SkillLevels.MaxLevel);
                    _slotLv[i].EnableInClassList("sn-badge--silver", lv > 1 && lv < vc_SkillLevels.MaxLevel);
                    _slotLv[i].style.display = lv > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void SetSlotsReady(bool ready)
        {
            vc_UIAnim.StopPulse(ref _readyPulse, _readySlot);
            _readySlot = null;
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                if (_slotFrames[i] == null) continue;
                if (ready) _slotFrames[i].AddToClassList("ready");
                else       _slotFrames[i].RemoveFromClassList("ready");
            }
        }

        private void SetCategorySlotReady(vc_SkillData skill)
        {
            SetSlotsReady(false);

            int slotIndex = skill != null ? vc_SkillManager.SlotIndexForLetter(skill.riaSecLetter) : -1;
            if (slotIndex < 0 || slotIndex >= _slotFrames.Length || _slotFrames[slotIndex] == null) return;

            _slotFrames[slotIndex].AddToClassList("ready");
            _readySlot = _slotFrames[slotIndex];
            _readyPulse = vc_UIAnim.Pulse(_readySlot, 0.07f, 0.9f);
        }

        private void OnCheatClicked()
        {
            if (_cheat == null)
                _cheat = Object.FindFirstObjectByType<vc_CheatUnlockAll>(FindObjectsInactive.Include);

            _cheat?.UnlockAll();
            PopulateCards();
            StaggerCards(0f);
        }

        private void OnSkillAdded(vc_SkillData _)
        {
            if (_open)
                PopulateCards();
        }

        // Skill-name language may have switched: rebuild the visible names if the panel is open.
        private void OnOptionsChanged()
        {
            if (_overlay == null || !_open) return;

            var selected = _selectedSkill;
            PopulateCards();
            RefreshSlots();
            if (selected != null) PopulateDetail(selected);
        }
    }
}
