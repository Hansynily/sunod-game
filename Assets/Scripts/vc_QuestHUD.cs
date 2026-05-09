using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_QuestHUD : MonoBehaviour
{
    public static vc_QuestHUD Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI questCounter;
    [SerializeField] private TextMeshProUGUI questObjective;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private vc_HintSystem hintSystem;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetCounter(string text)
    {
        if (questCounter != null) questCounter.text = text;
    }

    public void SetObjective(string text)
    {
        if (questObjective == null) return;
        questObjective.text = text;
        questObjective.gameObject.SetActive(true);
    }

    public void HideObjective()
    {
        if (questObjective != null) questObjective.gameObject.SetActive(false);
    }

    public void SetDescription(string text)
    {
        if (questDescriptionText != null) questDescriptionText.text = text;
    }

    public void SetHints(string[] hints)
    {
        hintSystem?.SetHints(hints);
    }

    public void ShowFeedback(string text)
    {
        if (feedbackText == null) return;
        feedbackText.text = text;
        feedbackText.gameObject.SetActive(true);
    }

    public void HideFeedback()
    {
        if (feedbackText == null) return;
        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }
}
