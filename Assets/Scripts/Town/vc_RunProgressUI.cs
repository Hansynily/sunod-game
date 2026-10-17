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
    private Label _bannerDay;

    // one card per life stage
    private readonly List<VisualElement> _stageCards = new List<VisualElement>();
    private readonly List<Label> _stageBadges = new List<Label>();
    private readonly List<Label> _stageCounts = new List<Label>();
    private readonly List<VisualElement> _stageBars = new List<VisualElement>();
    private readonly List<List<VisualElement>> _pips = new List<List<VisualElement>>();
    private VisualElement _stormCard;

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

    // ── banner: "Your life stages" ────────────────────────────────────────
    // Header (title + day chip), then one card per stage: number badge + name, a flush
    // segmented bar (one segment per quest, lit in that quest's RIASEC colour once done)
    // and a short status line. A small storm card at the end is the disaster.

    private void BuildBanner(VisualElement parent)
    {
        _banner = new VisualElement { pickingMode = PickingMode.Ignore };
        _banner.AddToClassList("rp-banner");
        _banner.AddToClassList("sl-panel-cream");
        _banner.AddToClassList("rp-hidden");      // opened by tapping the progress ring
        parent.Add(_banner);

        var header = new VisualElement { pickingMode = PickingMode.Ignore };
        header.AddToClassList("rp-banner-header");
        var title = new Label("Your life stages") { pickingMode = PickingMode.Ignore };
        title.AddToClassList("rp-banner-title");
        _bannerDay = new Label("") { pickingMode = PickingMode.Ignore };
        _bannerDay.AddToClassList("rp-banner-day");
        _bannerDay.AddToClassList("sl-chip");
        header.Add(title);
        header.Add(_bannerDay);
        _banner.Add(header);

        _track = new VisualElement { pickingMode = PickingMode.Ignore };
        _track.AddToClassList("rp-stages");
        _banner.Add(_track);
    }

    private void RebuildTrack(vc_TownFlow flow)
    {
        _track.Clear();
        _stageCards.Clear();
        _stageBadges.Clear();
        _stageCounts.Clear();
        _stageBars.Clear();
        _pips.Clear();

        for (int s = 0; s < StageNames.Length; s++)
        {
            if (s > 0)
            {
                var arrow = new Label(">") { pickingMode = PickingMode.Ignore };
                arrow.AddToClassList("rp-stage-arrow");
                _track.Add(arrow);
            }

            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("rp-stage");

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("rp-stage-top");
            var badge = new Label(StageShort[s]) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("rp-stage-badge");
            var name = new Label(StageNames[s]) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("rp-stage-name");
            top.Add(badge);
            top.Add(name);
            card.Add(top);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("rp-stage-bar");
            var pips = new List<VisualElement>();
            int count = flow != null ? flow.StageQuestCount(s) : 0;
            for (int q = 0; q < Mathf.Max(1, count); q++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("rp-seg");
                bar.Add(pip);
                pips.Add(pip);
            }
            card.Add(bar);

            var countLabel = new Label("") { pickingMode = PickingMode.Ignore };
            countLabel.AddToClassList("rp-stage-count");
            card.Add(countLabel);

            _track.Add(card);
            _stageCards.Add(card);
            _stageBadges.Add(badge);
            _stageCounts.Add(countLabel);
            _stageBars.Add(bar);
            _pips.Add(pips);
        }

        _stormCard = new VisualElement { pickingMode = PickingMode.Ignore };
        _stormCard.AddToClassList("rp-storm");
        var bolt = new Label("!") { pickingMode = PickingMode.Ignore };
        bolt.AddToClassList("rp-storm-label");
        _stormCard.Add(bolt);
        _track.Add(_stormCard);
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

        for (int s = 0; s < StageNames.Length; s++)
        {
            int count = flow != null ? flow.StageQuestCount(s) : 0;
            int done = 0;
            for (int q = 0; q < _pips[s].Count; q++)
            {
                VisualElement pip = _pips[s][q];
                foreach (string l in new[] { "R", "I", "A", "S", "E", "C" }) pip.RemoveFromClassList("rp-seg--" + l);
                bool isDone = q < count && flow.StageQuestDone(s, q);
                pip.EnableInClassList("rp-seg--done", isDone);
                if (!isDone) continue;
                string code = flow.StageQuestCode(s, q);
                if (!string.IsNullOrEmpty(code)) pip.AddToClassList("rp-seg--" + code.Substring(0, 1).ToUpperInvariant());
                done++;
            }

            bool isCurrent = s == current;
            bool isPast = current >= 0 && s < current;
            _stageCards[s].EnableInClassList("rp-stage--current", isCurrent);
            _stageCards[s].EnableInClassList("rp-stage--done", isPast);
            _stageCards[s].EnableInClassList("rp-stage--future", !isCurrent && !isPast);
            _stageBadges[s].text = isPast ? "v" : StageShort[s];

            _stageCounts[s].text = isPast ? $"Done - {done} helped"
                : isCurrent ? $"{done} of {count} helped"
                : "Up next";
        }

        _stormCard?.EnableInClassList("rp-storm--near", current >= StageNames.Length - 1);

        vc_TownStage stage = vc_TownStage.Current;
        _bannerDay.text = stage != null ? $"Day {stage.Day} of {stage.daysPerStage}" : "";
        _bannerDay.EnableInClassList("rp-hidden", stage == null);
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
        SunodGame.Core.vc_Music.SetMuted(true);   // dreams play in silence; the next cue fades in after
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
            // after done: if it moved to the next town / results, that scene's track is the cue.
            // (done may have started another dream right away - then stay silent.)
            if (_dreamLines == null) SunodGame.Core.vc_Music.SetMuted(false);
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
        SetGlow(_nextBtn, true);
    }

    public void HideNextStageButton()
    {
        _nextBtn?.AddToClassList("rp-hidden");
        SetGlow(_nextBtn, false);
    }

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
        Button zoomOut = MakeSideButton("-", "rp-zoom-btn", null);
        Button zoomIn = MakeSideButton("+", "rp-zoom-btn", null);
        zoomRow.Add(zoomOut);
        zoomRow.Add(zoomIn);
        vc_CameraZoom.BindButtons(zoomOut, zoomIn);
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

    /// <summary>Sleep pulses when the day is used up (no energy, or nobody left asking).</summary>
    public void SetSleepGlow(bool glow) => SetGlow(_sleepBtn, glow);

    // ── "do this now" glow: the button breathes between two states ─────────
    private readonly System.Collections.Generic.List<VisualElement> _glowing = new System.Collections.Generic.List<VisualElement>();
    private IVisualElementScheduledItem _glowTick;
    private bool _glowPhase;

    private void SetGlow(VisualElement el, bool on)
    {
        if (el == null) return;
        if (on && !_glowing.Contains(el)) _glowing.Add(el);
        if (!on) { _glowing.Remove(el); el.RemoveFromClassList("rp-glow"); el.RemoveFromClassList("rp-glow--hi"); }
        else el.AddToClassList("rp-glow");
        if (_glowTick == null && _glowing.Count > 0)
            _glowTick = el.schedule.Execute(() =>
            {
                _glowPhase = !_glowPhase;
                foreach (var g in _glowing) g.EnableInClassList("rp-glow--hi", _glowPhase);
            }).Every(550);
    }

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
    private Label _ringStage, _ringDay, _ringName;
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
        // life-stage name under the ring, so "1 / 2 / 3" is never the only hint
        _ringName = new Label("") { pickingMode = PickingMode.Ignore };
        _ringName.AddToClassList("rp-ring-name");
        _ringName.AddToClassList("sl-chip");
        _ring.Add(_ringName);
        _ring.RegisterCallback<PointerUpEvent>(_ => ToggleBanner());
        parent.Add(_ring);

        // Tapping the open banner closes it again.
        _banner.pickingMode = PickingMode.Position;
        _banner.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.target is VisualElement ve && _partialBtn != null && (_partialBtn == ve || _partialBtn.Contains(ve))) return;
            ToggleBanner();
        });

        // Leaning-career peek: inside the banner, shown once half the run's quests are answered.
        _partialBtn = MakeSideButton("My leaning career", "rp-partial-btn", RequestPartialResults);
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
        if (_ringName != null)
        {
            _ringName.text = current >= 0 ? StageNames[Mathf.Min(current, StageNames.Length - 1)] : "";
            _ringName.EnableInClassList("rp-hidden", current < 0);
        }

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

    private bool _previewBusy;

    /// <summary>
    /// "My leaning career": a peek at where the answers point so far. It never ends the run;
    /// the real result still comes at the end of College.
    /// </summary>
    private void RequestPartialResults()
    {
        if (vc_DialogPanel.Instance == null || _previewBusy) return;
        _bannerOpen = false;
        _banner.AddToClassList("rp-hidden");
        SunodGame.Core.vc_Sfx.Play("click", 0.6f);

        string leaning = LeaningText();
        string basis = $"Based on {_partialAnswered} of {_partialTotal} people so far. This is only a peek - keep playing, your answers can still change it.";

        vc_SessionTelemetry t = vc_SessionTelemetry.Instance;
        if (t == null)
        {
            ShowLeaning(null, leaning, basis);
            return;
        }

        _previewBusy = true;
        ShowToast("Checking your path...", 0f);
        StartCoroutine(t.PreviewPrediction(result =>
        {
            _previewBusy = false;
            HideToast();
            ShowLeaning(result, leaning, basis);
        }));
    }

    private static void ShowLeaning(SunodGame.Models.PredictionResponsePayload result, string leaning, string basis)
    {
        string career = result != null ? result.career_result : null;
        string family = result != null ? result.career_family : null;
        var body = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(career) && !career.StartsWith("Cluster"))
        {
            body.Append($"You are leaning toward: {career}");
            if (!string.IsNullOrWhiteSpace(family)) body.Append($" ({family})");
            body.Append(".\n\n");
        }
        else if (!string.IsNullOrWhiteSpace(family))
        {
            body.Append($"You are leaning toward the {family} path.\n\n");
        }
        if (!string.IsNullOrEmpty(leaning)) body.Append($"Your strongest interests: {leaning}.\n\n");
        if (body.Length == 0) body.Append("Nothing stands out yet - help a few more people first.\n\n");
        body.Append(basis);
        vc_DialogPanel.Instance?.ShowMessage("Your path so far", body.ToString(), null);
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
        ribbon.AddToClassList("sl-ribbon");
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
            p.strokeColor = new Color32(160, 118, 84, 255);
            p.lineWidth = 2f;
            p.Stroke();

            p.lineWidth = 8f;
            p.BeginPath();
            p.Arc(rc.center, r - 2f, 0f, 360f);
            p.strokeColor = new Color32(214, 196, 168, 255);
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
