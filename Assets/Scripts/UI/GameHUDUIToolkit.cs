using UnityEngine;
using UnityEngine.UIElements;
using UGUIButton = UnityEngine.UI.Button;
using UGUIImage  = UnityEngine.UI.Image;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDUIToolkit : MonoBehaviour
    {
        private readonly Button[]        _slotBtns   = new Button[4];
        private readonly VisualElement[] _slotIcons  = new VisualElement[4];
        private readonly Label[]         _slotLevels = new Label[4];
        private readonly Label[]         _slotNames  = new Label[4];
        private readonly VisualElement[] _previews   = new VisualElement[6];

        private readonly vc_SkillData[] _cachedSlots = new vc_SkillData[4];
        private readonly UGUIButton[]   _uguiBtns    = new UGUIButton[4];

        private InventoryPanelUIToolkit _inventoryPanel;
        private bool _inventoryEventBound;

        private static readonly StyleBackground NoBackground =
            new StyleBackground(StyleKeyword.None);

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _slotBtns[i]   = root.Q<Button>($"slot-btn-{i}");
                _slotIcons[i]  = root.Q<VisualElement>($"slot-icon-{i}");
                _slotLevels[i] = root.Q<Label>($"slot-level-{i}");
                _slotNames[i]  = root.Q<Label>($"slot-name-{i}");

                _slotBtns[i]?.RegisterCallback<ClickEvent>(_       => OnSlotClicked(idx));
                _slotBtns[i]?.RegisterCallback<PointerDownEvent>(_ => OnSlotPointerDown(idx));
                _slotBtns[i]?.RegisterCallback<PointerUpEvent>(_   => OnSlotPointerUp(idx));
            }

            for (int i = 0; i < 6; i++)
                _previews[i] = root.Q<VisualElement>($"preview-{i}");

            root.Q<Button>("btn-open-inventory")
                ?.RegisterCallback<ClickEvent>(_ => _inventoryPanel?.Show());
        }

        private void Start()
        {
            _inventoryPanel = Object.FindFirstObjectByType<InventoryPanelUIToolkit>();

            TryBindInventoryEvent();
            HideUGUISlotVisuals();
            RefreshAllSlots();
            RefreshPreviewCircles();
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

        private void Update()
        {
            TryBindInventoryEvent();

            if (vc_SkillManager.Instance == null) return;

            for (int i = 0; i < 4; i++)
            {
                var current = vc_SkillManager.Instance.GetSkillInSlot(i);
                if (current != _cachedSlots[i])
                {
                    _cachedSlots[i] = current;
                    RefreshSlot(i, current);
                }

                if (_slotBtns[i] != null && _uguiBtns[i] != null)
                    _slotBtns[i].SetEnabled(_uguiBtns[i].interactable);
            }
        }

        private void HideUGUISlotVisuals()
        {
            if (vc_SkillManager.Instance == null) return;

            for (int i = 0; i < 4; i++)
            {
                var slot = vc_SkillManager.Instance.GetSkillSlot(i);
                if (slot == null) continue;

                var btn = slot.GetComponent<UGUIButton>();
                if (btn != null)
                {
                    btn.interactable = false;
                    _uguiBtns[i] = btn;
                }

                foreach (var img in slot.GetComponentsInChildren<UGUIImage>(true))
                    img.enabled = false;

                foreach (var tmp in slot.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    tmp.enabled = false;
            }
        }

        private void RefreshAllSlots()
        {
            if (vc_SkillManager.Instance == null) return;
            for (int i = 0; i < 4; i++)
            {
                var data = vc_SkillManager.Instance.GetSkillInSlot(i);
                _cachedSlots[i] = data;
                RefreshSlot(i, data);
            }
        }

        private void RefreshSlot(int i, vc_SkillData data)
        {
            if (_slotIcons[i] != null)
            {
                _slotIcons[i].style.backgroundImage = data?.icon != null
                    ? new StyleBackground(data.icon)
                    : NoBackground;
            }

            if (_slotLevels[i] != null)
                _slotLevels[i].text = data != null ? $"Lv.{data.level}" : string.Empty;

            if (_slotNames[i] != null)
                _slotNames[i].text = data != null ? data.skillName : string.Empty;
        }

        private void RefreshPreviewCircles()
        {
            if (vc_PlayerInventory.Instance == null) return;
            var skills = vc_PlayerInventory.Instance.GatheredSkills;

            for (int i = 0; i < 6; i++)
            {
                if (_previews[i] == null) continue;
                bool hasSkill = i < skills.Count;
                _previews[i].style.display = hasSkill ? DisplayStyle.Flex : DisplayStyle.None;
                _previews[i].style.backgroundImage = hasSkill && skills[i]?.icon != null
                    ? new StyleBackground(skills[i].icon)
                    : NoBackground;
            }
        }

        private void OnSlotClicked(int i)      => vc_SkillManager.Instance?.GetSkillSlot(i)?.Activate();
        private void OnSlotPointerDown(int i)  => vc_SkillManager.Instance?.GetSkillSlot(i)?.SetHeld(true);
        private void OnSlotPointerUp(int i)    => vc_SkillManager.Instance?.GetSkillSlot(i)?.SetHeld(false);
        private void OnSkillAdded(vc_SkillData _) => RefreshPreviewCircles();
    }
}
