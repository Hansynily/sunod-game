using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_QuestHUD : MonoBehaviour
{
    public static vc_QuestHUD Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI questCounter;
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private TextMeshProUGUI questObjective;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private vc_HintSystem hintSystem;
    [SerializeField] private Transform objectivesContainer;
    [SerializeField] private vc_CheckboxItem checkboxItemPrefab;
    [SerializeField] private GameObject questInfoPanel;

    private Coroutine _feedbackCoroutine;
    private vc_CheckboxItem[] _checkboxItems;

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
        if (_feedbackCoroutine != null) return;
        if (feedbackText == null) return;
        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }

    public void ForceHideFeedback()
    {
        if (_feedbackCoroutine != null) { StopCoroutine(_feedbackCoroutine); _feedbackCoroutine = null; }
        if (feedbackText == null) return;
        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }

    public void ShowFeedbackTimed(string text, float duration = 2f)
    {
        ShowFeedback(text);
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = StartCoroutine(ClearFeedbackAfter(duration));
    }

    private IEnumerator ClearFeedbackAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideFeedback();
        _feedbackCoroutine = null;
    }

    public void ShowQuestInfo(string counter, string title, string description, string[] steps)
    {
        SetCounter(counter);
        if (questTitleText != null) questTitleText.text = title;
        SetDescription(description);

        if (objectivesContainer != null)
        {
            for (int i = objectivesContainer.childCount - 1; i >= 0; i--)
                Destroy(objectivesContainer.GetChild(i).gameObject);
        }

        if (steps != null && checkboxItemPrefab != null && objectivesContainer != null)
        {
            _checkboxItems = new vc_CheckboxItem[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                vc_CheckboxItem item = Instantiate(checkboxItemPrefab, objectivesContainer);
                item.SetLabel(steps[i]);
                item.SetChecked(false);
                _checkboxItems[i] = item;
            }
        }

        questInfoPanel?.SetActive(true);
    }

    public void CheckObjective(int index)
    {
        if (_checkboxItems == null || index < 0 || index >= _checkboxItems.Length) return;
        _checkboxItems[index]?.SetChecked(true);
    }

    public void HideQuestInfo()
    {
        questInfoPanel?.SetActive(false);
        _checkboxItems = null;
    }
}
