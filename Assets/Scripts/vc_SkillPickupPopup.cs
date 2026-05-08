using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_SkillPickupPopup : MonoBehaviour
{
    public static vc_SkillPickupPopup Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text textSkillName;
    [SerializeField] private TMP_Text textDescription;
    [SerializeField] private Image imageSkillIcon;
    [SerializeField] private Button panelButton;

    private Action _onConfirm;
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

    public void Show(vc_SkillData data, Action onConfirm)
    {
        textSkillName.text = data.skillName;
        textDescription.text = data.description;
        if (data.icon != null)
            imageSkillIcon.sprite = data.icon;

        _onConfirm = onConfirm;
        panelButton.onClick.RemoveAllListeners();
        panelButton.onClick.AddListener(OnPanelTapped);
        panel.SetActive(true);

        _playerController?.MoveAction.Disable();
    }

    private void OnPanelTapped()
    {
        panel.SetActive(false);
        _onConfirm?.Invoke();
        _onConfirm = null;
        _playerController?.MoveAction.Enable();
    }

    public void Hide()
    {
        panel.SetActive(false);
        _onConfirm = null;
        _playerController?.MoveAction.Enable();
    }
}
