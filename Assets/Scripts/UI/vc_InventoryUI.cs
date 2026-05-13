using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform gridSkillCards;
    [SerializeField] private Button[] inventorySlotButtons = new Button[4];
    [SerializeField] private TMP_Text[] slotLabels = new TMP_Text[4];
    [SerializeField] private GameObject skillCardPrefab;
    [SerializeField] private Button btnOpen;
    [SerializeField] private Button btnClose;

    private vc_SkillManager _skillManager;
    private vc_SkillData _selectedSkill;
    private readonly List<GameObject> _spawnedCards = new List<GameObject>();
    private bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (vc_PlayerInventory.Instance != null)
            vc_PlayerInventory.Instance.OnSkillAdded += OnSkillAdded;
    }

    private void OnDisable()
    {
        if (vc_PlayerInventory.Instance != null)
            vc_PlayerInventory.Instance.OnSkillAdded -= OnSkillAdded;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        _skillManager = FindFirstObjectByType<vc_SkillManager>();

        if (btnOpen != null)
        {
            btnOpen.onClick.AddListener(OpenPanel);
        }

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(ClosePanel);
        }

        if (inventorySlotButtons != null)
        {
            for (int i = 0; i < inventorySlotButtons.Length; i++)
            {
                int slotIndex = i;
                if (inventorySlotButtons[i] != null)
                {
                    inventorySlotButtons[i].onClick.AddListener(() => OnSlotTapped(slotIndex));
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (btnOpen != null)
        {
            btnOpen.onClick.RemoveListener(OpenPanel);
        }

        if (btnClose != null)
        {
            btnClose.onClick.RemoveListener(ClosePanel);
        }
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        _selectedSkill = null;
        RefreshCards();
        RefreshSlotLabels();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        _selectedSkill = null;
    }

    private void RefreshCards()
    {
        if (gridSkillCards != null)
        {
            for (int i = gridSkillCards.childCount - 1; i >= 0; i--)
            {
                Transform child = gridSkillCards.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }

        _spawnedCards.Clear();

        if (vc_PlayerInventory.Instance == null || skillCardPrefab == null || gridSkillCards == null)
        {
            return;
        }

        foreach (vc_SkillData skill in vc_PlayerInventory.Instance.GatheredSkills)
        {
            if (skill == null)
            {
                continue;
            }

            GameObject card = Instantiate(skillCardPrefab, gridSkillCards);
            TMP_Text skillNameLabel = FindChildText(card.transform, "Lbl_SkillName");
            if (skillNameLabel != null)
            {
                skillNameLabel.text = skill.skillName;
            }

            Button cardButton = card.GetComponent<Button>();
            if (cardButton != null)
            {
                vc_SkillData capturedSkill = skill;
                cardButton.onClick.AddListener(() => OnSkillCardTapped(capturedSkill));
            }

            _spawnedCards.Add(card);
        }
    }

    private void RefreshSlotLabels()
    {
        if (slotLabels == null)
        {
            return;
        }

        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null)
            {
                continue;
            }

            vc_SkillData equipped = _skillManager != null ? _skillManager.GetSkillInSlot(i) : null;
            slotLabels[i].text = equipped != null ? equipped.skillName : "Empty";
        }
    }

    private void OnSkillCardTapped(vc_SkillData skill)
    {
        _selectedSkill = skill;
    }

    private void OnSlotTapped(int slotIndex)
    {
        if (_selectedSkill == null || _skillManager == null)
        {
            return;
        }

        _skillManager.AssignSkillToSlot(slotIndex, _selectedSkill);
        _selectedSkill = null;
        RefreshSlotLabels();
    }

    private void OnSkillAdded(vc_SkillData skill)
    {
        if (panelRoot != null && panelRoot.activeSelf)
        {
            RefreshCards();
        }
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].name == childName)
            {
                return labels[i];
            }
        }

        return null;
    }
}
