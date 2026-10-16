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
    private RiasecChart _summaryChart;

    private bool _hudVisible = true;
    private bool _bannerOpen;

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
        BuildRing(container);
        BuildSideControls(container);
        BuildToast(container);
        BuildSummary(container);
        BuildQuestDone(container);
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
        _banner.AddToClassList("rp-hidden");      // opened by tapping the progress ring
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
        _captionRight.text = "";
        UpdateRing(flow, stage, current);
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
        _summaryChart = new RiasecChart();
        _summaryChart.AddToClassList("rp-chart");
        var chartCaption = new Label("Your interests so far");
        chartCaption.AddToClassList("rp-chart-caption");

        card.Add(_summaryTitle);
        card.Add(_summaryBody);
        card.Add(chartCaption);
        card.Add(_summaryChart);
        card.Add(_summaryBtn);
    }

    /// <summary>
    /// End-of-day card. "Sleep" runs <paramref name="beforeDream"/> (e.g. the skill pick; it
    /// gets the continuation to call when done), then plays the night's dream (if any), then
    /// calls onWake.
    /// </summary>
    public void ShowDaySummary(string title, string body, string dreamLine, Action onWake, Action<Action> beforeDream = null)
    {
        SetPlayerFrozen(true);
        _summaryTitle.text = title;
        _summaryBody.text = body;
        _summaryChart.SetValues(LetterScores());
        _summaryDim.RemoveFromClassList("rp-hidden");
        PopIn(_summaryDim.Q(className: "rp-card"));

        _summaryBtn.clickable = new Clickable(() =>
        {
            _summaryDim.AddToClassList("rp-hidden");
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);

            Action dream = () =>
            {
                if (!string.IsNullOrEmpty(dreamLine))
                    PlayDream("That night...", new[] { dreamLine }, 2, () => { SetPlayerFrozen(false); onWake?.Invoke(); });
                else
                {
                    SetPlayerFrozen(false);
                    onWake?.Invoke();
                }
            };

            if (beforeDream != null) beforeDream(dream);
            else dream();
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

    /// <summary>
    /// The progress HUD (corner ring; the full banner opens when the ring is tapped) hides
    /// during quests so it doesn't crowd the quest UI.
    /// </summary>
    public void SetBannerVisible(bool visible)
    {
        _hudVisible = visible;
        _ring?.EnableInClassList("rp-hidden", !visible);
        _banner?.EnableInClassList("rp-hidden", !(visible && _bannerOpen));
    }

    // ── "next stage" button (shown when nothing is left to do in the town) ──

    private Button _nextBtn;
    private Label _nextLabel;

    public void ShowNextStageButton(string label, Action onClick)
    {
        if (_nextBtn == null)
        {
            _nextBtn = new Button();
            _nextBtn.AddToClassList("rp-next-btn");
            _nextBtn.AddToClassList("sl-button-wide");
            _nextLabel = new Label();
            _nextLabel.AddToClassList("rp-card-btn-label");
            _nextBtn.Add(_nextLabel);
            _root.Q(className: "rp-root")?.Add(_nextBtn);
        }
        _nextLabel.text = label;
        _nextBtn.clickable = new Clickable(() => onClick?.Invoke());
        _nextBtn.RemoveFromClassList("rp-hidden");
    }

    public void HideNextStageButton() => _nextBtn?.AddToClassList("rp-hidden");

    // ── side controls: energy, Sleep, camera zoom, quest "Not for me" ─────

    private VisualElement _side;
    private Label _energyLabel;
    private VisualElement _energyChip;
    private Button _sleepBtn;
    private Button _cancelQuestBtn;

    private void BuildSideControls(VisualElement parent)
    {
        _side = new VisualElement { pickingMode = PickingMode.Ignore };
        _side.AddToClassList("rp-side");
        parent.Add(_side);

        // chip = row of [bolt icon][label]; a Label with children stops sizing to its text
        _energyChip = new VisualElement { pickingMode = PickingMode.Ignore };
        _energyChip.AddToClassList("rp-energy");
        _energyChip.AddToClassList("sl-chip");
        _energyChip.AddToClassList("rp-hidden");
        var bolt = new VisualElement { pickingMode = PickingMode.Ignore };
        bolt.AddToClassList("rp-energy-icon");
        _energyChip.Add(bolt);
        _energyLabel = new Label("") { pickingMode = PickingMode.Ignore };
        _energyLabel.AddToClassList("rp-energy-label");
        _energyChip.Add(_energyLabel);
        _side.Add(_energyChip);

        _sleepBtn = MakeSideButton("Sleep", "rp-side-btn", () =>
        {
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            vc_TownStage.Current?.RequestSleep();
        });
        _sleepBtn.AddToClassList("rp-hidden");
        _side.Add(_sleepBtn);

        _cancelQuestBtn = MakeSideButton("Not for me", "rp-side-btn", null);
        _cancelQuestBtn.AddToClassList("rp-hidden");
        _side.Add(_cancelQuestBtn);

        var zoomRow = new VisualElement { pickingMode = PickingMode.Ignore };
        zoomRow.AddToClassList("rp-zoom-row");
        zoomRow.Add(MakeSideButton("-", "rp-zoom-btn", () => vc_CameraZoom.Instance?.Step(+1)));   // zoom out
        zoomRow.Add(MakeSideButton("+", "rp-zoom-btn", () => vc_CameraZoom.Instance?.Step(-1)));   // zoom in
        _side.Add(zoomRow);
    }

    private static Button MakeSideButton(string text, string cls, Action onClick)
    {
        var btn = new Button();
        btn.AddToClassList(cls);
        btn.AddToClassList("sl-button-wide");
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("rp-card-btn-label");
        btn.Add(label);
        if (onClick != null) btn.clickable = new Clickable(onClick);
        return btn;
    }

    public void SetEnergy(int energy, int max)
    {
        if (_energyLabel == null) return;
        _energyLabel.text = $"{energy}/{max}";
        _energyChip?.RemoveFromClassList("rp-hidden");
    }

    public void SetSleepVisible(bool visible) => _sleepBtn?.EnableInClassList("rp-hidden", !visible);

    /// <summary>Shows the in-quest "Not for me" button.</summary>
    public void ShowQuestCancel(Action onClick)
    {
        if (_cancelQuestBtn == null) return;
        _cancelQuestBtn.clickable = new Clickable(() => onClick?.Invoke());
        _cancelQuestBtn.RemoveFromClassList("rp-hidden");
        _energyChip?.AddToClassList("rp-hidden");
    }

    public void HideQuestCancel()
    {
        _cancelQuestBtn?.AddToClassList("rp-hidden");
        _energyChip?.RemoveFromClassList("rp-hidden");
    }

    public bool IsShowingOverlay =>
        (_dream != null && !_dream.ClassListContains("rp-hidden")) ||
        (_summaryDim != null && !_summaryDim.ClassListContains("rp-hidden"));

    // ── progress ring (top-right corner; tap to open the full banner) ─────

    private ProgressRing _ring;
    private Label _ringStage, _ringDay;
    private Button _partialBtn;

    private void BuildRing(VisualElement parent)
    {
        _ring = new ProgressRing();
        _ring.AddToClassList("rp-ring");
        _ringStage = new Label("1") { pickingMode = PickingMode.Ignore };
        _ringStage.AddToClassList("rp-ring-stage");
        _ringDay = new Label("") { pickingMode = PickingMode.Ignore };
        _ringDay.AddToClassList("rp-ring-day");
        _ring.Add(_ringStage);
        _ring.Add(_ringDay);
        _ring.RegisterCallback<PointerUpEvent>(_ => ToggleBanner());
        parent.Add(_ring);

        // Tapping the open banner closes it again.
        _banner.pickingMode = PickingMode.Position;
        _banner.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.target is VisualElement ve && _partialBtn != null && (_partialBtn == ve || _partialBtn.Contains(ve))) return;
            ToggleBanner();
        });

        // Partial results: inside the banner, shown once half the run's quests are answered.
        _partialBtn = MakeSideButton("See my results so far", "rp-partial-btn", RequestPartialResults);
        _partialBtn.AddToClassList("rp-hidden");
        _banner.Add(_partialBtn);
    }

    private void ToggleBanner()
    {
        _bannerOpen = !_bannerOpen;
        SunodGame.Core.vc_Sfx.Play("click", 0.5f);
        _banner.EnableInClassList("rp-hidden", !(_hudVisible && _bannerOpen));
        if (_bannerOpen) PopIn(_banner);
    }

    private void UpdateRing(vc_TownFlow flow, vc_TownStage stage, int current)
    {
        if (_ring == null) return;

        int built = 0, resolved = 0;
        if (stage != null)
        {
            foreach (vc_TownNPC npc in stage.GetComponentsInChildren<vc_TownNPC>(true))
            {
                if (!npc.HasQuest) continue;
                built++;
                if (npc.Attempted || npc.Gone) resolved++;
            }
        }
        _ring.Progress = built > 0 ? (float)resolved / built : 0f;
        _ring.StageIndex = Mathf.Max(0, current);
        _ringStage.text = current >= 0 ? StageShort[Mathf.Min(current, StageShort.Length - 1)] : "-";
        _ringDay.text = stage != null ? $"Day {stage.Day}" : "";

        // Partial results once half of all built quests (all stages) have been answered.
        int total = 0, answered = 0;
        if (flow != null)
            for (int s = 0; s < StageNames.Length; s++)
            {
                int n = flow.StageQuestCount(s);
                total += n;
                for (int q = 0; q < n; q++)
                    if (vc_TownFlow.IsQuestDone(flow.Stages[s].questIds[q])) answered++;
            }
        bool lastStage = current >= StageNames.Length - 1;
        _partialAnswered = answered;
        _partialTotal = total;
        _partialBtn?.EnableInClassList("rp-hidden", total == 0 || answered * 2 < total || (lastStage && stage != null && stage.StageDone));
    }

    private int _partialAnswered, _partialTotal;

    private void RequestPartialResults()
    {
        if (vc_DialogPanel.Instance == null) return;
        _bannerOpen = false;
        _banner.AddToClassList("rp-hidden");
        vc_DialogPanel.Instance.ShowChoice(
            "See results now?",
            $"This ends your run. Your results will be based on {_partialAnswered} of {_partialTotal} quests, so they are less certain than a full run.",
            null,
            "See results",
            "Keep playing",
            onConfirm: () =>
            {
                SunodGame.Core.GameSessionData.partialRunNote =
                    $"Partial result: based on {_partialAnswered} of {_partialTotal} quests. Finish a full run for a surer result.";
                vc_QuestHUD.Instance?.HideQuestInfo();
                vc_EndRunUI ui = vc_EndRunUI.Instance ?? FindFirstObjectByType<vc_EndRunUI>();
                if (ui != null) ui.SubmitNow();
                else SunodGame.Core.SceneLoader.GoToEnd();
            },
            onCancel: null);
    }

    // ── toast (replaces the old feedback text) ────────────────────────────

    private Label _toast;
    private IVisualElementScheduledItem _toastHide;

    private void BuildToast(VisualElement parent)
    {
        _toast = new Label("") { pickingMode = PickingMode.Ignore };
        _toast.AddToClassList("rp-toast");
        _toast.AddToClassList("sl-chip");
        _toast.AddToClassList("rp-toast--hidden");
        parent.Add(_toast);
    }

    /// <summary>Short message at the top of the screen. duration &lt;= 0 keeps it until HideToast.</summary>
    public void ShowToast(string text, float duration = 2f)
    {
        if (_toast == null) return;
        if (string.IsNullOrEmpty(text)) { HideToast(); return; }
        _toast.text = text;
        _toast.RemoveFromClassList("rp-toast--hidden");
        _toastHide?.Pause();
        _toastHide = duration > 0f ? _toast.schedule.Execute(HideToast).StartingIn((long)(duration * 1000f)) : null;
    }

    public void HideToast()
    {
        _toastHide?.Pause();
        _toastHide = null;
        _toast?.AddToClassList("rp-toast--hidden");
    }

    // ── quest-done panel ──────────────────────────────────────────────────

    private VisualElement _doneDim;
    private Label _doneTitle, _doneName, _doneNote;
    private readonly List<VisualElement> _doneStars = new List<VisualElement>();
    private Button _doneBtn;
    private Action _doneContinue;

    private void BuildQuestDone(VisualElement parent)
    {
        _doneDim = new VisualElement();
        _doneDim.AddToClassList("rp-dim");
        _doneDim.AddToClassList("rp-hidden");
        parent.Add(_doneDim);

        var card = new VisualElement();
        card.AddToClassList("rp-card");
        card.AddToClassList("rp-done-card");
        card.AddToClassList("sl-panel-cream");
        _doneDim.Add(card);

        var ribbon = new VisualElement();
        ribbon.AddToClassList("rp-done-ribbon");
        ribbon.AddToClassList("sl-panel-wood");
        _doneTitle = new Label("Quest complete!");
        _doneTitle.AddToClassList("rp-done-title");
        ribbon.Add(_doneTitle);
        card.Add(ribbon);

        _doneName = new Label();
        _doneName.AddToClassList("rp-done-name");
        card.Add(_doneName);

        var stars = new VisualElement();
        stars.AddToClassList("rp-done-stars");
        for (int i = 0; i < 5; i++)
        {
            var s = new VisualElement();
            s.AddToClassList("rp-star");
            stars.Add(s);
            _doneStars.Add(s);
        }
        card.Add(stars);

        _doneNote = new Label();
        _doneNote.AddToClassList("rp-done-note");
        card.Add(_doneNote);

        _doneBtn = MakeSideButton("Back to town", "rp-card-btn", () =>
        {
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            _doneDim.AddToClassList("rp-hidden");
            SetPlayerFrozen(false);
            Action cb = _doneContinue;
            _doneContinue = null;
            cb?.Invoke();
        });
        card.Add(_doneBtn);
    }

    /// <summary>Town version of the quest-done popup: stars pop in one by one, then "Back to town".</summary>
    public void ShowQuestDone(string questName, int stars, Action onContinue)
    {
        SetPlayerFrozen(true);
        _doneContinue = onContinue;
        _doneName.text = questName;
        stars = Mathf.Clamp(stars, 0, 5);
        _doneNote.text = stars >= 5 ? "Perfect!" : stars >= 3 ? "Nice work!" : "You did it!";

        for (int i = 0; i < _doneStars.Count; i++)
        {
            VisualElement s = _doneStars[i];
            s.RemoveFromClassList("rp-star--full");
            s.RemoveFromClassList("rp-star--shown");
            if (i < stars) s.AddToClassList("rp-star--full");
            int idx = i;
            s.schedule.Execute(() =>
            {
                s.AddToClassList("rp-star--shown");
                if (idx < stars) SunodGame.Core.vc_Sfx.Play("step_done", 0.5f, 0.08f);
            }).StartingIn(250 + 160 * i);
        }

        _doneDim.RemoveFromClassList("rp-hidden");
        PopIn(_doneDim.Q(className: "rp-card"));
    }

    // ── juice helpers ─────────────────────────────────────────────────────

    /// <summary>Little scale-up pop for a panel that just appeared.</summary>
    private static void PopIn(VisualElement el)
    {
        if (el == null) return;
        el.AddToClassList("rp-pop-start");
        el.schedule.Execute(() => el.RemoveFromClassList("rp-pop-start")).StartingIn(16);
    }

    // ── RIASEC scores (live chart + "leans toward" teaser) ────────────────

    private static readonly string[] Letters = { "R", "I", "A", "S", "E", "C" };
    private static readonly string[] LetterNames = { "Realistic", "Investigative", "Artistic", "Social", "Enterprising", "Conventional" };

    /// <summary>
    /// Per-letter mean of the quest item scores recorded so far (1-5; a refused quest is 1),
    /// 3 for a letter with nothing recorded yet. Same numbers the model will see.
    /// </summary>
    public static float[] LetterScores()
    {
        var sum = new float[6];
        var count = new int[6];
        vc_SessionTelemetry t = vc_SessionTelemetry.Instance;
        if (t != null)
        {
            var latest = new Dictionary<string, vc_SessionTelemetry.QuestRecord>();
            foreach (var r in t.GetAllRecords())
                if (r != null && !string.IsNullOrEmpty(r.questionCode)) latest[r.questionCode] = r;
            foreach (var r in latest.Values)
            {
                int i = Array.IndexOf(Letters, r.questionCode.Substring(0, 1).ToUpperInvariant());
                if (i < 0) continue;
                sum[i] += Mathf.Clamp(r.itemScore, 1, 5);
                count[i]++;
            }
        }
        var result = new float[6];
        for (int i = 0; i < 6; i++) result[i] = count[i] > 0 ? sum[i] / count[i] : 3f;
        return result;
    }

    /// <summary>"Realistic and Social", or null when nothing stands out yet.</summary>
    public static string LeaningText()
    {
        float[] s = LetterScores();
        var order = new List<int> { 0, 1, 2, 3, 4, 5 };
        order.Sort((a, b) => s[b].CompareTo(s[a]));
        if (s[order[0]] <= 3.05f) return null;
        if (s[order[1]] > 3.05f && s[order[0]] - s[order[1]] < 0.75f)
            return $"{LetterNames[order[0]]} and {LetterNames[order[1]]}";
        return LetterNames[order[0]];
    }

    /// <summary>Hexagon radar of the six RIASEC scores, drawn with Painter2D.</summary>
    public class RiasecChart : VisualElement
    {
        private static readonly Color[] LetterColors =
        {
            new Color32(217, 58, 74, 255), new Color32(240, 138, 36, 255), new Color32(142, 79, 214, 255),
            new Color32(47, 163, 90, 255), new Color32(47, 111, 214, 255), new Color32(34, 179, 196, 255),
        };
        private float[] _values = { 3, 3, 3, 3, 3, 3 };
        private readonly Label[] _labels = new Label[6];

        public RiasecChart()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            for (int i = 0; i < 6; i++)
            {
                _labels[i] = new Label(Letters[i]) { pickingMode = PickingMode.Ignore };
                _labels[i].AddToClassList("rp-chart-label");
                _labels[i].style.color = LetterColors[i];
                Add(_labels[i]);
            }
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public void SetValues(float[] values)
        {
            if (values == null || values.Length != 6) return;
            _values = (float[])values.Clone();
            MarkDirtyRepaint();
        }

        private Vector2 Point(int i, float r)
        {
            Rect rc = contentRect;
            float a = Mathf.Deg2Rad * (-90f + 60f * i);
            return rc.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        private float Radius => Mathf.Min(contentRect.width, contentRect.height) * 0.5f - 22f;

        private void PlaceLabels()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = Point(i, Radius + 14f);
                _labels[i].style.left = p.x - 10f;
                _labels[i].style.top = p.y - 12f;
            }
        }

        private void Draw(MeshGenerationContext ctx)
        {
            float R = Radius;
            if (R <= 4f) return;
            Painter2D p = ctx.painter2D;

            // rings at scores 1..5
            for (int ring = 5; ring >= 1; ring--)
            {
                float rr = R * ring / 5f;
                p.BeginPath();
                for (int i = 0; i < 6; i++) { if (i == 0) p.MoveTo(Point(i, rr)); else p.LineTo(Point(i, rr)); }
                p.ClosePath();
                if (ring == 5) { p.fillColor = new Color32(250, 236, 205, 255); p.Fill(); }
                p.strokeColor = new Color32(180, 150, 115, 255);
                p.lineWidth = ring == 3 ? 2f : 1f;
                p.Stroke();
            }
            for (int i = 0; i < 6; i++)
            {
                p.BeginPath(); p.MoveTo(Point(i, 0f)); p.LineTo(Point(i, R));
                p.strokeColor = new Color32(180, 150, 115, 255); p.lineWidth = 1f; p.Stroke();
            }

            // the player's shape
            p.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                Vector2 v = Point(i, R * Mathf.Clamp(_values[i], 1f, 5f) / 5f);
                if (i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
            p.fillColor = new Color32(255, 180, 60, 120);
            p.Fill();
            p.strokeColor = new Color32(168, 92, 30, 255);
            p.lineWidth = 3f;
            p.Stroke();

            for (int i = 0; i < 6; i++)
            {
                Vector2 v = Point(i, R * Mathf.Clamp(_values[i], 1f, 5f) / 5f);
                p.BeginPath();
                p.Arc(v, 5f, 0f, 360f);
                p.fillColor = LetterColors[i];
                p.Fill();
            }
        }
    }

    /// <summary>Circular progress for the current stage, drawn with Painter2D.</summary>
    public class ProgressRing : VisualElement
    {
        private static readonly Color[] StageColors =
        {
            new Color32(96, 180, 90, 255), new Color32(255, 180, 60, 255), new Color32(120, 150, 230, 255),
        };
        private float _progress;
        public int StageIndex { get; set; }
        public float Progress
        {
            get => _progress;
            set { _progress = Mathf.Clamp01(value); MarkDirtyRepaint(); }
        }

        public ProgressRing()
        {
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect rc = contentRect;
            float r = Mathf.Min(rc.width, rc.height) * 0.5f - 7f;
            if (r <= 4f) return;
            Painter2D p = ctx.painter2D;

            p.BeginPath();
            p.Arc(rc.center, r + 5f, 0f, 360f);
            p.fillColor = new Color32(250, 236, 205, 255);
            p.Fill();
            p.strokeColor = new Color32(46, 34, 24, 255);
            p.lineWidth = 3f;
            p.Stroke();

            p.lineWidth = 8f;
            p.BeginPath();
            p.Arc(rc.center, r - 2f, 0f, 360f);
            p.strokeColor = new Color32(120, 98, 82, 255);
            p.Stroke();

            if (_progress > 0.001f)
            {
                p.BeginPath();
                p.Arc(rc.center, r - 2f, -90f, -90f + 360f * _progress);
                p.strokeColor = StageColors[Mathf.Clamp(StageIndex, 0, StageColors.Length - 1)];
                p.lineCap = LineCap.Round;
                p.Stroke();
            }
        }
    }

    private static void SetPlayerFrozen(bool frozen)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        if (frozen) player.MoveAction.Disable();
        else player.MoveAction.Enable();
    }
}
