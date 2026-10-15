using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Town-mode overlay (Game_Scene_Town):
///  - the life-stage progress banner: a rail per stage with one diamond per quest, lit in
///    that quest's RIASEC colour when it's done, and milestone badges for Elementary,
///    High School, College and the disaster at the end;
///  - the end-of-day summary card ("Sleep" button);
///  - the dream screen (between days and between stages), tap to advance.
/// The tree is built in code from RunProgress.uss.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class vc_RunProgressUI : MonoBehaviour
{
    public static vc_RunProgressUI Instance { get; private set; }

    [SerializeField] private StyleSheet styleSheet;
    [Tooltip("Dream backgrounds: 0 = after Elementary, 1 = after High School, 2 = night dreams, 3 = the end.")]
    [SerializeField] private Texture2D[] dreamBackgrounds;
    [SerializeField] private float charsPerSecond = 40f;

    private static readonly string[] StageShort = { "1", "2", "3" };
    private static readonly string[] StageNames = { "Elementary", "High School", "College" };

    private VisualElement _root, _banner, _track;
    private Label _caption, _captionRight;
    private readonly List<VisualElement> _milestones = new List<VisualElement>();
    private readonly List<Label> _milestoneLabels = new List<Label>();
    private readonly List<VisualElement> _fills = new List<VisualElement>();
    private readonly List<List<VisualElement>> _pips = new List<List<VisualElement>>();
    private readonly List<Label> _stageNames = new List<Label>();

    private VisualElement _summaryDim;
    private Label _summaryTitle, _summaryBody;
    private Button _summaryBtn;

    private VisualElement _dream;
    private Label _dreamTitle, _dreamText, _dreamHint;
    private string[] _dreamLines;
    private int _dreamIndex;
    private bool _dreamTyping;
    private Action _dreamDone;
    private Coroutine _typeRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        vc_TownFlow.ProgressChanged -= Refresh;
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        _root = doc.rootVisualElement;
        _root.Clear();
        if (styleSheet != null) _root.styleSheets.Add(styleSheet);
        _root.pickingMode = PickingMode.Ignore;

        var container = new VisualElement { pickingMode = PickingMode.Ignore };
        container.AddToClassList("rp-root");
        _root.Add(container);

        BuildBanner(container);
        BuildSummary(container);
        BuildDream(container);

        vc_TownFlow.ProgressChanged -= Refresh;
        vc_TownFlow.ProgressChanged += Refresh;
    }

    private void Start() => Refresh();

    // ── banner ────────────────────────────────────────────────────────────

    private void BuildBanner(VisualElement parent)
    {
        _banner = new VisualElement { pickingMode = PickingMode.Ignore };
        _banner.AddToClassList("rp-banner");
        _banner.AddToClassList("sl-panel-wood");
        parent.Add(_banner);

        var captionRow = new VisualElement { pickingMode = PickingMode.Ignore };
        captionRow.AddToClassList("rp-caption-row");
        _caption = new Label("") { pickingMode = PickingMode.Ignore };
        _caption.AddToClassList("rp-caption");
        _captionRight = new Label("") { pickingMode = PickingMode.Ignore };
        _captionRight.AddToClassList("rp-caption-right");
        captionRow.Add(_caption);
        captionRow.Add(_captionRight);
        _banner.Add(captionRow);

        _track = new VisualElement { pickingMode = PickingMode.Ignore };
        _track.AddToClassList("rp-track");
        _banner.Add(_track);

        var names = new VisualElement { pickingMode = PickingMode.Ignore };
        names.AddToClassList("rp-stage-names");
        _banner.Add(names);
        for (int i = 0; i < StageNames.Length; i++)
        {
            var n = new Label(StageNames[i]) { pickingMode = PickingMode.Ignore };
            n.AddToClassList("rp-stage-name");
            names.Add(n);
            _stageNames.Add(n);
        }
    }

    private void RebuildTrack(vc_TownFlow flow)
    {
        _track.Clear();
        _milestones.Clear();
        _milestoneLabels.Clear();
        _fills.Clear();
        _pips.Clear();

        for (int s = 0; s < StageNames.Length; s++)
        {
            AddMilestone(StageShort[s], false);

            var seg = new VisualElement { pickingMode = PickingMode.Ignore };
            seg.AddToClassList("rp-segment");
            var rail = new VisualElement { pickingMode = PickingMode.Ignore };
            rail.AddToClassList("rp-rail");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("rp-rail-fill");
            rail.Add(fill);
            _fills.Add(fill);

            var pips = new List<VisualElement>();
            int count = flow != null ? flow.StageQuestCount(s) : 0;
            for (int q = 0; q < Mathf.Max(1, count); q++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("rp-pip");
                if (count == 0) pip.AddToClassList("rp-pip--empty-stage");
                rail.Add(pip);
                pips.Add(pip);
            }
            _pips.Add(pips);
            seg.Add(rail);
            _track.Add(seg);
        }
        AddMilestone("!", true);   // the disaster
    }

    private void AddMilestone(string text, bool storm)
    {
        var m = new VisualElement { pickingMode = PickingMode.Ignore };
        m.AddToClassList("rp-milestone");
        if (storm) m.AddToClassList("rp-milestone--storm");
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.AddToClassList("rp-milestone-label");
        m.Add(l);
        _track.Add(m);
        _milestones.Add(m);
        _milestoneLabels.Add(l);
    }

    public void Refresh()
    {
        vc_TownFlow flow = vc_TownFlow.Instance;
        if (_track == null) return;

        int expectedPips = 0;
        for (int s = 0; s < StageNames.Length; s++) expectedPips += Mathf.Max(1, flow != null ? flow.StageQuestCount(s) : 0);
        int havePips = 0;
        foreach (var p in _pips) havePips += p.Count;
        if (_pips.Count != StageNames.Length || havePips != expectedPips) RebuildTrack(flow);

        int current = flow != null ? flow.CurrentStageIndex : -1;
        int totalDone = 0, totalQuests = 0;

        for (int s = 0; s < StageNames.Length; s++)
        {
            int count = flow != null ? flow.StageQuestCount(s) : 0;
            int done = 0;
            for (int q = 0; q < count; q++)
            {
                VisualElement pip = _pips[s][q];
                string code = flow.StageQuestCode(s, q);
                string letter = string.IsNullOrEmpty(code) ? "" : code.Substring(0, 1).ToUpperInvariant();
                bool isDone = flow.StageQuestDone(s, q);
                foreach (string l in new[] { "R", "I", "A", "S", "E", "C" }) pip.RemoveFromClassList("rp-pip--" + l);
                pip.EnableInClassList("rp-pip--done", isDone);
                if (isDone) { pip.AddToClassList("rp-pip--" + letter); done++; }
            }
            totalDone += done;
            totalQuests += count;

            bool stageDone = count > 0 && done >= count && s < current;
            float pct = count > 0 ? 100f * done / count : (s < current ? 100f : 0f);
            if (s < current) pct = 100f;
            _fills[s].style.width = Length.Percent(pct);

            VisualElement badge = _milestones[s];
            badge.EnableInClassList("rp-milestone--current", s == current);
            badge.EnableInClassList("rp-milestone--done", s < current || stageDone);
            _milestoneLabels[s].text = s < current ? "+" : StageShort[s];
            _stageNames[s].EnableInClassList("rp-stage-name--current", s == current);
        }

        _milestones[_milestones.Count - 1].EnableInClassList("rp-milestone--storm-near", current >= StageNames.Length - 1);

        vc_TownStage stage = vc_TownStage.Current;
        _caption.text = stage != null ? $"{stage.stageTitle}  -  Day {stage.Day}" : "";
        _captionRight.text = $"{totalDone} / {totalQuests} helped";
    }

    // ── day summary ───────────────────────────────────────────────────────

    private void BuildSummary(VisualElement parent)
    {
        _summaryDim = new VisualElement();
        _summaryDim.AddToClassList("rp-dim");
        _summaryDim.AddToClassList("rp-hidden");
        parent.Add(_summaryDim);

        var card = new VisualElement();
        card.AddToClassList("rp-card");
        card.AddToClassList("sl-panel-cream");
        _summaryDim.Add(card);

        _summaryTitle = new Label();
        _summaryTitle.AddToClassList("rp-card-title");
        _summaryBody = new Label();
        _summaryBody.AddToClassList("rp-card-body");
        _summaryBtn = new Button();
        _summaryBtn.AddToClassList("rp-card-btn");
        _summaryBtn.AddToClassList("sl-button-wide");
        var btnLabel = new Label("Sleep");
        btnLabel.AddToClassList("rp-card-btn-label");
        _summaryBtn.Add(btnLabel);
        card.Add(_summaryTitle);
        card.Add(_summaryBody);
        card.Add(_summaryBtn);
    }

    /// <summary>End-of-day card. "Sleep" plays the night's dream (if any), then calls onWake.</summary>
    public void ShowDaySummary(string title, string body, string dreamLine, Action onWake)
    {
        SetPlayerFrozen(true);
        _summaryTitle.text = title;
        _summaryBody.text = body;
        _summaryDim.RemoveFromClassList("rp-hidden");

        _summaryBtn.clickable = new Clickable(() =>
        {
            _summaryDim.AddToClassList("rp-hidden");
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            if (!string.IsNullOrEmpty(dreamLine))
                PlayDream("That night...", new[] { dreamLine }, 2, () => { SetPlayerFrozen(false); onWake?.Invoke(); });
            else
            {
                SetPlayerFrozen(false);
                onWake?.Invoke();
            }
        });
    }

    // ── dream ─────────────────────────────────────────────────────────────

    private void BuildDream(VisualElement parent)
    {
        _dream = new VisualElement();
        _dream.AddToClassList("rp-dream");
        _dream.AddToClassList("rp-hidden");
        parent.Add(_dream);

        _dreamTitle = new Label();
        _dreamTitle.AddToClassList("rp-dream-title");
        _dreamText = new Label();
        _dreamText.AddToClassList("rp-dream-text");
        _dreamHint = new Label("tap to continue");
        _dreamHint.AddToClassList("rp-dream-hint");
        _dream.Add(_dreamTitle);
        _dream.Add(_dreamText);
        _dream.Add(_dreamHint);

        _dream.RegisterCallback<PointerUpEvent>(_ => AdvanceDream());
    }

    /// <summary>Full-screen dream: lines are typed out, tap to skip / advance.</summary>
    public void PlayDream(string title, string[] lines, int background, Action onDone)
    {
        SetPlayerFrozen(true);
        _dreamLines = lines ?? new string[0];
        _dreamIndex = 0;
        _dreamDone = onDone;
        _dreamTitle.text = title;

        Texture2D bg = dreamBackgrounds != null && background >= 0 && background < dreamBackgrounds.Length
            ? dreamBackgrounds[background] : null;
        _dream.style.backgroundImage = bg != null ? new StyleBackground(bg) : new StyleBackground(StyleKeyword.None);

        _dream.RemoveFromClassList("rp-hidden");
        _dream.schedule.Execute(() => _dream.AddToClassList("rp-dream--shown")).StartingIn(20);
        ShowDreamLine();
    }

    private void ShowDreamLine()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        string line = _dreamIndex < _dreamLines.Length ? _dreamLines[_dreamIndex] : "";
        _typeRoutine = StartCoroutine(TypeLine(line));
    }

    private IEnumerator TypeLine(string line)
    {
        _dreamTyping = true;
        _dreamHint.visible = false;
        for (int i = 1; i <= line.Length; i++)
        {
            _dreamText.text = line.Substring(0, i);
            yield return new WaitForSecondsRealtime(1f / Mathf.Max(1f, charsPerSecond));
        }
        _dreamTyping = false;
        _dreamHint.visible = true;
    }

    private void AdvanceDream()
    {
        if (_dreamLines == null) return;
        if (_dreamTyping)
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _dreamText.text = _dreamIndex < _dreamLines.Length ? _dreamLines[_dreamIndex] : "";
            _dreamTyping = false;
            _dreamHint.visible = true;
            return;
        }

        _dreamIndex++;
        if (_dreamIndex < _dreamLines.Length)
        {
            ShowDreamLine();
            return;
        }

        _dreamLines = null;
        _dream.RemoveFromClassList("rp-dream--shown");
        _dream.schedule.Execute(() =>
        {
            _dream.AddToClassList("rp-hidden");
            SetPlayerFrozen(false);
            Action done = _dreamDone;
            _dreamDone = null;
            done?.Invoke();
        }).StartingIn(800);
    }

    /// <summary>The banner hides during quests so it doesn't crowd the quest UI.</summary>
    public void SetBannerVisible(bool visible) => _banner?.EnableInClassList("rp-hidden", !visible);

    public bool IsShowingOverlay =>
        (_dream != null && !_dream.ClassListContains("rp-hidden")) ||
        (_summaryDim != null && !_summaryDim.ClassListContains("rp-hidden"));

    private static void SetPlayerFrozen(bool frozen)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        if (frozen) player.MoveAction.Disable();
        else player.MoveAction.Enable();
    }
}
