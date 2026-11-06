using UnityEngine;
using UnityEngine.UIElements;
using UGUIButton = UnityEngine.UI.Button;
using UGUIImage  = UnityEngine.UI.Image;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDUIToolkit : MonoBehaviour
    {
        [SerializeField] private GameObject _uguiSkillBarRoot;

        private readonly Button[]        _slotBtns       = new Button[vc_SkillManager.SlotCount];
        private readonly VisualElement[] _slotIcons      = new VisualElement[vc_SkillManager.SlotCount];
        private readonly Label[]         _slotNames      = new Label[vc_SkillManager.SlotCount];
        private readonly Label[]         _slotEmpties    = new Label[vc_SkillManager.SlotCount];
        private readonly Label[]         _slotCategories = new Label[vc_SkillManager.SlotCount];
        private readonly VisualElement[] _previews       = new VisualElement[6];
        private readonly VisualElement[] _cooldownShade  = new VisualElement[vc_SkillManager.SlotCount];
        private readonly Label[]         _cooldownLabel  = new Label[vc_SkillManager.SlotCount];

        private readonly vc_SkillData[] _cachedSlots = new vc_SkillData[vc_SkillManager.SlotCount];

        private InventoryPanelUIToolkit _inventoryPanel;
        private int _previewSkillCount = -1;
        private bool _inventoryEventBound;

        private static readonly StyleBackground NoBackground =
            new StyleBackground(StyleKeyword.None);

        private void OnEnable()
        {
            // Disable only the UGUI skill slot children - not the whole root,
            // because Controls UI also hosts the Joystick which must stay active.
            if (_uguiSkillBarRoot != null)
            {
                for (int i = 0; i < _uguiSkillBarRoot.transform.childCount; i++)
                {
                    Transform child = _uguiSkillBarRoot.transform.GetChild(i);
                    if (child.name.StartsWith("Skill", System.StringComparison.OrdinalIgnoreCase))
                        child.gameObject.SetActive(false);
                }
            }

            var root = GetComponent<UIDocument>().rootVisualElement;

            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                int idx = i;
                _slotBtns[i]      = root.Q<Button>($"slot-btn-{i}");
                _slotIcons[i]     = root.Q<VisualElement>($"slot-icon-{i}");
                _slotNames[i]     = root.Q<Label>($"slot-name-{i}");
                _slotEmpties[i]   = root.Q<Label>($"slot-empty-{i}");
                _slotCategories[i] = root.Q<Label>($"slot-category-{i}");

                if (_slotCategories[i] != null)
                    _slotCategories[i].text = vc_SkillManager.CategoryNameForSlot(i);

                if (_slotBtns[i] != null)
                    _slotBtns[i].AddToClassList($"cat-{vc_SkillManager.CategoryLetterForSlot(i)}");

                // Skill buttons show their RIASEC category symbol (placeholder for per-skill art).
                if (_slotIcons[i] != null)
                    _slotIcons[i].style.backgroundImage =
                        vc_RiasecSymbols.Background(vc_SkillManager.CategoryLetterForSlot(i));

                // Cooldown shade (drains downward) + seconds left, built in code.
                if (_slotBtns[i] != null && _cooldownShade[i] == null)
                {
                    _cooldownShade[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                    _cooldownShade[i].AddToClassList("slot-cooldown");
                    _cooldownLabel[i] = new Label("") { pickingMode = PickingMode.Ignore };
                    _cooldownLabel[i].AddToClassList("slot-cooldown-label");
                    _slotBtns[i].Add(_cooldownShade[i]);
                    _slotBtns[i].Add(_cooldownLabel[i]);
                }

                _slotBtns[i]?.RegisterCallback<ClickEvent>(_       => OnSlotClicked(idx));
                _slotBtns[i]?.RegisterCallback<PointerDownEvent>(_ => OnSlotPointerDown(idx));
                _slotBtns[i]?.RegisterCallback<PointerUpEvent>(_   => OnSlotPointerUp(idx));
            }

            for (int i = 0; i < 6; i++)
                _previews[i] = root.Q<VisualElement>($"preview-{i}");

            _inventoryBtn = root.Q<Button>("btn-open-inventory");
            _inventoryBtn?.RegisterCallback<ClickEvent>(_ => _inventoryPanel?.Show());

            vc_PlayerOptions.Changed += OnOptionsChanged;
            vc_SkillLevels.Changed += OnSkillLevelsChanged;
            vc_SkillLevels.LevelUp += OnSkillLevelUp;
        }

        private void Start()
        {
            _inventoryPanel = Object.FindFirstObjectByType<InventoryPanelUIToolkit>();

            TryBindInventoryEvent();
            RefreshPreviewCircles();
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            vc_PlayerOptions.Changed -= OnOptionsChanged;
            vc_SkillLevels.Changed -= OnSkillLevelsChanged;
            vc_SkillLevels.LevelUp -= OnSkillLevelUp;

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

            // Starting skills can be granted without OnSkillAdded; keep the preview in sync.
            int gathered = vc_PlayerInventory.Instance != null ? vc_PlayerInventory.Instance.GatheredSkills.Count : 0;
            if (gathered != _previewSkillCount) RefreshPreviewCircles();

            var mgr = vc_SkillManager.Instance;
            if (mgr == null) return;

            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                var current = mgr.GetSkillInSlot(i);
                if (current != _cachedSlots[i])
                {
                    _cachedSlots[i] = current;
                    RefreshSlot(i, current);
                }

                if (_slotBtns[i] != null)
                {
                    var uguiSlot = mgr.GetSkillSlot(i);
                    var uguiBtn  = uguiSlot?.GetComponent<UGUIButton>();
                    if (uguiBtn != null)
                        _slotBtns[i].SetEnabled(uguiBtn.interactable);
                }

                float frac = mgr.CooldownFraction(i);
                bool cooling = frac > 0f && current != null;
                if (_cooldownShade[i] != null)
                {
                    _cooldownShade[i].style.display = cooling ? DisplayStyle.Flex : DisplayStyle.None;
                    _cooldownShade[i].style.height = Length.Percent(frac * 100f);
                }
                if (_cooldownLabel[i] != null)
                {
                    float left = mgr.CooldownRemaining(i);
                    // seconds only on the long rest; the short shared lockout is just a quick shade
                    _cooldownLabel[i].text = cooling && left >= 1.05f ? Mathf.CeilToInt(left).ToString() : "";
                }
                _slotBtns[i]?.EnableInClassList("skill-slot-btn--cooling", cooling);
            }

            UpdatePrepGlow();
        }

        private void RefreshAllSlots()
        {
            if (vc_SkillManager.Instance == null) return;
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                var data = vc_SkillManager.Instance.GetSkillInSlot(i);
                _cachedSlots[i] = data;
                RefreshSlot(i, data);
            }
        }

        private void RefreshSlot(int i, vc_SkillData data)
        {
            // Symbol stays on; an empty slot just shows it dimmed.
            _slotIcons[i]?.EnableInClassList("slot-icon--empty", data == null);

            if (_slotNames[i] != null)
                _slotNames[i].text = data != null ? (data.DisplayName ?? "-").ToUpper() : string.Empty;

            if (_slotEmpties[i] != null)
                _slotEmpties[i].style.display = DisplayStyle.None;

            // A remembered skill (carried over from the last loop) wears a small violet gem.
            if (_slotBtns[i] != null)
            {
                if (_memoryGems[i] == null)
                {
                    _memoryGems[i] = MakeMemoryGem();
                    _slotBtns[i].Add(_memoryGems[i]);
                }
                bool kept = vc_LoopMeta.IsKept(data);
                _memoryGems[i].style.display = kept ? DisplayStyle.Flex : DisplayStyle.None;
                // ...and a violet frame, so it reads at a glance as "carried over"
                _keptFrame[i] = kept;
                ApplyFrame(i, false);

                // Skill level: a silver pip at Lv2, a gold one at Lv3 (Lv1 shows nothing).
                if (_levelPips[i] == null)
                {
                    _levelPips[i] = MakeLevelPip();
                    _slotBtns[i].Add(_levelPips[i]);
                }
                SetLevelPip(_levelPips[i], data != null ? vc_SkillLevels.Level(data) : 1);
            }
        }

        // ── skill levels + prep glow (skill system v2) ──────────────────────

        private readonly Label[] _levelPips = new Label[vc_SkillManager.SlotCount];
        private readonly bool[] _keptFrame = new bool[vc_SkillManager.SlotCount];
        private readonly bool[] _glowing = new bool[vc_SkillManager.SlotCount];

        private static readonly Color Silver = new Color32(196, 206, 222, 255);
        private static readonly Color Gold = new Color32(255, 196, 40, 255);

        private static Label MakeLevelPip()
        {
            var pip = new Label { name = "level-pip", pickingMode = PickingMode.Ignore };
            pip.style.position = Position.Absolute;
            pip.style.top = 2;
            pip.style.left = 4;
            pip.style.fontSize = 14;
            pip.style.unityFontStyleAndWeight = FontStyle.Bold;
            pip.style.paddingLeft = 4; pip.style.paddingRight = 4;
            pip.style.paddingTop = 0; pip.style.paddingBottom = 0;
            Color edge = new Color32(60, 40, 30, 255);
            pip.style.color = edge;
            pip.style.borderTopColor = edge; pip.style.borderBottomColor = edge;
            pip.style.borderLeftColor = edge; pip.style.borderRightColor = edge;
            pip.style.borderTopWidth = 2; pip.style.borderBottomWidth = 2;
            pip.style.borderLeftWidth = 2; pip.style.borderRightWidth = 2;
            return pip;
        }

        private static void SetLevelPip(Label pip, int level)
        {
            if (pip == null) return;
            pip.style.display = level >= 2 ? DisplayStyle.Flex : DisplayStyle.None;
            pip.text = $"Lv{level}";
            pip.style.backgroundColor = level >= vc_SkillLevels.MaxLevel ? Gold : Silver;
        }

        private void ApplyFrame(int i, bool glow)
        {
            if (_slotBtns[i] == null) return;
            bool on = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
            StyleColor frame = glow ? new StyleColor(on ? Gold : (Color)new Color32(255, 120, 30, 255))
                : _keptFrame[i] ? new StyleColor((Color)new Color32(150, 110, 255, 255))
                : new StyleColor(StyleKeyword.Null);
            Button b = _slotBtns[i];
            b.style.borderTopColor = frame;
            b.style.borderLeftColor = frame;
            b.style.borderRightColor = frame;
            b.style.borderBottomColor = glow ? frame : new StyleColor(StyleKeyword.Null);
            // the unlocked skill: thick pulsing gold frame + a little bounce
            StyleFloat width = glow ? new StyleFloat(6f) : new StyleFloat(StyleKeyword.Null);
            b.style.borderTopWidth = width; b.style.borderBottomWidth = width;
            b.style.borderLeftWidth = width; b.style.borderRightWidth = width;
            b.style.scale = new Scale(glow && on ? new Vector2(1.08f, 1.08f) : Vector2.one);
        }

        /// <summary>The skill a prep made instant pulses gold until the quest ends.</summary>
        private void UpdatePrepGlow()
        {
            string tag = vc_FitQuestBase.PrepGlowTag;
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                bool glow = !string.IsNullOrEmpty(tag) && _cachedSlots[i] != null && _cachedSlots[i].HasTag(tag);
                if (glow || _glowing[i]) ApplyFrame(i, glow);
                _glowing[i] = glow;
            }
        }

        private void OnSkillLevelsChanged()
        {
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
                SetLevelPip(_levelPips[i], _cachedSlots[i] != null ? vc_SkillLevels.Level(_cachedSlots[i]) : 1);
        }

        private void OnSkillLevelUp(vc_SkillData skill, int from, int to)
        {
            SunodGame.Core.vc_Sfx.Play("skill_equip", 0.8f);
            vc_RunProgressUI.Instance?.ShowStamp($"{skill.DisplayName} Lv{to}!", to >= vc_SkillLevels.MaxLevel ? Gold : (Color)new Color32(120, 140, 170, 255), 1.1f);
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                if (_cachedSlots[i] != skill || _levelPips[i] == null) continue;
                Label pip = _levelPips[i];
                pip.style.scale = new Scale(new Vector2(1.8f, 1.8f));
                pip.schedule.Execute(() => pip.style.scale = new Scale(Vector2.one)).StartingIn(220);
            }
        }

        private readonly VisualElement[] _memoryGems = new VisualElement[vc_SkillManager.SlotCount];

        private static VisualElement MakeMemoryGem()
        {
            var gem = new VisualElement { name = "memory-gem", pickingMode = PickingMode.Ignore };
            gem.style.position = Position.Absolute;
            gem.style.top = 4;
            gem.style.right = 4;
            gem.style.width = 16;
            gem.style.height = 16;
            gem.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            gem.style.backgroundColor = (Color)new Color32(176, 140, 255, 255);
            Color edge = new Color32(60, 40, 110, 255);
            gem.style.borderTopColor = edge; gem.style.borderBottomColor = edge;
            gem.style.borderLeftColor = edge; gem.style.borderRightColor = edge;
            gem.style.borderTopWidth = 2; gem.style.borderBottomWidth = 2;
            gem.style.borderLeftWidth = 2; gem.style.borderRightWidth = 2;
            // slow two-step twinkle, pixel style
            gem.schedule.Execute(() =>
                gem.style.opacity = Mathf.Repeat(Time.unscaledTime, 1.6f) < 0.8f ? 1f : 0.65f).Every(200);
            return gem;
        }

        private Button _inventoryBtn;

        private void RefreshPreviewCircles()
        {
            if (vc_PlayerInventory.Instance == null) return;
            var skills = vc_PlayerInventory.Instance.GatheredSkills;
            _previewSkillCount = skills.Count;
            // no empty inventory box before the first skill (e.g. start of the tutorial)
            if (_inventoryBtn != null) _inventoryBtn.style.visibility = skills.Count > 0 ? Visibility.Visible : Visibility.Hidden;

            for (int i = 0; i < 6; i++)
            {
                if (_previews[i] == null) continue;
                bool hasSkill = i < skills.Count;
                _previews[i].style.display = hasSkill ? DisplayStyle.Flex : DisplayStyle.None;
                _previews[i].style.backgroundImage = hasSkill && skills[i] != null
                    ? vc_RiasecSymbols.Background(skills[i].riaSecLetter)
                    : NoBackground;
            }
        }

        private void OnSlotClicked(int i)      => vc_SkillManager.Instance?.GetSkillSlot(i)?.Activate();
        private void OnSlotPointerDown(int i)  => vc_SkillManager.Instance?.GetSkillSlot(i)?.SetHeld(true);
        private void OnSlotPointerUp(int i)    => vc_SkillManager.Instance?.GetSkillSlot(i)?.SetHeld(false);
        private void OnSkillAdded(vc_SkillData _) => RefreshPreviewCircles();
        private void OnOptionsChanged() => RefreshAllSlots(); // skill-name language may have switched
    }
}
