using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

/// <summary>
/// Quest card, top-left (UI V2): title, divider, the current objective with a checkbox.
/// The card slides in from the left when a quest starts and out when it ends. Checking the
/// current objective ticks the box with a punch, strikes the line, then the next objective
/// slides in. Tap the header to see every objective.
/// </summary>
[DisallowMultipleComponent]
public class vc_QuestHUD : MonoBehaviour
{
    public static vc_QuestHUD Instance { get; private set; }

    private VisualElement _questPanel;
    private VisualElement _header;
    private VisualElement _chevron;
    private VisualElement _currentRow;
    private VisualElement _objectivesList;
    private Label _counterLabel;
    private Label _titleLabel;
    private Label _activeCheck;
    private Label _activeLabel;
    private Label _feedbackLabel;
    private Button _expandBtn;

    private string[] _steps;
    private bool[] _done;
    private bool _expanded;
    private string _savedCounter;
    private string _savedTitle;
    private Coroutine _feedbackCoroutine;

    // current-row animation bookkeeping (visual only)
    private int _activeIndex = -1;
    private int _rowVersion;            // bumps whenever something sets the current row
    private int _pendingVersion = -1;   // == _rowVersion while a "done -> next" animation runs
    private readonly HashSet<VisualElement> _hiding = new HashSet<VisualElement>();

    private const float DoneHoldSeconds = 0.45f;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _hiding.Clear();

        _questPanel      = root.Q<VisualElement>("quest-panel");
        _header          = root.Q<VisualElement>("quest-header");
        _chevron         = root.Q<VisualElement>("quest-chevron");
        _currentRow      = root.Q<VisualElement>("quest-current-row");
        _objectivesList  = root.Q<VisualElement>("quest-objectives-list");
        _counterLabel    = root.Q<Label>("quest-counter");
        _titleLabel      = root.Q<Label>("quest-title");
        _activeCheck     = root.Q<Label>("quest-arrow");
        _activeLabel     = root.Q<Label>("quest-active-label");
        _feedbackLabel   = root.Q<Label>("quest-feedback");
        _expandBtn       = root.Q<Button>("quest-expand-btn");

        AddTick(_activeCheck);

        // The whole header row toggles the list (a big thumb target); the round button too.
        // (unregister first: re-enabling the component on the same tree must not double-toggle)
        _header?.UnregisterCallback<ClickEvent>(OnHeaderClick);
        _header?.RegisterCallback<ClickEvent>(OnHeaderClick);
        _expandBtn?.UnregisterCallback<ClickEvent>(OnExpandClick);
        _expandBtn?.RegisterCallback<ClickEvent>(OnExpandClick);
        if (_expandBtn != null) vc_UIAnim.Press(_expandBtn);

        if (_steps != null)
        {
            if (_counterLabel != null) _counterLabel.text = _savedCounter;
            if (_titleLabel   != null) _titleLabel.text   = _savedTitle;
            RebuildObjectiveRows();
            for (int i = 0; i < _done.Length; i++)
                if (_done[i]) UpdateObjectiveRow(i, done: true, animate: false);
            _rowVersion++;
            RefreshActiveObjective();
            _objectivesList?.SetDisplay(_expanded);
            SetChevron(_expanded);
            AnimShow(_questPanel, vc_UIAnim.Enter.SlideRight);
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
        _rowVersion++;
        RefreshActiveObjective();

        _expanded = false;
        _objectivesList?.SetDisplay(false);
        SetChevron(false);

        // New quest: the card slides in from the left. Already up: a small nudge instead.
        if (!AnimShow(_questPanel, vc_UIAnim.Enter.SlideRight))
            vc_UIAnim.Punch(_titleLabel, 0.08f, 0.3f);
    }

    public void CheckObjective(int index)
    {
        if (_done == null || index < 0 || index >= _done.Length) return;
        bool wasDone = _done[index];
        if (!wasDone) vc_Sfx.Play("step_done", 0.7f);
        _done[index] = true;
        UpdateObjectiveRow(index, done: true, animate: !wasDone);

        if (!wasDone && index == _activeIndex && IsShown(_currentRow))
            AnimateActiveDone();
        else if (_pendingVersion != _rowVersion)
            RefreshActiveObjective();
        // else: the running "done -> next" animation picks the right objective when it lands
    }

    public void HideQuestInfo()
    {
        AnimHide(_questPanel, vc_UIAnim.Exit.SlideLeft);
        _rowVersion++;
        _steps = null;
        _done  = null;
    }

    public void SetCounter(string text)
    {
        if (_counterLabel != null) _counterLabel.text = text;
    }

    public void SetObjective(string text)
    {
        _rowVersion++;
        bool wasHidden = !IsShown(_currentRow) || _hiding.Contains(_currentRow);
        ResetRowVisual();
        SetCheck(_activeCheck, false);
        _activeLabel?.RemoveFromClassList("quest-active-label--done");
        if (_activeLabel != null) _activeLabel.text = text;
        if (wasHidden) vc_UIAnim.Show(_currentRow, vc_UIAnim.Enter.SlideRight);
    }

    public void HideObjective()
    {
        _rowVersion++;
        AnimHide(_currentRow, vc_UIAnim.Exit.Fade);
    }

    // Kept for API compatibility - description not shown in the compact card.
    public void SetDescription(string text) { }

    // Hints are shown through the scene's uGUI hint button/panel (vc_HintSystem),
    // not inside this compact panel.
    public void SetHints(string[] hints)
    {
        if (_hintSystem == null)
            _hintSystem = FindFirstObjectByType<vc_HintSystem>(FindObjectsInactive.Include);
        _hintSystem?.SetHints(hints);
    }

    private vc_HintSystem _hintSystem;

    public void ShowFeedback(string text)
    {
        // Town mode: feedback shows as the shared toast, not inside the quest panel.
        if (vc_RunProgressUI.Instance != null)
        {
            vc_RunProgressUI.Instance.ShowToast(text, 0f);   // stays until HideFeedback
            return;
        }
        if (_feedbackLabel == null) return;
        _feedbackLabel.text = text;
        if (!AnimShow(_feedbackLabel, vc_UIAnim.Enter.Drop))
            vc_UIAnim.Punch(_feedbackLabel, 0.06f, 0.25f);
    }

    public void HideFeedback()
    {
        if (vc_RunProgressUI.Instance != null) { vc_RunProgressUI.Instance.HideToast(); return; }
        if (_feedbackCoroutine != null) return;
        HideFeedbackLabel();
    }

    public void ForceHideFeedback()
    {
        if (vc_RunProgressUI.Instance != null) { vc_RunProgressUI.Instance.HideToast(); return; }
        if (_feedbackCoroutine != null) { StopCoroutine(_feedbackCoroutine); _feedbackCoroutine = null; }
        HideFeedbackLabel();
    }

    public void ShowFeedbackTimed(string text, float duration = 2f)
    {
        if (vc_RunProgressUI.Instance != null)
        {
            vc_RunProgressUI.Instance.ShowToast(text, Mathf.Max(0.5f, duration));
            return;
        }
        ShowFeedback(text);
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = StartCoroutine(ClearFeedbackAfter(duration));
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void OnHeaderClick(ClickEvent _) => ToggleExpand();

    private void OnExpandClick(ClickEvent e)
    {
        e.StopPropagation();
        ToggleExpand();
    }

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        SetChevron(_expanded);
        if (_objectivesList == null) return;
        if (_expanded)
        {
            vc_UIAnim.StopAll(_objectivesList);
            _hiding.Remove(_objectivesList);
            _objectivesList.style.opacity = StyleKeyword.Null;
            _objectivesList.SetDisplay(true);
            vc_UIAnim.Stagger(_objectivesList.Children(), vc_UIAnim.Enter.SlideRight);
        }
        else
        {
            AnimHide(_objectivesList, vc_UIAnim.Exit.Fade);
        }
    }

    private void SetChevron(bool up) => _chevron?.EnableInClassList("quest-chevron--up", up);

    private void RefreshActiveObjective()
    {
        if (_steps == null || _done == null) return;

        for (int i = 0; i < _steps.Length; i++)
        {
            if (!_done[i])
            {
                _activeIndex = i;
                ResetRowVisual();
                SetCheck(_activeCheck, false);
                _activeLabel?.RemoveFromClassList("quest-active-label--done");
                if (_activeLabel != null) _activeLabel.text = _steps[i];
                _currentRow?.SetDisplay(true);
                return;
            }
        }

        // All objectives done
        _activeIndex = -1;
        SetCheck(_activeCheck, true);
        _activeLabel?.RemoveFromClassList("quest-active-label--done");
        if (_activeLabel != null) _activeLabel.text = "All done!";
    }

    /// <summary>Tick + punch, strike the line, then slide the next objective in.</summary>
    private void AnimateActiveDone()
    {
        int version = ++_rowVersion;
        _pendingVersion = version;

        SetCheck(_activeCheck, true);
        vc_UIAnim.Punch(_activeCheck, 0.35f, 0.35f);
        if (_activeLabel != null)
        {
            _activeLabel.text = Strike(_activeLabel.text);
            _activeLabel.AddToClassList("quest-active-label--done");
        }

        _currentRow?.schedule.Execute(() =>
        {
            if (version != _rowVersion) return;          // the row was set by something else meanwhile
            _hiding.Add(_currentRow);
            vc_UIAnim.Hide(_currentRow, () =>
            {
                _hiding.Remove(_currentRow);
                if (version != _rowVersion) return;
                _pendingVersion = -1;
                _rowVersion++;
                RefreshActiveObjective();
                vc_UIAnim.Show(_currentRow, vc_UIAnim.Enter.SlideRight, 0f, null, 0.28f);
            }, vc_UIAnim.Exit.SlideLeft, 0f, keepLayout: true);
        }).StartingIn((long)(DoneHoldSeconds * 1000f));
    }

    /// <summary>Cancel any row motion and make the row plainly visible.</summary>
    private void ResetRowVisual()
    {
        if (_currentRow == null) return;
        vc_UIAnim.StopAll(_currentRow);
        _hiding.Remove(_currentRow);
        _currentRow.style.opacity = StyleKeyword.Null;
        _currentRow.style.scale = StyleKeyword.Null;
        _currentRow.style.translate = StyleKeyword.Null;
        _currentRow.style.visibility = Visibility.Visible;
        _currentRow.SetDisplay(true);
    }

    private void RebuildObjectiveRows()
    {
        if (_objectivesList == null) return;
        _objectivesList.Clear();

        if (_steps == null) return;

        for (int i = 0; i < _steps.Length; i++)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("quest-obj-row");
            row.name = $"obj-row-{i}";

            var check = new Label { name = $"obj-check-{i}", text = string.Empty, pickingMode = PickingMode.Ignore };
            check.AddToClassList("sn-check");
            check.AddToClassList("quest-obj-check");
            AddTick(check);

            var label = new Label { name = $"obj-text-{i}", text = _steps[i], pickingMode = PickingMode.Ignore };
            label.AddToClassList("quest-obj-text");

            row.Add(check);
            row.Add(label);
            _objectivesList.Add(row);
        }
    }

    private void UpdateObjectiveRow(int index, bool done, bool animate = false)
    {
        if (_objectivesList == null) return;

        var check = _objectivesList.Q<Label>($"obj-check-{index}");
        var label = _objectivesList.Q<Label>($"obj-text-{index}");

        SetCheck(check, done);
        if (animate && check != null && _expanded) vc_UIAnim.Punch(check, 0.35f, 0.35f);

        if (label != null)
        {
            if (_steps != null && index < _steps.Length)
                label.text = done ? Strike(_steps[index]) : _steps[index];
            label.EnableInClassList("quest-obj-text--done", done);
        }
    }

    private void HideFeedbackLabel()
    {
        if (_feedbackLabel == null) return;
        AnimHide(_feedbackLabel, vc_UIAnim.Exit.Fade, () => { if (_feedbackLabel != null) _feedbackLabel.text = string.Empty; });
    }

    private IEnumerator ClearFeedbackAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        _feedbackCoroutine = null;
        HideFeedbackLabel();
    }

    // ── tiny view helpers ─────────────────────────────────────────────────

    private static string Strike(string text) =>
        string.IsNullOrEmpty(text) || text.StartsWith("<s>") ? text : "<s>" + text + "</s>";

    private static void SetCheck(VisualElement check, bool on) => check?.EnableInClassList("sn-check--on", on);

    /// <summary>Two flat bars inside an sn-check; USS shows them when the box is on.</summary>
    private static void AddTick(VisualElement check)
    {
        if (check == null || check.Q(className: "quest-tick") != null) return;
        var tick = new VisualElement { pickingMode = PickingMode.Ignore };
        tick.AddToClassList("quest-tick");
        var a = new VisualElement { pickingMode = PickingMode.Ignore };
        a.AddToClassList("quest-tick__a");
        var b = new VisualElement { pickingMode = PickingMode.Ignore };
        b.AddToClassList("quest-tick__b");
        tick.Add(a);
        tick.Add(b);
        check.Add(tick);
    }

    private static bool IsShown(VisualElement ve)
    {
        if (ve == null) return false;
        StyleEnum<DisplayStyle> d = ve.style.display;
        return d.keyword == StyleKeyword.Undefined ? d.value == DisplayStyle.Flex : ve.resolvedStyle.display == DisplayStyle.Flex;
    }

    /// <summary>Animated show; false when it was already up (and not on its way out).</summary>
    private bool AnimShow(VisualElement ve, vc_UIAnim.Enter kind)
    {
        if (ve == null) return false;
        if (IsShown(ve) && !_hiding.Contains(ve)) return false;
        _hiding.Remove(ve);
        vc_UIAnim.Show(ve, kind);
        return true;
    }

    private void AnimHide(VisualElement ve, vc_UIAnim.Exit kind, System.Action done = null)
    {
        if (ve == null) { done?.Invoke(); return; }
        if (!IsShown(ve)) { ve.SetDisplay(false); done?.Invoke(); return; }
        if (_hiding.Contains(ve)) return;
        _hiding.Add(ve);
        vc_UIAnim.Hide(ve, () => { _hiding.Remove(ve); done?.Invoke(); }, kind);
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
