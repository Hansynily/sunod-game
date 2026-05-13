using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Panel_SkillAward overlay: populates three skill cards, enables the
/// ConfirmButton only after the player selects a card, and fires a callback with the
/// chosen <see cref="vc_SkillData"/> when confirmed.
/// </summary>
[DisallowMultipleComponent]
public class vc_SkillAwardPopup : MonoBehaviour
{
    private static readonly Color ColorButtonEnabled  = new(0.878f, 0.706f, 0.075f, 1f);
    private static readonly Color ColorButtonDisabled = new(0.35f,  0.35f,  0.35f,  1f);

    public static vc_SkillAwardPopup Instance { get; private set; }

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject questObjectivesPanel;

    [Header("Title")]
    [SerializeField] private TMP_Text titleText;

    [Header("Skill Cards (exactly 3)")]
    [SerializeField] private Button[]    cardButtons  = new Button[3];
    [SerializeField] private Image[]     cardIcons    = new Image[3];
    [SerializeField] private TMP_Text[]  cardNames    = new TMP_Text[3];
    [SerializeField] private TMP_Text[]  cardDescs    = new TMP_Text[3];

    [Header("Confirm Button")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image  confirmButtonImage;

    [Header("Card Selection Colors")]
    [SerializeField] private Color cardDefaultColor  = Color.white;
    [SerializeField] private Color cardSelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Image[] cardBackgrounds = new Image[3];

    private vc_SkillData[] _offeredSkills;
    private vc_SkillData   _selectedSkill;
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

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Shows the panel with three skill choices.
    /// <paramref name="skills"/> must contain exactly 3 entries (extras are ignored, missing entries show empty cards).
    /// <paramref name="onConfirm"/> is invoked with the chosen skill when the player presses Confirm.
    /// </summary>
    public void Show(vc_SkillData[] skills, Action<vc_SkillData> onConfirm)
    {
        _offeredSkills = skills;
        _selectedSkill = null;
        _onConfirm = onConfirm;

        PopulateCards(skills);
        SetConfirmInteractable(false);

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(false);

        _playerController?.MoveAction.Disable();
    }

    /// <summary>Hides the panel without invoking the confirm callback.</summary>
    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(true);

        _onConfirm = null;
        _selectedSkill = null;
        _playerController?.MoveAction.Enable();
    }

    private void PopulateCards(vc_SkillData[] skills)
    {
        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] == null) continue;

            cardButtons[i].onClick.RemoveAllListeners();

            bool hasSkill = skills != null && i < skills.Length && skills[i] != null;
            vc_SkillData skill = hasSkill ? skills[i] : null;

            if (cardBackgrounds[i] != null)
                cardBackgrounds[i].color = cardDefaultColor;

            if (cardIcons[i] != null)
                cardIcons[i].sprite = skill != null ? skill.icon : null;

            if (cardNames[i] != null)
                cardNames[i].text = skill != null ? skill.skillName : string.Empty;

            if (cardDescs[i] != null)
                cardDescs[i].text = skill != null ? skill.description : string.Empty;

            int capturedIndex = i;
            cardButtons[i].onClick.AddListener(() => OnCardSelected(capturedIndex));
            cardButtons[i].interactable = hasSkill;
        }
    }

    private void OnCardSelected(int index)
    {
        if (_offeredSkills == null || index < 0 || index >= _offeredSkills.Length) return;

        _selectedSkill = _offeredSkills[index];
        SetConfirmInteractable(_selectedSkill != null);

        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardBackgrounds[i] != null)
                cardBackgrounds[i].color = (i == index) ? cardSelectedColor : cardDefaultColor;
        }

        Debug.Log($"[vc_SkillAwardPopup] Selected: {_selectedSkill?.skillName}");
    }

    private void OnConfirmClicked()
    {
        if (_selectedSkill == null) return;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (questObjectivesPanel != null)
            questObjectivesPanel.SetActive(true);

        _playerController?.MoveAction.Enable();

        Action<vc_SkillData> callback = _onConfirm;
        vc_SkillData chosen = _selectedSkill;
        _onConfirm = null;
        _selectedSkill = null;

        callback?.Invoke(chosen);
    }

    private void SetConfirmInteractable(bool interactable)
    {
        if (confirmButton != null)
            confirmButton.interactable = interactable;

        if (confirmButtonImage != null)
            confirmButtonImage.color = interactable ? ColorButtonEnabled : ColorButtonDisabled;
    }
}
