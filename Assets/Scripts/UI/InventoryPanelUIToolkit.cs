using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class InventoryPanelUIToolkit : MonoBehaviour
    {
        private VisualElement _overlay;
        private ScrollView    _cardsScroll;

        private readonly VisualElement[] _slotIcons   = new VisualElement[4];
        private readonly VisualElement[] _slotFrames  = new VisualElement[4];
        private readonly Label[]         _slotEmpties = new Label[4];

        // Detail pane elements
        private VisualElement _detailEmpty;
        private VisualElement _detailFilled;
        private VisualElement _detailIcon;
        private Label         _detailName;
        private VisualElement _detailTag;
        private Label         _detailTagLetter;
        private Label         _detailTagName;
        private Label         _detailLevel;
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
            { "r", "REALISTIC" },
            { "i", "INVESTIGATIVE" },
            { "a", "ARTISTIC" },
            { "s", "SOCIAL" },
            { "e", "ENTERPRISING" },
            { "c", "CONVENTIONAL" }
        };

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _overlay     = root.Q<VisualElement>("overlay");
            _cardsScroll = root.Q<ScrollView>("cards-scroll");

            // Detail pane
            _detailEmpty     = root.Q<VisualElement>("detail-empty");
            _detailFilled    = root.Q<VisualElement>("detail-filled");
            _detailIcon      = root.Q<VisualElement>("detail-icon");
            _detailName      = root.Q<Label>("detail-name");
            _detailTag       = root.Q<VisualElement>("detail-tag");
            _detailTagLetter = root.Q<Label>("detail-tag-letter");
            _detailTagName   = root.Q<Label>("detail-tag-name");
            _detailLevel     = root.Q<Label>("detail-level");
            _detailDesc      = root.Q<Label>("detail-desc");
            _detailAction    = root.Q<Label>("detail-action");

            root.Q<Button>("btn-back")
                ?.RegisterCallback<ClickEvent>(_ => Hide());

            root.Q<Button>("btn-cheat-all")
                ?.RegisterCallback<ClickEvent>(_ => OnCheatClicked());

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _slotFrames[i]  = root.Q<Button>($"slot-{i}");
                _slotIcons[i]   = root.Q<VisualElement>($"slot-icon-{i}");
                _slotEmpties[i] = root.Q<Label>($"slot-empty-{i}");
                _slotFrames[i]?.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));
            }

            Hide();
        }

        private void Start()
        {
            _cheat = Object.FindFirstObjectByType<vc_CheatUnlockAll>(FindObjectsInactive.Include);
            TryBindInventoryEvent();
        }

        private void OnDisable()
        {
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
            _overlay.style.display = DisplayStyle.Flex;
            ClearSelection();
            PopulateCards();
            RefreshSlots();
            SetSlotsReady(false);
            SetHideTargets(false);
        }

        public void Hide()
        {
            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;
            ClearSelection();
            SetHideTargets(true);
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
            if (_detailEmpty != null)  _detailEmpty.style.display  = DisplayStyle.Flex;
            if (_detailFilled != null) _detailFilled.style.display = DisplayStyle.None;
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

            // Icon frame
            var iconFrame = new VisualElement();
            iconFrame.AddToClassList("row-icon-frame");
            var icon = new VisualElement();
            icon.AddToClassList("row-icon");
            if (skill.icon != null)
                icon.style.backgroundImage = new StyleBackground(skill.icon);
            iconFrame.Add(icon);
            row.Add(iconFrame);

            // Text block
            var textBlock = new VisualElement();
            textBlock.AddToClassList("row-textblock");

            var nameLabel = new Label(skill.skillName);
            nameLabel.AddToClassList("row-name");
            textBlock.Add(nameLabel);

            string letter = (skill.riaSecLetter ?? "?").ToUpper();
            string riasecName = RiasecNames.TryGetValue(letter.ToLower(), out var n) ? n : "—";
            var metaLabel = new Label($"{riasecName}  -  LV. {skill.level}");
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

            row.RegisterCallback<ClickEvent>(_ => OnRowClicked(row, skill));
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
            SetSlotsReady(true);
        }

        private void PopulateDetail(vc_SkillData skill)
        {
            if (_detailEmpty != null)  _detailEmpty.style.display  = DisplayStyle.None;
            if (_detailFilled != null) _detailFilled.style.display = DisplayStyle.Flex;

            if (_detailIcon != null)
                _detailIcon.style.backgroundImage = skill.icon != null
                    ? new StyleBackground(skill.icon)
                    : NoBackground;

            if (_detailName != null)
                _detailName.text = (skill.skillName ?? "UNNAMED").ToUpper();

            string letter = (skill.riaSecLetter ?? "?").ToUpper();
            string letterLower = letter.ToLower();
            string riasecName = RiasecNames.TryGetValue(letterLower, out var n) ? n : "—";

            if (_detailTagLetter != null) _detailTagLetter.text = letter;
            if (_detailTagName != null)   _detailTagName.text   = riasecName;

            if (_detailTag != null)
            {
                foreach (var k in new[] { "r","i","a","s","e","c" })
                    _detailTag.RemoveFromClassList($"riasec-{k}");
                _detailTag.AddToClassList($"riasec-{letterLower}");
            }

            if (_detailLevel != null)  _detailLevel.text  = $"LV. {skill.level}";
            if (_detailDesc != null)   _detailDesc.text   = string.IsNullOrEmpty(skill.description) ? "No description available." : skill.description;
            if (_detailAction != null) _detailAction.text = string.IsNullOrEmpty(skill.buttonLabel) ? "—" : skill.buttonLabel;
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_selectedSkill == null || vc_SkillManager.Instance == null) return;

            vc_SkillManager.Instance.AssignSkillToSlot(slotIndex, _selectedSkill);

            _selectedCard?.RemoveFromClassList("selected");
            _selectedCard  = null;
            _selectedSkill = null;
            ShowEmptyDetail();

            RefreshSlots();
            SetSlotsReady(false);
        }

        private void RefreshSlots()
        {
            if (vc_SkillManager.Instance == null) return;

            for (int i = 0; i < 4; i++)
            {
                var data = vc_SkillManager.Instance.GetSkillInSlot(i);

                if (_slotIcons[i] != null)
                    _slotIcons[i].style.backgroundImage = data?.icon != null
                        ? new StyleBackground(data.icon)
                        : NoBackground;

                if (_slotEmpties[i] != null)
                    _slotEmpties[i].style.display =
                        data == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetSlotsReady(bool ready)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_slotFrames[i] == null) continue;
                if (ready) _slotFrames[i].AddToClassList("ready");
                else       _slotFrames[i].RemoveFromClassList("ready");
            }
        }

        private void OnCheatClicked()
        {
            if (_cheat == null)
                _cheat = Object.FindFirstObjectByType<vc_CheatUnlockAll>(FindObjectsInactive.Include);

            _cheat?.UnlockAll();
            PopulateCards();
        }

        private void OnSkillAdded(vc_SkillData _)
        {
            if (_overlay?.style.display == DisplayStyle.Flex)
                PopulateCards();
        }
    }
}
