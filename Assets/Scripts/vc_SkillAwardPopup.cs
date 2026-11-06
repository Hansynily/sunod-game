using System;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.UI;

/// <summary>
/// "Pick a skill" reward overlay (UI Toolkit, UI V2 - SkillAward.uxml).
/// Shows up to three skill cards; tapping a card selects it (lifts + icon punch), the gold
/// button confirms and fires the callback with the chosen <see cref="vc_SkillData"/>.
/// Motion: scrim fades in, the board drops in with overshoot, cards pop in one by one and a
/// gold shine sweeps across; on close the board shrinks out while the scrim fades.
/// Same public API as the old uGUI Panel_SkillAward version.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public class vc_SkillAwardPopup : MonoBehaviour
{
    public static vc_SkillAwardPopup Instance { get; private set; }

    private const int CardCount = 3;

    [Header("Optional: hidden while the popup is open")]
    [SerializeField] private GameObject questObjectivesPanel;

    private VisualElement _root;
    private VisualElement _box;
    private VisualElement _title;
    private VisualElement _shine;
    private Button _confirm;
    private readonly Button[] _cards = new Button[CardCount];

    private vc_SkillData[] _offeredSkills;
    private vc_SkillData _selectedSkill;
    private int _selectedIndex = -1;
    private Action<vc_SkillData> _onConfirm;
    private PlayerController _playerController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _playerController = FindFirstObjectByType<PlayerController>();
    }

    private void OnEnable()
    {
        VisualElement doc = GetComponent<UIDocument>().rootVisualElement;
        _root = doc.Q<VisualElement>("award-root");
        _box = doc.Q<VisualElement>("award-box");
        _title = doc.Q<VisualElement>("award-title");
        _confirm = doc.Q<Button>("award-confirm");
        _confirm?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
        vc_UIAnim.Press(_confirm);
        BuildShine();

        for (int i = 0; i < CardCount; i++)
        {
            int idx = i;
            _cards[i] = doc.Q<Button>($"award-card-{i}");
            _cards[i]?.RegisterCallback<ClickEvent>(_ => OnCardSelected(idx));
            vc_UIAnim.Press(_cards[i], 0.95f);
        }

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Shows the panel with up to three skill choices (extras ignored, missing entries hide their card).
    /// <paramref name="onConfirm"/> is invoked with the chosen skill when the player presses Take Skill.
    /// </summary>
    public void Show(vc_SkillData[] skills, Action<vc_SkillData> onConfirm)
    {
        _offeredSkills = skills;
        _selectedSkill = null;
        _selectedIndex = -1;
        _onConfirm = onConfirm;

        PopulateCards(skills);
        RefreshSelection();
        SetVisible(true);
        PlayEntrance();

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(false);

        if (_playerController == null) _playerController = FindFirstObjectByType<PlayerController>();
        _playerController?.MoveAction.Disable();
    }

    /// <summary>Hides the panel without invoking the confirm callback.</summary>
    public void Hide()
    {
        SetVisible(false);

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(true);

        _onConfirm = null;
        _selectedSkill = null;
        _selectedIndex = -1;
        _playerController?.MoveAction.Enable();
    }

    private void SetVisible(bool visible)
    {
        if (_root == null) return;
        if (visible)
        {
            vc_UIAnim.Show(_root, vc_UIAnim.Enter.Fade);
            return;
        }
        // board shrinks out, scrim fades with it
        AnimHide(_box, null, vc_UIAnim.Exit.Shrink);
        AnimHide(_root, null, vc_UIAnim.Exit.Fade);
    }

    // ── Motion ──────────────────────────────────────────────────────────

    private void PlayEntrance()
    {
        // the board drops in with overshoot, the cards pop in one by one, then the title punches
        vc_UIAnim.Show(_box, vc_UIAnim.Enter.Drop, 0.05f, () => vc_UIAnim.Punch(_title, 0.12f, 0.3f), 0.36f);
        var shown = new System.Collections.Generic.List<VisualElement>();
        foreach (Button card in _cards)
            if (card != null && card.style.display != DisplayStyle.None) shown.Add(card);
        vc_UIAnim.Stagger(shown, vc_UIAnim.Enter.Pop, 0.07f, 0.2f);
        SweepShine(0.45f);
    }

    /// <summary>Gold shine bar (built once) clipped to the board, swept across on open.</summary>
    private void BuildShine()
    {
        if (_box == null || _shine != null) return;
        var clip = new VisualElement { name = "award-shine-clip", pickingMode = PickingMode.Ignore };
        clip.AddToClassList("award-shine-clip");
        _shine = new VisualElement { name = "award-shine", pickingMode = PickingMode.Ignore };
        _shine.AddToClassList("award-shine");
        clip.Add(_shine);
        _box.Add(clip);     // last child: drawn over the cards, never takes taps
    }

    private void SweepShine(float delay)
    {
        if (_shine == null || _box == null) return;
        VisualElement shine = _shine;
        VisualElement box = _box;
        shine.style.opacity = 0f;
        vc_UIAnim.Tween(shine, 0.85f, vc_UIAnim.InOutCubic, e =>
        {
            float w = Mathf.Max(400f, box.resolvedStyle.width);
            shine.style.translate = new Translate(Mathf.Lerp(-220f, w + 120f, e), 0f);
            shine.style.opacity = Mathf.Sin(Mathf.Clamp01(e) * Mathf.PI);   // fades in and out across the sweep
        }, () => shine.style.opacity = 0f, delay);
    }

    /// <summary>
    /// vc_UIAnim.Hide, but also safe when the element was shown this same frame (resolved
    /// display still None, so Hide would skip it and leave the entrance running).
    /// </summary>
    private static void AnimHide(VisualElement ve, Action done, vc_UIAnim.Exit kind)
    {
        if (ve == null) { done?.Invoke(); return; }
        if (ve.resolvedStyle.display == DisplayStyle.None)
        {
            vc_UIAnim.StopAll(ve);
            ve.style.display = DisplayStyle.None;
            ve.style.opacity = StyleKeyword.Null;
            ve.style.scale = StyleKeyword.Null;
            ve.style.translate = StyleKeyword.Null;
            done?.Invoke();
            return;
        }
        vc_UIAnim.Hide(ve, done, kind);
    }

    // ── Cards ───────────────────────────────────────────────────────────

    private void PopulateCards(vc_SkillData[] skills)
    {
        for (int i = 0; i < CardCount; i++)
        {
            Button card = _cards[i];
            if (card == null) continue;

            vc_SkillData skill = skills != null && i < skills.Length ? skills[i] : null;
            card.Clear();
            card.style.display = skill != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (skill == null) continue;

            string letter = string.IsNullOrWhiteSpace(skill.riaSecLetter)
                ? "?" : skill.riaSecLetter.Trim().ToUpperInvariant();

            bool riasec = letter.Length == 1 && "RIASEC".IndexOf(letter[0]) >= 0;
            Color kind = riasec ? vc_UITheme.Riasec(letter) : vc_UITheme.Brown;

            var cat = new VisualElement { pickingMode = PickingMode.Ignore };
            cat.AddToClassList("award-cat");
            cat.AddToClassList($"cat-bg-{letter}");
            cat.style.backgroundColor = kind;
            var catLabel = new Label(KindName(letter)) { pickingMode = PickingMode.Ignore };
            catLabel.AddToClassList("award-cat-label");
            cat.Add(catLabel);
            card.Add(cat);

            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("award-icon-frame");
            vc_UITheme.Border(frame, 4f, kind);       // ring in the kind colour
            frame.style.borderBottomWidth = 7f;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("award-icon");
            icon.style.backgroundImage = vc_RiasecSymbols.Background(letter);
            frame.Add(icon);
            card.Add(frame);

            var name = new Label(skill.DisplayName ?? "-") { pickingMode = PickingMode.Ignore };
            name.AddToClassList("award-name");
            card.Add(name);

            var desc = new Label(skill.description ?? string.Empty) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("award-desc");
            card.Add(desc);

            DecorateCard(card, frame, skill);
        }
    }

    // ── skill system v2: rare frame, duplicate (+XP) tag, level ────────────

    private static void DecorateCard(Button card, VisualElement iconFrame, vc_SkillData skill)
    {
        bool rare = vc_SkillLevels.IsRare(skill);
        bool duplicate = vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(skill);
        int level = vc_SkillLevels.Level(skill);

        // gold frame for rare cards (class toggled both ways: cards are reused every night)
        card.EnableInClassList("award-card--rare", rare);
        if (rare)
        {
            var ribbon = new Label("Rare") { pickingMode = PickingMode.Ignore };
            ribbon.AddToClassList("sn-badge");
            ribbon.AddToClassList("sn-badge--gold");
            ribbon.AddToClassList("award-rare");
            card.Add(ribbon);
        }

        // Lv badge on the icon disc: silver Lv2, gold Lv3
        if (level > 1 && iconFrame != null)
        {
            var lv = new Label($"Lv{level}") { pickingMode = PickingMode.Ignore };
            lv.AddToClassList("sn-badge");
            lv.AddToClassList(level >= vc_SkillLevels.MaxLevel ? "sn-badge--gold" : "sn-badge--silver");
            lv.AddToClassList("award-lv");
            iconFrame.Add(lv);
        }

        // what taking it does: a new skill, or XP for one you have
        string line = duplicate
            ? (level >= vc_SkillLevels.MaxLevel ? "Have it - max level"
                : $"Have it: +{vc_SkillLevels.XpDuplicate} XP ({vc_SkillLevels.XpToNext(skill)} to Lv{level + 1})")
            : (level > 1 ? $"New - remembered Lv{level}" : "New skill");
        var tag = new Label(line) { pickingMode = PickingMode.Ignore };
        tag.AddToClassList("award-tag");
        tag.EnableInClassList("award-tag--dup", duplicate);
        card.Add(tag);
    }

    private void OnCardSelected(int index)
    {
        if (_offeredSkills == null || index < 0 || index >= _offeredSkills.Length || _offeredSkills[index] == null) return;

        bool changed = _selectedIndex != index;
        _selectedIndex = index;
        _selectedSkill = _offeredSkills[index];
        RefreshSelection();

        if (changed && _cards[index] != null)
        {
            vc_UIAnim.Punch(_cards[index].Q(className: "award-icon-frame"), 0.22f, 0.4f);
            vc_UIAnim.Punch(_confirm, 0.08f, 0.3f);
        }
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < CardCount; i++)
        {
            if (_cards[i] == null) continue;
            _cards[i].EnableInClassList("award-card--selected", i == _selectedIndex);
            _cards[i].EnableInClassList("award-card--dimmed", _selectedIndex >= 0 && i != _selectedIndex);
        }

        _confirm?.SetEnabled(_selectedSkill != null);
        if (_confirm != null)
            _confirm.text = _selectedSkill == null ? "Pick one"
                : vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(_selectedSkill)
                    ? $"Train {_selectedSkill.DisplayName}"
                    : $"Take {_selectedSkill.DisplayName}";
    }

    private void OnConfirmClicked()
    {
        if (_selectedSkill == null) return;

        SetVisible(false);

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(true);

        _playerController?.MoveAction.Enable();

        Action<vc_SkillData> callback = _onConfirm;
        vc_SkillData chosen = _selectedSkill;
        _onConfirm = null;
        _selectedSkill = null;
        _selectedIndex = -1;

        callback?.Invoke(chosen);
    }

    private static string KindName(string letter)
    {
        switch (letter)
        {
            case "R": return "Realistic";
            case "I": return "Investigative";
            case "A": return "Artistic";
            case "S": return "Social";
            case "E": return "Enterprising";
            case "C": return "Conventional";
            default:  return "Skill";
        }
    }
}
