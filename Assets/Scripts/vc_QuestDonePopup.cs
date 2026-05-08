using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_QuestDonePopup : MonoBehaviour
{
    public static vc_QuestDonePopup Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text textQuestName;
    [SerializeField] private TMP_Text textStars;
    [SerializeField] private Button btnContinue;

    private Action _onContinue;
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

        if (btnContinue != null)
            btnContinue.onClick.AddListener(OnContinueTapped);

        if (panel != null)
            panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (btnContinue != null)
            btnContinue.onClick.RemoveListener(OnContinueTapped);

        if (Instance == this)
            Instance = null;
    }

    public void Show(string questName, int stars, Action onContinue)
    {
        if (textQuestName != null)
            textQuestName.text = questName;

        if (textStars != null)
            textStars.text = BuildStarString(stars);

        _onContinue = onContinue;

        if (panel != null)
            panel.SetActive(true);

        _playerController?.MoveAction.Disable();
    }

    private void OnContinueTapped()
    {
        if (panel != null)
            panel.SetActive(false);

        _playerController?.MoveAction.Enable();

        Action callback = _onContinue;
        _onContinue = null;
        callback?.Invoke();
    }

    private static string BuildStarString(int stars)
    {
        int clamped = Mathf.Clamp(stars, 0, 5);
        return $"{clamped} / 5 Stars";
    }
}
