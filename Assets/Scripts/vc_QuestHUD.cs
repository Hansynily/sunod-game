using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class vc_QuestHUD : MonoBehaviour
{
    public static vc_QuestHUD Instance { get; private set; }

    private VisualElement _questPanel;
    private VisualElement _currentRow;
    private VisualElement _objectivesList;
    private Label _counterLabel;
    private Label _titleLabel;
    private Label _activeLabel;
    private Label _feedbackLabel;
    private Button _expandBtn;

    private string[] _steps;
    private bool[] _done;
    private bool _expanded;
    private string _savedCounter;
    private string _savedTitle;
    private Coroutine _feedbackCoroutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _questPanel      = root.Q<VisualElement>("quest-panel");
        _currentRow      = root.Q<VisualElement>("quest-current-row");
        _objectivesList  = root.Q<VisualElement>("quest-objectives-list");
        _counterLabel    = root.Q<Label>("quest-counter");
        _titleLabel      = root.Q<Label>("quest-title");
        _activeLabel     = root.Q<Label>("quest-active-label");
        _feedbackLabel   = root.Q<Label>("quest-feedback");
        _expandBtn       = root.Q<Button>("quest-expand-btn");

        _expandBtn?.RegisterCallback<ClickEvent>(_ => ToggleExpand());

        if (_steps != null)
        {
            if (_counterLabel != null) _counterLabel.text = _savedCounter;
            if (_titleLabel   != null) _titleLabel.text   = _savedTitle;
            RebuildObjectiveRows();
            for (int i = 0; i < _done.Length; i++)
                if (_done[i]) UpdateObjectiveRow(i, done: true);
            RefreshActiveObjective();
            _objectivesList?.SetDisplay(_expanded);
            if (_expandBtn != null) _expandBtn.text = _expanded ? "▲" : "▼";
            _questPanel?.SetDisplay(true);
        }
        else
        {
            _questPanel?.SetDisplay(false);
        }
    }

    // ── Public API (same signatures as the old uGUI version) ──────────────

    public void ShowQuestInfo(string counter, string title, string description, string[] steps)
    {
        _steps = steps;
        _done  = steps != null ? new bool[steps.Length] : null;
        _savedCounter = counter;
        _savedTitle   = title;

        if (_counterLabel != null) _counterLabel.text = counter;
        if (_titleLabel   != null) _titleLabel.text   = title;

        RebuildObjectiveRows();
        RefreshActiveObjective();

        _expanded = false;
        _objectivesList?.SetDisplay(false);
        if (_expandBtn != null) _expandBtn.text = "▼";

        _questPanel?.SetDisplay(true);
    }

    public void CheckObjective(int index)
    {
        if (_done == null || index < 0 || index >= _done.Length) return;
        _done[index] = true;
        UpdateObjectiveRow(index, done: true);
        RefreshActiveObjective();
    }

    public void HideQuestInfo()
    {
        _questPanel?.SetDisplay(false);
        _steps = null;
        _done  = null;
    }

    public void SetCounter(string text)
    {
        if (_counterLabel != null) _counterLabel.text = text;
    }

    public void SetObjective(string text)
    {
        if (_activeLabel  != null) _activeLabel.text = text;
        _currentRow?.SetDisplay(true);
    }

    public void HideObjective()
    {
        _currentRow?.SetDisplay(false);
    }

    // Kept for API compatibility — description/hints not shown in the new compact design.
    public void SetDescription(string text) { }
    public void SetHints(string[] hints)    { }

    public void ShowFeedback(string text)
    {
        if (_feedbackLabel == null) return;
        _feedbackLabel.text = text;
        _feedbackLabel.SetDisplay(true);
    }

    public void HideFeedback()
    {
        if (_feedbackCoroutine != null) return;
        if (_feedbackLabel == null) return;
        _feedbackLabel.text = string.Empty;
        _feedbackLabel.SetDisplay(false);
    }

    public void ForceHideFeedback()
    {
        if (_feedbackCoroutine != null) { StopCoroutine(_feedbackCoroutine); _feedbackCoroutine = null; }
        if (_feedbackLabel == null) return;
        _feedbackLabel.text = string.Empty;
        _feedbackLabel.SetDisplay(false);
    }

    public void ShowFeedbackTimed(string text, float duration = 2f)
    {
        ShowFeedback(text);
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = StartCoroutine(ClearFeedbackAfter(duration));
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        _objectivesList?.SetDisplay(_expanded);
        if (_expandBtn != null) _expandBtn.text = _expanded ? "▲" : "▼";
    }

    private void RefreshActiveObjective()
    {
        if (_steps == null || _done == null) return;

        for (int i = 0; i < _steps.Length; i++)
        {
            if (!_done[i])
            {
                if (_activeLabel != null) _activeLabel.text = _steps[i];
                _currentRow?.SetDisplay(true);
                return;
            }
        }

        // All objectives done
        if (_activeLabel != null) _activeLabel.text = "All done!";
    }

    private void RebuildObjectiveRows()
    {
        if (_objectivesList == null) return;
        _objectivesList.Clear();

        if (_steps == null) return;

        for (int i = 0; i < _steps.Length; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("quest-obj-row");
            row.name = $"obj-row-{i}";

            var check = new Label { name = $"obj-check-{i}", text = "●" };
            check.AddToClassList("quest-obj-check");
            check.AddToClassList("quest-obj-check--pending");

            var label = new Label { name = $"obj-text-{i}", text = _steps[i] };
            label.AddToClassList("quest-obj-text");

            row.Add(check);
            row.Add(label);
            _objectivesList.Add(row);
        }
    }

    private void UpdateObjectiveRow(int index, bool done)
    {
        if (_objectivesList == null) return;

        var check = _objectivesList.Q<Label>($"obj-check-{index}");
        var label = _objectivesList.Q<Label>($"obj-text-{index}");

        if (check != null)
        {
            check.text = done ? "✓" : "●";
            check.EnableInClassList("quest-obj-check--pending", !done);
        }

        label?.EnableInClassList("quest-obj-text--done", done);
    }

    private IEnumerator ClearFeedbackAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_feedbackLabel != null)
        {
            _feedbackLabel.text = string.Empty;
            _feedbackLabel.SetDisplay(false);
        }
        _feedbackCoroutine = null;
    }
}

// Extension to keep display toggling terse
internal static class VisualElementDisplayExt
{
    internal static void SetDisplay(this VisualElement el, bool visible)
    {
        if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
