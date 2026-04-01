using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class vc_SkillSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private int slotIndex = 0;
    [SerializeField] private vc_PlayerSkill assignedSkill;
    [SerializeField] private Button skillButton;
    [SerializeField] private TextMeshProUGUI skillLabel;

    private bool _isHeld = false;

    public event Action<vc_SkillSlot> SkillPressed;

    public int SlotIndex => slotIndex;
    public vc_PlayerSkill AssignedSkill => assignedSkill;
    public vc_SkillData AssignedSkillData => assignedSkill != null ? assignedSkill.SkillData : null;
    public bool IsHeld => _isHeld;

    private void Awake()
    {
        EnsureReferences();

        if (skillButton != null)
        {
            skillButton.onClick.AddListener(NotifySkillPressed);
        }

        RefreshDisplay();
    }

    private void OnDestroy()
    {
        if (skillButton != null)
        {
            skillButton.onClick.RemoveListener(NotifySkillPressed);
        }
    }

    private void OnDisable()
    {
        _isHeld = false;
    }

    public void RefreshDisplay()
    {
        EnsureReferences();

        if (skillLabel != null)
        {
            skillLabel.text = AssignedSkillData != null ? AssignedSkillData.buttonLabel : string.Empty;
        }
    }

    public void AssignSkill(vc_PlayerSkill skill)
    {
        assignedSkill = skill;
        RefreshDisplay();
    }

    private void EnsureReferences()
    {
        if (skillButton == null)
        {
            skillButton = GetComponent<Button>();
        }

        if (skillLabel == null)
        {
            skillLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (skillLabel == null && skillButton != null)
        {
            skillLabel = CreateRuntimeLabel(skillButton.transform);
        }
    }

    private void NotifySkillPressed()
    {
        assignedSkill?.UseSkill();
        SkillPressed?.Invoke(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isHeld = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isHeld = false;
    }

    private static TextMeshProUGUI CreateRuntimeLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("SkillLabel", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 3f;
        label.fontSizeMax = 8f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = string.Empty;
        return label;
    }
}
