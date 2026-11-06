using UnityEngine;
using UnityEngine.UIElements;
using UGUIButton = UnityEngine.UI.Button;

namespace SunodGame.UI
{
    /// <summary>
    /// Game HUD (UI V2, 2026-11-07): the radial skill menu bottom-right (vc_SkillRadialMenu)
    /// and the bag button that opens the inventory. The radial folds away by default, which
    /// replaces the old "hide skills while roaming town" toggle: only the hub shows until the
    /// player taps or holds it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDUIToolkit : MonoBehaviour
    {
        [SerializeField] private GameObject _uguiSkillBarRoot;

        private static GameHUDUIToolkit _instance;

        private vc_SkillRadialMenu _radial;
        private readonly VisualElement[] _previews = new VisualElement[6];
        private readonly vc_SkillData[] _cachedSlots = new vc_SkillData[vc_SkillManager.SlotCount];
        private readonly bool[] _glowing = new bool[vc_SkillManager.SlotCount];

        private InventoryPanelUIToolkit _inventoryPanel;
        private Button _inventoryBtn;
        private int _previewSkillCount = -1;
        private bool _inventoryEventBound;
        private bool _wasInQuest;

        private void OnEnable()
        {
            _instance = this;

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
            VisualElement hudRoot = root.Q<VisualElement>("hud-root") ?? root;

            if (_radial == null || _radial.parent == null)
            {
                _radial = new vc_SkillRadialMenu();
                // the panel is 720 units tall: the 480-unit radial is drawn at 0.62 (~300 units)
                var wrap = new VisualElement { name = "skill-radial-wrap", pickingMode = PickingMode.Ignore };
                wrap.AddToClassList("skill-radial-wrap");
                wrap.Add(_radial);
                hudRoot.Add(wrap);
                _radial.PetalActivated += OnPetalActivated;
                _radial.PetalHeld += OnPetalHeld;
            }

            for (int i = 0; i < 6; i++)
                _previews[i] = root.Q<VisualElement>($"preview-{i}");

            _inventoryBtn = root.Q<Button>("btn-open-inventory");
            if (_inventoryBtn != null)
            {
                _inventoryBtn.RegisterCallback<ClickEvent>(_ => _inventoryPanel?.Show());
                vc_UIAnim.Press(_inventoryBtn);
            }

            // the HUD slides in from the bottom-right on load
            vc_UIAnim.Show(_radial, vc_UIAnim.Enter.SlideLeft, 0.15f);
            if (_inventoryBtn != null) vc_UIAnim.Show(_inventoryBtn, vc_UIAnim.Enter.Pop, 0.3f);

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
            if (_instance == this) _instance = null;
            vc_PlayerOptions.Changed -= OnOptionsChanged;
            vc_SkillLevels.Changed -= OnSkillLevelsChanged;
            vc_SkillLevels.LevelUp -= OnSkillLevelUp;

            if (_inventoryEventBound && vc_PlayerInventory.Instance != null)
            {
                vc_PlayerInventory.Instance.OnSkillAdded -= OnSkillAdded;
                _inventoryEventBound = false;
            }
        }

        /// <summary>Open the radial (the tutorial does this before pointing at a skill).</summary>
        public static void OpenSkillMenu() => _instance?._radial?.Open();

        /// <summary>Fold the radial away.</summary>
        public static void CloseSkillMenu() => _instance?._radial?.Close();

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
            if (mgr == null || _radial == null) return;

            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                var current = mgr.GetSkillInSlot(i);
                if (current != _cachedSlots[i])
                {
                    _cachedSlots[i] = current;
                    RefreshSlot(i, current);
                }

                var uguiBtn = mgr.GetSkillSlot(i)?.GetComponent<UGUIButton>();
                if (uguiBtn != null) _radial.SetPetalEnabled(i, uguiBtn.interactable);

                _radial.SetCooldown(i, current != null ? mgr.CooldownFraction(i) : 0f, mgr.CooldownRemaining(i));
            }

            UpdatePrepGlow();

            // a quest just ended: fold the skills away so the town is clear again
            bool inQuest = vc_TownStage.Current != null && vc_TownStage.Current.InQuest;
            if (_wasInQuest && !inQuest) _radial.Close();
            _wasInQuest = inQuest;
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
            if (_radial == null) return;
            string letter = vc_SkillManager.CategoryLetterForSlot(i);
            _radial.SetSlot(i, letter, vc_RiasecSymbols.Background(letter),
                data != null ? (data.DisplayName ?? "-").ToUpper() : "",
                data != null, data != null && vc_LoopMeta.IsKept(data));
            int level = data != null ? vc_SkillLevels.Level(data) : 1;
            _radial.SetLevel(i, level, level >= vc_SkillLevels.MaxLevel);
        }

        /// <summary>The skill a prep made instant breathes gold until the quest ends.</summary>
        private void UpdatePrepGlow()
        {
            string tag = vc_FitQuestBase.PrepGlowTag;
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                bool glow = !string.IsNullOrEmpty(tag) && _cachedSlots[i] != null && _cachedSlots[i].HasTag(tag);
                if (glow != _glowing[i]) _radial.SetGlow(i, glow);
                _glowing[i] = glow;
            }
        }

        private void OnSkillLevelsChanged()
        {
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                int level = _cachedSlots[i] != null ? vc_SkillLevels.Level(_cachedSlots[i]) : 1;
                _radial?.SetLevel(i, level, level >= vc_SkillLevels.MaxLevel);
            }
        }

        private void OnSkillLevelUp(vc_SkillData skill, int from, int to)
        {
            SunodGame.Core.vc_Sfx.Play("skill_equip", 0.8f);
            vc_RunProgressUI.Instance?.ShowStamp($"{skill.DisplayName} Lv{to}!",
                to >= vc_SkillLevels.MaxLevel ? vc_UITheme.Gold : (Color)new Color32(120, 140, 170, 255), 1.1f);
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
                if (_cachedSlots[i] == skill) _radial?.LevelUpFlash(i);
        }

        private void RefreshPreviewCircles()
        {
            if (vc_PlayerInventory.Instance == null) return;
            var skills = vc_PlayerInventory.Instance.GatheredSkills;
            int before = _previewSkillCount;
            _previewSkillCount = skills.Count;
            // no empty bag before the first skill (e.g. start of the tutorial)
            if (_inventoryBtn != null) _inventoryBtn.style.visibility = skills.Count > 0 ? Visibility.Visible : Visibility.Hidden;

            for (int i = 0; i < 6; i++)
            {
                if (_previews[i] == null) continue;
                bool hasSkill = i < skills.Count && skills[i] != null;
                _previews[i].style.display = hasSkill ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasSkill) _previews[i].style.backgroundColor = vc_UITheme.Riasec(skills[i].riaSecLetter);
            }
            // a new skill landed in the bag: bounce it
            if (before >= 0 && skills.Count > before && _inventoryBtn != null) vc_UIAnim.Punch(_inventoryBtn, 0.2f, 0.4f);
        }

        private void OnPetalActivated(int i) => vc_SkillManager.Instance?.GetSkillSlot(i)?.Activate();
        private void OnPetalHeld(int i, bool held) => vc_SkillManager.Instance?.GetSkillSlot(i)?.SetHeld(held);
        private void OnSkillAdded(vc_SkillData _) => RefreshPreviewCircles();
        private void OnOptionsChanged() => RefreshAllSlots(); // skill-name language may have switched
    }
}
