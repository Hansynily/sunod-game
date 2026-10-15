using System;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.UI;

/// <summary>
/// Hallway "Choose a Skill" overlay (UI Toolkit, Sprout Lands theme - SkillAward.uxml).
/// Shows up to three skill cards; tapping a card selects it (lifts + glows), Take Skill
/// confirms and fires the callback with the chosen <see cref="vc_SkillData"/>.
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
        _confirm = doc.Q<Button>("award-confirm");
        _confirm?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());

        for (int i = 0; i < CardCount; i++)
        {
            int idx = i;
            _cards[i] = doc.Q<Button>($"award-card-{i}");
            _cards[i]?.RegisterCallback<ClickEvent>(_ => OnCardSelected(idx));
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
        if (_root != null)
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

            var cat = new VisualElement { pickingMode = PickingMode.Ignore };
            cat.AddToClassList("award-cat");
            cat.AddToClassList($"cat-bg-{letter}");
            var catLabel = new Label($"{letter} - {KindName(letter)}".ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            catLabel.AddToClassList("award-cat-label");
            cat.Add(catLabel);
            card.Add(cat);

            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("award-icon-frame");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("award-icon");
            icon.style.backgroundImage = vc_RiasecSymbols.Background(letter);
            frame.Add(icon);
            card.Add(frame);

            var name = new Label((skill.DisplayName ?? "-").ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("award-name");
            card.Add(name);

            var desc = new Label(skill.description ?? string.Empty) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("award-desc");
            card.Add(desc);
        }
    }

    private void OnCardSelected(int index)
    {
        if (_offeredSkills == null || index < 0 || index >= _offeredSkills.Length || _offeredSkills[index] == null) return;

        _selectedIndex = index;
        _selectedSkill = _offeredSkills[index];
        RefreshSelection();
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
            _confirm.text = _selectedSkill != null ? $"Take {_selectedSkill.DisplayName}" : "Pick a skill";
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
