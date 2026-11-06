using System;
using System.Collections;
using System.Collections.Generic;
using SunodGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Town-mode overlay (Game_Scene_Town), UI V2 (2026-11-07):
///  - top-right: the day dial (stage progress ring + "Day N" chip; tap = life stages banner)
///    and the zoom -/+ pair; the pause button (another document) sits right of the dial;
///  - right column: Back to town / Sleep / Talk + Journal / energy pill;
///  - the life-stage banner: one card per stage, a segment per quest lit in its RIASEC colour;
///  - the end-of-day summary card ("Good night") and the dream screen;
///  - quest timer, presses, stamps, toast, quest-done sheet, next-stage button.
/// The tree is built in code on the sn-* theme components; RunProgress.uss lays it out.
/// Motion goes through vc_UIAnim (unscaled time).
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

    private VisualElement _root, _container, _banner, _track;
    private Label _bannerDay;

    // one card per life stage
    private readonly List<VisualElement> _stageCards = new List<VisualElement>();
    private readonly List<Label> _stageBadges = new List<Label>();
    private readonly List<HudIcon> _stageChecks = new List<HudIcon>();
    private readonly List<Label> _stageCounts = new List<Label>();
    private readonly List<VisualElement> _stageBars = new List<VisualElement>();
    private readonly List<List<VisualElement>> _pips = new List<List<VisualElement>>();
    private VisualElement _stormCard;

    private VisualElement _summaryDim, _summaryCard;
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

    /// <summary>The HUD's UI Toolkit root, for minigame overlays built by quests.</summary>
    public VisualElement OverlayRoot => _root;

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
        _pulses.Clear();

        var container = new VisualElement { pickingMode = PickingMode.Ignore };
        container.AddToClassList("rp-root");
        _root.Add(container);
        _container = container;

        BuildBanner(container);
        BuildRing(container);
        BuildSideControls(container);
        BuildToast(container);
        BuildSummary(container);
        BuildQuestDone(container);
        BuildDream(container);
        BuildTimer(container);
        BuildCinematic(container);

        vc_TownFlow.ProgressChanged -= Refresh;
        vc_TownFlow.ProgressChanged += Refresh;
    }

    private void Start()
    {
        Refresh();
        // HUD widgets slide in from their edge
        SlideIn(_ring, vc_UIAnim.Enter.SlideDown, 0.05f);
        SlideIn(_zoomRow, vc_UIAnim.Enter.SlideDown, 0.1f);
        SlideIn(_side, vc_UIAnim.Enter.SlideLeft, 0.15f);
    }

    // ── banner: "Life stages" ─────────────────────────────────────────────
    // Header (title + day chip), then one card per stage: number badge + name, a flush
    // segmented bar (one segment per quest, lit in that quest's RIASEC colour once done)
    // and a short status line. A small storm card at the end is the disaster.

    private void BuildBanner(VisualElement parent)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.AddToClassList("rp-center-row");
        row.AddToClassList("rp-banner-row");
        parent.Add(row);

        _banner = new VisualElement { pickingMode = PickingMode.Ignore };
        _banner.AddToClassList("sn-panel");
        _banner.AddToClassList("rp-banner");
        _banner.AddToClassList("rp-hidden");      // opened by tapping the day dial
        row.Add(_banner);

        var header = new VisualElement { pickingMode = PickingMode.Ignore };
        header.AddToClassList("sn-header");
        header.AddToClassList("rp-banner-header");
        var title = new Label("Life stages") { pickingMode = PickingMode.Ignore };
        title.AddToClassList("sn-subtitle");
        title.AddToClassList("rp-banner-title");
        _bannerDay = new Label("") { pickingMode = PickingMode.Ignore };
        _bannerDay.AddToClassList("sn-chip");
        _bannerDay.AddToClassList("rp-banner-day");
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
        _stageChecks.Clear();
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
            card.AddToClassList("sn-card");
            card.AddToClassList("rp-stage");

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("rp-stage-top");
            var badge = new Label(StageShort[s]) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("rp-stage-badge");
            var check = new HudIcon(HudIcon.Glyph.Check);
            check.AddToClassList("rp-glyph");
            check.style.display = DisplayStyle.None;
            badge.Add(check);
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
            countLabel.AddToClassList("sn-caption");
            countLabel.AddToClassList("rp-stage-count");
            card.Add(countLabel);

            _track.Add(card);
            _stageCards.Add(card);
            _stageBadges.Add(badge);
            _stageChecks.Add(check);
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
            _stageBadges[s].text = isPast ? "" : StageShort[s];
            _stageChecks[s].style.display = isPast ? DisplayStyle.Flex : DisplayStyle.None;

            _stageCounts[s].text = isPast ? $"All {count} done"
                : isCurrent ? $"{done}/{count} done"
                : $"{count} quests";
        }

        _stormCard?.EnableInClassList("rp-storm--near", current >= StageNames.Length - 1);

        vc_TownStage stage = vc_TownStage.Current;
        _bannerDay.text = stage != null ? $"Day {stage.Day}  -  {TotalDone(flow)}/{TotalQuests(flow)} quests" : "";
        _bannerDay.EnableInClassList("rp-hidden", stage == null);
        UpdateRing(flow, stage, current);
    }

    /// <summary>Quests played so far in the whole run (passed or failed), all stages.</summary>
    public static int TotalDone(vc_TownFlow flow)
    {
        if (flow == null) return 0;
        int done = 0;
        for (int s = 0; s < flow.Stages.Length; s++)
            for (int q = 0; q < flow.StageQuestCount(s); q++)
                if (flow.StageQuestDone(s, q)) done++;
        return done;
    }

    /// <summary>All quests in the run (48 when every stage has its 16).</summary>
    public static int TotalQuests(vc_TownFlow flow)
    {
        if (flow == null) return 48;
        int total = 0;
        for (int s = 0; s < flow.Stages.Length; s++) total += flow.StageQuestCount(s);
        return total > 0 ? total : 48;
    }

    // ── day summary ───────────────────────────────────────────────────────

    private void BuildSummary(VisualElement parent)
    {
        _summaryDim = new VisualElement();
        _summaryDim.AddToClassList("sn-scrim");
        _summaryDim.AddToClassList("rp-dim");
        _summaryDim.AddToClassList("rp-hidden");
        parent.Add(_summaryDim);

        _summaryCard = new VisualElement();
        _summaryCard.AddToClassList("sn-panel");
        _summaryCard.AddToClassList("sn-sheet");
        _summaryCard.AddToClassList("rp-card");
        _summaryDim.Add(_summaryCard);

        _summaryTitle = new Label();
        _summaryTitle.AddToClassList("sn-title");
        _summaryTitle.AddToClassList("rp-card-title");
        _summaryBody = new Label();
        _summaryBody.AddToClassList("sn-body");
        _summaryBody.AddToClassList("sn-center");
        _summaryBody.AddToClassList("rp-card-body");
        _summaryBtn = MakePill("Good night", HudIcon.Glyph.Moon, "sleep", "night", null);
        _summaryBtn.AddToClassList("rp-card-btn");
        _summaryChart = new RiasecChart();
        _summaryChart.AddToClassList("rp-chart");
        var chartCaption = new Label("Your interests");
        chartCaption.AddToClassList("sn-caption");
        chartCaption.AddToClassList("sn-center");
        chartCaption.AddToClassList("rp-chart-caption");

        _summaryCard.Add(_summaryTitle);
        _summaryCard.Add(_summaryBody);
        _summaryCard.Add(chartCaption);
        _summaryCard.Add(_summaryChart);
        _summaryCard.Add(_summaryBtn);
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
        ShowEl(_summaryDim, vc_UIAnim.Enter.Fade);
        vc_UIAnim.Show(_summaryCard, vc_UIAnim.Enter.Pop, 0.06f);

        bool used = false;   // the card animates out, so guard against a second tap
        _summaryBtn.clickable = new Clickable(() =>
        {
            if (used) return;
            used = true;
            HideEl(_summaryDim, vc_UIAnim.Exit.Fade);
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
        _dreamHint = new Label("Tap to continue");
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
        ShowDreamHint();
    }

    private void ShowDreamHint()
    {
        _dreamHint.visible = true;
        vc_UIAnim.Show(_dreamHint, vc_UIAnim.Enter.Fade);
    }

    private void AdvanceDream()
    {
        if (_dreamLines == null) return;
        if (_dreamTyping)
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _dreamText.text = _dreamIndex < _dreamLines.Length ? _dreamLines[_dreamIndex] : "";
            _dreamTyping = false;
            ShowDreamHint();
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
    /// The progress HUD (day dial; the full banner opens when the dial is tapped) hides
    /// during quests so it doesn't crowd the quest UI.
    /// </summary>
    public void SetBannerVisible(bool visible)
    {
        _hudVisible = visible;
        SetShownEl(_ring, visible, vc_UIAnim.Enter.SlideDown, vc_UIAnim.Exit.SlideUp);
        SetShownEl(_banner, visible && _bannerOpen, vc_UIAnim.Enter.Drop, vc_UIAnim.Exit.SlideUp);
    }

    // ── "next stage" button (shown when nothing is left to do in the town) ──

    private Button _nextBtn;
    private Label _nextLabel;

    public void ShowNextStageButton(string label, Action onClick)
    {
        if (_nextBtn == null)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("rp-center-row");
            row.AddToClassList("rp-next-row");
            (_container ?? _root).Add(row);

            _nextBtn = MakePill("", HudIcon.Glyph.Arrow, "next", "gold", null, true);
            _nextBtn.AddToClassList("rp-next-btn");
            _nextBtn.AddToClassList("rp-hidden");
            _nextLabel = PillLabel(_nextBtn);
            row.Add(_nextBtn);
        }
        // the arrow is drawn now, so a trailing " >" in the caller's text is dropped
        _nextLabel.text = (label ?? "").TrimEnd(' ', '>');
        _nextBtn.clickable = new Clickable(() => onClick?.Invoke());
        SetShownEl(_nextBtn, true, vc_UIAnim.Enter.Drop, vc_UIAnim.Exit.SlideUp);
        SetGlow(_nextBtn, true, false);
    }

    public void HideNextStageButton()
    {
        if (_nextBtn == null) return;
        SetGlow(_nextBtn, false);
        HideEl(_nextBtn, vc_UIAnim.Exit.SlideUp);
    }

    // ── right column: Back to town, Sleep, Talk + Journal, energy; zoom in the top bar ──

    private VisualElement _side, _zoomRow;
    private Label _energyLabel;
    private VisualElement _energyChip, _energyPips;
    private readonly List<HudIcon> _energyIcons = new List<HudIcon>();
    private int _energyShown = -1;
    private const int MaxEnergyPips = 8;
    private Button _sleepBtn;

    private void BuildSideControls(VisualElement parent)
    {
        _side = new VisualElement { pickingMode = PickingMode.Ignore };
        _side.AddToClassList("rp-side");
        parent.Add(_side);

        _sleepBtn = MakePill("Sleep", HudIcon.Glyph.Moon, "sleep", "night", () =>
        {
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            vc_TownStage.Current?.RequestSleep();
        });
        _sleepBtn.AddToClassList("rp-side-btn");
        _sleepBtn.AddToClassList("rp-hidden");
        _side.Add(_sleepBtn);

        // energy pill: one bolt per point (filled = left); "bolt n/max" when there are lots
        _energyChip = new VisualElement { pickingMode = PickingMode.Ignore };
        _energyChip.AddToClassList("sn-chip");
        _energyChip.AddToClassList("rp-energy");
        _energyChip.AddToClassList("rp-hidden");
        _energyPips = new VisualElement { pickingMode = PickingMode.Ignore };
        _energyPips.style.flexDirection = FlexDirection.Row;
        _energyPips.style.alignItems = Align.Center;
        _energyChip.Add(_energyPips);
        _energyLabel = new Label("") { pickingMode = PickingMode.Ignore };
        _energyLabel.AddToClassList("rp-energy-label");
        _energyLabel.style.display = DisplayStyle.None;
        _energyChip.Add(_energyLabel);
        _side.Add(_energyChip);

        // zoom -/+ : top bar, left of the day dial (keeps the column short)
        _zoomRow = new VisualElement { pickingMode = PickingMode.Ignore };
        _zoomRow.AddToClassList("rp-zoom-row");
        var zoomOut = new Button { text = "-" };
        var zoomIn = new Button { text = "+" };
        foreach (Button b in new[] { zoomOut, zoomIn })
        {
            b.AddToClassList("sn-btn");
            b.AddToClassList("rp-zoom-btn");
            vc_UIAnim.Press(b);
            _zoomRow.Add(b);
        }
        vc_CameraZoom.BindButtons(zoomOut, zoomIn);
        parent.Add(_zoomRow);
    }

    public void SetEnergy(int energy, int max)
    {
        if (_energyLabel == null) return;
        max = Mathf.Max(0, max);
        bool compact = max <= 0 || max > MaxEnergyPips;
        int shown = compact ? 1 : max;

        while (_energyIcons.Count < shown)
        {
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.AddToClassList("rp-energy-pip");
            holder.AddToClassList("rp-icon--energy");
            var icon = new HudIcon(HudIcon.Glyph.Bolt);
            icon.AddToClassList("rp-glyph");
            holder.Add(icon);
            _energyPips.Add(holder);
            _energyIcons.Add(icon);
        }
        for (int i = 0; i < _energyIcons.Count; i++)
        {
            _energyIcons[i].parent.style.display = i < shown ? DisplayStyle.Flex : DisplayStyle.None;
            _energyIcons[i].Empty = compact ? energy <= 0 : i >= energy;
        }
        _energyLabel.text = compact ? $"{energy}/{max}" : "";
        _energyLabel.style.display = compact ? DisplayStyle.Flex : DisplayStyle.None;

        if (_energyShown >= 0 && energy != _energyShown)
        {
            vc_UIAnim.Punch(_energyChip, 0.1f, 0.3f);
            // the bolt that was just spent pops
            if (!compact && energy < _energyShown && energy >= 0 && energy < shown)
                vc_UIAnim.Punch(_energyIcons[energy].parent, 0.45f, 0.35f);
        }
        _energyShown = energy;
        SetShownEl(_energyChip, true, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);
    }

    public void SetSleepVisible(bool visible) =>
        SetShownEl(_sleepBtn, visible, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);

    // ── Journal button: the quest journal (accepted requests) ─────────────

    private Button _journalBtn;
    private Label _journalBadge;
    private Action _journalAction;
    private int _journalShown = -1;

    /// <summary>count &lt; 0 hides it; otherwise a round book button with a count badge.</summary>
    public void SetJournalButton(int count, Action onClick)
    {
        if (_side == null) return;
        if (_journalBtn == null)
        {
            _journalBtn = new Button();
            _journalBtn.AddToClassList("sn-btn");
            _journalBtn.AddToClassList("rp-journal-btn");
            _journalBtn.AddToClassList("rp-hidden");
            _journalBtn.clickable = new Clickable(() =>
            {
                SunodGame.Core.vc_Sfx.Play("click", 0.6f);
                _journalAction?.Invoke();
            });
            var jl = new Label("Journal") { pickingMode = PickingMode.Ignore };
            jl.AddToClassList("sn-btn__label");
            jl.AddToClassList("rp-label--solo");
            _journalBtn.Add(jl);
            _journalBadge = new Label("") { pickingMode = PickingMode.Ignore };
            _journalBadge.AddToClassList("sn-badge");
            _journalBadge.AddToClassList("sn-badge--gold");
            _journalBadge.AddToClassList("rp-journal-badge");
            _journalBadge.style.display = DisplayStyle.None;
            _journalBtn.Add(_journalBadge);
            vc_UIAnim.Press(_journalBtn);
            ActionRow().Add(_journalBtn);
        }
        _journalAction = onClick;
        SetShownEl(_journalBtn, count >= 0, vc_UIAnim.Enter.Pop, vc_UIAnim.Exit.Shrink);
        _journalBadge.text = count > 0 ? count.ToString() : "";
        _journalBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (count > 0 && _journalShown >= 0 && count > _journalShown) vc_UIAnim.Punch(_journalBadge, 0.3f, 0.3f);
        if (count >= 0) _journalShown = count;
    }

    // Talk + Journal share one row under Sleep, so the column stays clear of the skill menu.
    private VisualElement _actionRow;

    private VisualElement ActionRow()
    {
        if (_actionRow != null) return _actionRow;
        _actionRow = new VisualElement { pickingMode = PickingMode.Ignore };
        _actionRow.AddToClassList("rp-action-row");
        int at = _sleepBtn != null ? _side.IndexOf(_sleepBtn) + 1 : _side.childCount;
        _side.Insert(Mathf.Clamp(at, 0, _side.childCount), _actionRow);
        return _actionRow;
    }

    // ── Talk button: shows when someone you can talk to is close ───────────

    private Button _talkBtn;
    private Label _talkLabel;
    private Action _talkAction;

    /// <summary>label null hides it. "Talk!" (someone with a request) glows.</summary>
    public void SetTalkButton(string label, Action onClick)
    {
        if (_side == null) return;
        if (_talkBtn == null)
        {
            _talkBtn = MakePill("Talk", HudIcon.Glyph.Talk, "talk", null, () =>
            {
                SunodGame.Core.vc_Sfx.Play("click", 0.6f);
                _talkAction?.Invoke();
            });
            _talkBtn.AddToClassList("rp-side-btn");
            _talkBtn.AddToClassList("rp-talk-btn");
            _talkBtn.AddToClassList("rp-hidden");
            _talkLabel = PillLabel(_talkBtn);
            ActionRow().Insert(0, _talkBtn);
        }
        _talkAction = onClick;
        bool show = !string.IsNullOrEmpty(label) && onClick != null;
        SetShownEl(_talkBtn, show, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);
        if (!show) { SetGlow(_talkBtn, false); return; }
        if (_talkLabel != null) _talkLabel.text = label.TrimEnd('!');
        SetGlow(_talkBtn, label.EndsWith("!"), true);
    }

    /// <summary>Sleep pulses when the day is used up (no energy, or nobody left asking).</summary>
    public void SetSleepGlow(bool glow) => SetGlow(_sleepBtn, glow, false);

    // ── "do this now": a gentle vc_UIAnim.Pulse (+ the gold sn-attn tint on cream pills) ──
    private readonly Dictionary<VisualElement, vc_UIAnim.Handle> _pulses = new Dictionary<VisualElement, vc_UIAnim.Handle>();

    private void SetGlow(VisualElement el, bool on, bool tint = true)
    {
        if (el == null) return;
        bool has = _pulses.TryGetValue(el, out vc_UIAnim.Handle h);
        if (on == has) return;
        if (on)
        {
            _pulses[el] = vc_UIAnim.Pulse(el);
            if (tint) el.AddToClassList("sn-attn");
        }
        else
        {
            vc_UIAnim.StopPulse(ref h, el);
            _pulses.Remove(el);
            el.RemoveFromClassList("sn-attn");
        }
    }

    /// <summary>Quest running: the energy pill steps aside (there is no way to back out of a quest).</summary>
    public void EnterQuestMode() => SetShownEl(_energyChip, false, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);

    public void ExitQuestMode() => SetShownEl(_energyChip, true, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);

    public bool IsShowingOverlay =>
        (_dream != null && !_dream.ClassListContains("rp-hidden")) ||
        (_summaryDim != null && !_summaryDim.ClassListContains("rp-hidden"));

    // ── day dial (top-right; tap to open the life-stages banner) ──────────

    private ProgressRing _ring;
    private Label _ringStage, _ringDay;
    private VisualElement _ringChipRow;
    private Button _partialBtn;

    private void BuildRing(VisualElement parent)
    {
        _ring = new ProgressRing();
        _ring.AddToClassList("rp-ring");
        _ringStage = new Label("1") { pickingMode = PickingMode.Ignore };
        _ringStage.AddToClassList("rp-ring-stage");
        _ring.Add(_ringStage);
        // "Day N" chip under the dial (part of the tap target)
        _ringChipRow = new VisualElement { pickingMode = PickingMode.Ignore };
        _ringChipRow.AddToClassList("rp-ring-chiprow");
        _ringDay = new Label("") { pickingMode = PickingMode.Position };
        _ringDay.AddToClassList("sn-chip");
        _ringDay.AddToClassList("rp-ring-day");
        _ringChipRow.Add(_ringDay);
        _ring.Add(_ringChipRow);
        _ring.RegisterCallback<PointerUpEvent>(_ => ToggleBanner());
        vc_UIAnim.Press(_ring);
        parent.Add(_ring);

        // Tapping the open banner closes it again.
        _banner.pickingMode = PickingMode.Position;
        _banner.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.target is VisualElement ve && _partialBtn != null && (_partialBtn == ve || _partialBtn.Contains(ve))) return;
            ToggleBanner();
        });

        // Leaning-career peek: inside the banner, shown once half the run's quests are answered.
        _partialBtn = MakePill("My path", HudIcon.Glyph.Star, "path", null, RequestPartialResults);
        _partialBtn.AddToClassList("rp-partial-btn");
        _partialBtn.AddToClassList("rp-hidden");
        _banner.Add(_partialBtn);
    }

    private void ToggleBanner()
    {
        _bannerOpen = !_bannerOpen;
        SunodGame.Core.vc_Sfx.Play("click", 0.5f);
        SetShownEl(_banner, _hudVisible && _bannerOpen, vc_UIAnim.Enter.Drop, vc_UIAnim.Exit.SlideUp);
        if (_bannerOpen && _hudVisible) vc_UIAnim.Stagger(_track.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.1f);
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
        string day = stage != null ? $"Day {stage.Day}" : "";
        if (_ringDay.text != day)
        {
            bool changed = !string.IsNullOrEmpty(_ringDay.text) && !string.IsNullOrEmpty(day);
            _ringDay.text = day;
            if (changed) vc_UIAnim.Punch(_ringDay, 0.2f, 0.35f);
        }
        _ringChipRow.style.display = stage != null ? DisplayStyle.Flex : DisplayStyle.None;

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
    /// "My path": a peek at where the answers point so far. It never ends the run;
    /// the real result still comes at the end of College.
    /// </summary>
    private void RequestPartialResults()
    {
        if (vc_DialogPanel.Instance == null || _previewBusy) return;
        _bannerOpen = false;
        HideEl(_banner, vc_UIAnim.Exit.SlideUp);
        SunodGame.Core.vc_Sfx.Play("click", 0.6f);

        string leaning = LeaningText();
        string basis = $"Based on {_partialAnswered} of {_partialTotal} quests.";

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
            body.Append($"Leaning toward: {career}");
            if (!string.IsNullOrWhiteSpace(family)) body.Append($" ({family})");
            body.Append(".\n\n");
        }
        else if (!string.IsNullOrWhiteSpace(family))
        {
            body.Append($"Leaning toward the {family} path.\n\n");
        }
        if (!string.IsNullOrEmpty(leaning)) body.Append($"Top interests: {leaning}.\n\n");
        if (body.Length == 0) body.Append("Nothing stands out yet. Help a few more people!\n\n");
        body.Append(basis);
        vc_DialogPanel.Instance?.ShowMessage("Your path so far", body.ToString(), null);
    }

    // ── toast (replaces the old feedback text) ────────────────────────────

    private Label _toast;
    private IVisualElementScheduledItem _toastHide;

    private void BuildToast(VisualElement parent)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.AddToClassList("rp-center-row");
        row.AddToClassList("rp-toast-row");
        parent.Add(row);

        _toast = new Label("") { pickingMode = PickingMode.Ignore };
        _toast.AddToClassList("sn-chip");
        _toast.AddToClassList("rp-toast");
        _toast.AddToClassList("rp-hidden");
        row.Add(_toast);
    }

    /// <summary>Short message at the top of the screen. duration &lt;= 0 keeps it until HideToast.</summary>
    public void ShowToast(string text, float duration = 2f)
    {
        if (_toast == null) return;
        if (string.IsNullOrEmpty(text)) { HideToast(); return; }
        bool changed = _toast.text != text;
        _toast.text = text;
        if (_toast.ClassListContains("rp-hidden")) ShowEl(_toast, vc_UIAnim.Enter.Drop);
        else if (changed) vc_UIAnim.Punch(_toast, 0.05f, 0.25f);
        _toastHide?.Pause();
        _toastHide = duration > 0f ? _toast.schedule.Execute(HideToast).StartingIn((long)(duration * 1000f)) : null;
    }

    public void HideToast()
    {
        _toastHide?.Pause();
        _toastHide = null;
        if (_toast != null && !_toast.ClassListContains("rp-hidden")) HideEl(_toast, vc_UIAnim.Exit.SlideUp);
    }

    // ── quest done: sheet + "Back to town" button (non-blocking) ──────────

    private VisualElement _doneToast, _doneScrim;
    private Label _doneTitle, _doneNote, _doneXp;
    private readonly List<HudIcon> _doneStars = new List<HudIcon>();
    private Button _backBtn;
    private Action _doneContinue;
    private IVisualElementScheduledItem _doneHide;

    private void BuildQuestDone(VisualElement parent)
    {
        // a light scrim that never takes input: the player can keep looking around
        _doneScrim = new VisualElement { pickingMode = PickingMode.Ignore };
        _doneScrim.AddToClassList("rp-done-scrim");
        _doneScrim.style.display = DisplayStyle.None;
        parent.Add(_doneScrim);

        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.AddToClassList("rp-center-row");
        row.AddToClassList("rp-done-row");
        parent.Add(row);

        _doneToast = new VisualElement { pickingMode = PickingMode.Ignore };
        _doneToast.AddToClassList("sn-panel");
        _doneToast.AddToClassList("rp-done-toast");
        _doneToast.AddToClassList("rp-hidden");
        row.Add(_doneToast);

        _doneTitle = new Label("Quest done!") { pickingMode = PickingMode.Ignore };
        _doneTitle.AddToClassList("sn-subtitle");
        _doneTitle.AddToClassList("rp-done-title");
        _doneToast.Add(_doneTitle);

        var stars = new VisualElement { pickingMode = PickingMode.Ignore };
        stars.AddToClassList("rp-done-stars");
        for (int i = 0; i < 5; i++)
        {
            var s = new HudIcon(HudIcon.Glyph.Star);
            s.AddToClassList("rp-star");
            stars.Add(s);
            _doneStars.Add(s);
        }
        _doneToast.Add(stars);

        _doneNote = new Label { pickingMode = PickingMode.Ignore };
        _doneNote.AddToClassList("sn-caption");
        _doneNote.AddToClassList("rp-done-note");
        _doneToast.Add(_doneNote);

        // skill XP earned by this quest (vc_FitQuestBase.LastXpReport)
        _doneXp = new Label { pickingMode = PickingMode.Ignore };
        _doneXp.AddToClassList("rp-done-xp");
        _doneToast.Add(_doneXp);

        // Lives at the top of the right column.
        _backBtn = MakePill("Back to town", HudIcon.Glyph.House, "home", null, () =>
        {
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            HideQuestDone();
            Action cb = _doneContinue;
            _doneContinue = null;
            cb?.Invoke();
        });
        _backBtn.AddToClassList("rp-side-btn");
        _backBtn.AddToClassList("rp-hidden");
        _side.Insert(0, _backBtn);
    }

    /// <summary>
    /// Town version of the quest-done popup. It doesn't block: a sheet with the stars pops in
    /// at the top and leaves after a few seconds, and a pulsing "Back to town" button stays
    /// until the player taps it, so they can keep looking around the room first.
    /// </summary>
    public void ShowQuestDone(string questName, int stars, Action onContinue)
    {
        _doneContinue = onContinue;
        stars = Mathf.Clamp(stars, 0, 5);
        _doneTitle.text = string.IsNullOrEmpty(questName) ? "Quest done!" : $"{questName} done!";
        string praise = stars >= 5 ? "Perfect!" : stars >= 3 ? "Nice work!" : "You did it!";
        _doneNote.text = $"{praise} Look around, then head back.";
        string xp = vc_FitQuestBase.TakeXpReport();
        _doneXp.text = xp ?? "";
        _doneXp.style.display = string.IsNullOrEmpty(xp) ? DisplayStyle.None : DisplayStyle.Flex;

        HideToast();
        vc_UIAnim.Show(_doneScrim, vc_UIAnim.Enter.Fade);
        ShowEl(_doneToast, vc_UIAnim.Enter.Pop);

        // stars pop in one by one; the earned ones punch with a ding
        for (int i = 0; i < _doneStars.Count; i++)
        {
            HudIcon s = _doneStars[i];
            bool full = i < stars;
            s.Empty = !full;
            vc_UIAnim.Show(s, vc_UIAnim.Enter.Pop, 0.3f + 0.14f * i, null, 0.3f);
            if (!full) continue;
            s.schedule.Execute(() =>
            {
                vc_UIAnim.Punch(s, 0.3f, 0.3f);
                SunodGame.Core.vc_Sfx.Play("step_done", 0.5f, 0.08f);
            }).StartingIn(600 + 140 * i);
        }
        if (!string.IsNullOrEmpty(xp)) _doneXp.schedule.Execute(() => vc_UIAnim.Punch(_doneXp, 0.15f, 0.3f)).StartingIn(600 + 140 * stars);

        _doneHide?.Pause();
        _doneHide = _doneToast.schedule.Execute(HideDoneSheet).StartingIn(5000);

        SetShownEl(_backBtn, true, vc_UIAnim.Enter.SlideLeft, vc_UIAnim.Exit.SlideRight);
        SetGlow(_backBtn, true, true);
    }

    private void HideDoneSheet()
    {
        HideEl(_doneToast, vc_UIAnim.Exit.SlideUp);
        vc_UIAnim.Hide(_doneScrim, null, vc_UIAnim.Exit.Fade);
    }

    private void HideQuestDone()
    {
        _doneHide?.Pause();
        HideDoneSheet();
        SetGlow(_backBtn, false);
        HideEl(_backBtn, vc_UIAnim.Exit.SlideRight);
        HideToast();
    }

    // ── quest timer: 5 stars + a draining bar, top centre ─────────────────
    // vc_QuestTimer drives it. Tick marks on the bar sit where each star goes out.

    private VisualElement _timer, _timerFill;
    private readonly List<HudIcon> _timerStars = new List<HudIcon>();
    private int _timerShown = -1;

    private static readonly Color[] TimerColors =
    {
        vc_UITheme.Red, new Color32(236, 140, 46, 255), vc_UITheme.Gold,
        new Color32(150, 190, 80, 255), vc_UITheme.Green,
    };

    private void BuildTimer(VisualElement parent)
    {
        var timerRow = new VisualElement { pickingMode = PickingMode.Ignore };
        timerRow.AddToClassList("rp-center-row");
        timerRow.AddToClassList("rp-timer-row");
        parent.Insert(parent.IndexOf(_summaryDim), timerRow);

        _timer = new VisualElement { pickingMode = PickingMode.Ignore };
        _timer.AddToClassList("sn-panel");
        _timer.AddToClassList("rp-timer");
        _timer.AddToClassList("rp-hidden");
        timerRow.Add(_timer);

        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.AddToClassList("rp-timer-stars");
        for (int i = 0; i < 5; i++)
        {
            var s = new HudIcon(HudIcon.Glyph.Star);
            s.AddToClassList("rp-timer-star");
            row.Add(s);
            _timerStars.Add(s);
        }
        _timer.Add(row);

        var bar = new VisualElement { pickingMode = PickingMode.Ignore };
        bar.AddToClassList("rp-timer-bar");
        _timerFill = new VisualElement { pickingMode = PickingMode.Ignore };
        _timerFill.AddToClassList("rp-timer-fill");
        bar.Add(_timerFill);
        for (int i = 1; i <= 4; i++)
        {
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("rp-timer-tick");
            tick.style.left = Length.Percent(i * 20f);
            bar.Add(tick);
        }
        _timer.Add(bar);

        // skill system v2: presses left this quest (filled = left), under the bar
        _presses = new VisualElement { pickingMode = PickingMode.Ignore };
        _presses.AddToClassList("rp-presses");
        _presses.style.display = DisplayStyle.None;
        _timer.Add(_presses);
    }

    // ── presses left (skill system v2) ────────────────────────────────────

    private VisualElement _presses;
    private readonly List<VisualElement> _pressDots = new List<VisualElement>();
    private int _pressesShown = -1;
    private vc_UIAnim.Handle _pressPulse;
    private VisualElement _pressPulseEl;

    /// <summary>Presses left out of total (total 0 hides the row). The last one turns red and pulses.</summary>
    public void SetPresses(int left, int total)
    {
        if (_presses == null) return;
        _presses.style.display = total > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (total <= 0)
        {
            _pressesShown = -1;
            SetPressPulse(null);
            return;
        }

        while (_pressDots.Count < total)
        {
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("rp-press-dot");
            _presses.Add(dot);
            _pressDots.Add(dot);
        }
        for (int i = 0; i < _pressDots.Count; i++)
        {
            VisualElement d = _pressDots[i];
            d.style.display = i < total ? DisplayStyle.Flex : DisplayStyle.None;
            bool full = i < left;
            d.EnableInClassList("rp-press-dot--full", full && left != 1);
            d.EnableInClassList("rp-press-dot--last", full && left == 1);
        }
        // the dot that was just spent pops
        if (_pressesShown > left && left >= 0 && left < _pressDots.Count)
            vc_UIAnim.Punch(_pressDots[left], 0.5f, 0.3f);
        _pressesShown = left;
        SetPressPulse(left == 1 && _pressDots.Count > 0 ? _pressDots[0] : null);
    }

    private void SetPressPulse(VisualElement want)
    {
        if (want == _pressPulseEl) return;
        if (_pressPulseEl != null) vc_UIAnim.StopPulse(ref _pressPulse, _pressPulseEl);
        _pressPulseEl = want;
        if (want != null) _pressPulse = vc_UIAnim.Pulse(want, 0.25f, 0.6f);
    }

    // ── stamp: "Perfect fit!" / "Prepared!" / "Lv2!" over the room ──────────

    /// <summary>The last stamp shown (tests / debugging).</summary>
    public string LastStamp { get; private set; }

    /// <summary>
    /// A big tilted word that drops onto the room for a moment, under the quest-done sheet.
    /// yShift moves it down (+) or up (-) in steps of ~20% of the screen, so a level-up and a
    /// "Perfect fit!" can show at once. The colour fills the stamp; text is ink or cream for contrast.
    /// </summary>
    public void ShowStamp(string text, Color color, float yShift = 0f)
    {
        VisualElement root = _container;
        if (root == null || string.IsNullOrEmpty(text)) return;
        LastStamp = text;

        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.AddToClassList("rp-center-row");
        row.style.top = Length.Percent(Mathf.Clamp(50f + yShift * 20f, 8f, 80f));

        var stamp = new Label(text) { pickingMode = PickingMode.Ignore };
        stamp.AddToClassList("rp-stamp");
        color.a = 1f;
        Color edge = Color.Lerp(color, vc_UITheme.BrownDark, 0.45f);
        stamp.style.backgroundColor = color;
        vc_UITheme.Border(stamp, 3f, edge);
        stamp.style.borderBottomWidth = 7f;
        float lum = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
        stamp.style.color = lum < 0.6f ? vc_UITheme.CreamHi : vc_UITheme.Ink;
        stamp.style.rotate = new Rotate(new Angle(-6f, AngleUnit.Degree));
        row.Add(stamp);
        root.Add(row);
        SunodGame.Core.vc_Sfx.Play("stamp", 0.55f, 0.08f);

        vc_UIAnim.Show(stamp, vc_UIAnim.Enter.Drop, 0f, null, 0.3f);
        stamp.schedule.Execute(() =>
            vc_UIAnim.Hide(stamp, () => row.RemoveFromHierarchy(), vc_UIAnim.Exit.Shrink)).StartingIn(1300);
    }

    /// <summary>The timer element, e.g. for the tutorial pointer.</summary>
    public VisualElement TimerElement => _timer;

    public void SetTimerVisible(bool visible)
    {
        SetShownEl(_timer, visible, vc_UIAnim.Enter.SlideDown, vc_UIAnim.Exit.SlideUp);
        if (visible) _timerShown = -1;
    }

    /// <summary>Stars left (0..5); a star that just went out punches.</summary>
    public void SetTimerStars(int stars, bool running)
    {
        if (_timer == null) return;
        for (int i = 0; i < _timerStars.Count; i++)
            _timerStars[i].Empty = i >= stars;
        if (running && _timerShown > stars && stars >= 0 && stars < _timerStars.Count)
            vc_UIAnim.Punch(_timerStars[stars], 0.45f, 0.38f);
        _timerShown = stars;
    }

    /// <summary>Bar fill (0..1), coloured by the stars left; blinks on the last star.</summary>
    public void SetTimerFill(float ratio, int stars, bool running)
    {
        if (_timerFill == null) return;
        _timerFill.style.width = Length.Percent(Mathf.Round(Mathf.Clamp01(ratio) * 100f));
        Color c = TimerColors[Mathf.Clamp(stars, 1, 5) - 1];
        if (stars <= 1 && running && Mathf.Repeat(Time.time, 0.5f) < 0.25f) c = vc_UITheme.CreamHi;
        _timerFill.style.backgroundColor = c;
    }

    // ── going to bed: letterbox, HUD away, fade, night backdrop ───────────

    private VisualElement _letterTop, _letterBottom, _night, _fade;
    private readonly List<(VisualElement el, DisplayStyle was)> _hiddenDocs = new List<(VisualElement, DisplayStyle)>();
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    private bool _cinematic;

    private void BuildCinematic(VisualElement parent)
    {
        // night sits under the day summary; letterbox over the night; fade on top of all
        _night = new VisualElement { pickingMode = PickingMode.Position };
        _night.AddToClassList("rp-night");
        _night.AddToClassList("rp-hidden");
        parent.Insert(parent.IndexOf(_summaryDim), _night);

        _letterTop = new VisualElement { pickingMode = PickingMode.Ignore };
        _letterTop.AddToClassList("rp-letterbox");
        _letterTop.AddToClassList("rp-letterbox--top");
        _letterBottom = new VisualElement { pickingMode = PickingMode.Ignore };
        _letterBottom.AddToClassList("rp-letterbox");
        _letterBottom.AddToClassList("rp-letterbox--bottom");
        parent.Insert(parent.IndexOf(_summaryDim), _letterTop);
        parent.Insert(parent.IndexOf(_summaryDim), _letterBottom);

        _fade = new VisualElement { pickingMode = PickingMode.Ignore };
        _fade.AddToClassList("rp-fade");
        parent.Add(_fade);
    }

    /// <summary>
    /// Cutscene mode: letterbox bars slide in and every HUD piece (quest panel, skill buttons,
    /// joystick, pause, day dial, side buttons) hides. Off restores what was there.
    /// </summary>
    public void SetCinematic(bool on)
    {
        if (on == _cinematic) return;
        _cinematic = on;
        _letterTop?.EnableInClassList("rp-letterbox--on", on);
        _letterBottom?.EnableInClassList("rp-letterbox--on", on);

        foreach (VisualElement el in new[] { _ring, _banner, _side, _zoomRow, _toast, _nextBtn })
            if (el != null) el.style.visibility = on ? Visibility.Hidden : Visibility.Visible;

        if (on)
        {
            _hiddenDocs.Clear();
            _hiddenCanvases.Clear();
            UIDocument mine = GetComponent<UIDocument>();
            foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc == mine || doc.rootVisualElement == null) continue;
                if (doc.GetComponent<vc_DialogPanel>() != null || doc.GetComponent<vc_SkillAwardPopup>() != null) continue;
                VisualElement root = doc.rootVisualElement;
                _hiddenDocs.Add((root, root.style.display.value));
                root.style.display = DisplayStyle.None;
            }
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || !c.enabled) continue;
                _hiddenCanvases.Add(c);
                c.enabled = false;
            }
        }
        else
        {
            foreach (var (el, was) in _hiddenDocs) if (el != null) el.style.display = was;
            foreach (Canvas c in _hiddenCanvases) if (c != null) c.enabled = true;
            _hiddenDocs.Clear();
            _hiddenCanvases.Clear();
        }
    }

    /// <summary>Fades the screen to black (1) or back (0), in whole steps like the pixel art.</summary>
    public IEnumerator FadeScreen(float target, float duration)
    {
        if (_fade == null) yield break;
        float from = _fade.resolvedStyle.opacity;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(from, target, t / duration);
            SetScreenFade(Mathf.Round(a * 8f) / 8f);
            yield return null;
        }
        SetScreenFade(target);
    }

    public void SetScreenFade(float alpha)
    {
        if (_fade == null) return;
        _fade.style.opacity = alpha;
        _fade.pickingMode = alpha > 0.01f ? PickingMode.Position : PickingMode.Ignore;
    }

    /// <summary>The dream picture behind the day summary and skill pick (0..3, as dreamBackgrounds).</summary>
    public void ShowNight(int background)
    {
        if (_night == null) return;
        Texture2D bg = dreamBackgrounds != null && background >= 0 && background < dreamBackgrounds.Length
            ? dreamBackgrounds[background] : null;
        _night.style.backgroundImage = bg != null ? new StyleBackground(bg) : new StyleBackground(StyleKeyword.None);
        _night.RemoveFromClassList("rp-hidden");
    }

    public void HideNight() => _night?.AddToClassList("rp-hidden");

    // ── show / hide with motion ───────────────────────────────────────────
    // The logical state stays the "rp-hidden" class (IsShowingOverlay etc. read it). While an
    // element animates out it is kept on screen with an inline display:flex, which is cleared
    // when the motion ends so the class decides again.

    private static void ShowEl(VisualElement el, vc_UIAnim.Enter kind = vc_UIAnim.Enter.Pop, float delay = 0f)
    {
        if (el == null) return;
        el.RemoveFromClassList("rp-hidden");
        StyleEnum<Visibility> vis = el.style.visibility;   // Show forces visible; keep cinematic hiding
        vc_UIAnim.Show(el, kind, delay, () => el.style.display = StyleKeyword.Null);
        el.style.visibility = vis;
    }

    private static void HideEl(VisualElement el, vc_UIAnim.Exit kind = vc_UIAnim.Exit.Shrink)
    {
        if (el == null) return;
        bool shown = !el.ClassListContains("rp-hidden") || el.style.display == DisplayStyle.Flex;
        el.AddToClassList("rp-hidden");
        if (!shown) return;
        el.style.display = DisplayStyle.Flex;   // stay on screen while it animates out
        vc_UIAnim.Hide(el, () => el.style.display = StyleKeyword.Null, kind);
    }

    private static void SetShownEl(VisualElement el, bool shown, vc_UIAnim.Enter enter, vc_UIAnim.Exit exit)
    {
        if (el == null) return;
        bool hidden = el.ClassListContains("rp-hidden");
        if (shown && hidden) ShowEl(el, enter);
        else if (!shown && !hidden) HideEl(el, exit);
    }

    private static void SlideIn(VisualElement el, vc_UIAnim.Enter kind, float delay)
    {
        if (el == null || el.ClassListContains("rp-hidden")) return;
        ShowEl(el, kind, delay);
    }

    // ── building blocks ───────────────────────────────────────────────────

    /// <summary>
    /// Icon slot for a button: a .sn-btn__icon holder (+ .rp-icon--key, the art hook) with a
    /// drawn glyph inside. Art can replace the glyph from USS (see RunProgress.uss).
    /// </summary>
    public static VisualElement MakeIcon(HudIcon.Glyph glyph, string key)
    {
        var holder = new VisualElement { pickingMode = PickingMode.Ignore };
        holder.AddToClassList("sn-btn__icon");
        holder.AddToClassList("rp-icon");
        if (!string.IsNullOrEmpty(key)) holder.AddToClassList("rp-icon--" + key);
        var g = new HudIcon(glyph);
        g.AddToClassList("rp-glyph");
        holder.Add(g);
        return holder;
    }

    /// <summary>Pill button: [icon][label] (or [label][icon]), sn-btn + optional variant, press juice.</summary>
    private static Button MakePill(string text, HudIcon.Glyph glyph, string iconKey, string variant, Action onClick, bool iconAfter = false)
    {
        var btn = new Button();
        btn.AddToClassList("sn-btn");
        if (!string.IsNullOrEmpty(variant)) btn.AddToClassList("sn-btn--" + variant);
        VisualElement icon = null;   // Han: no icons on labelled buttons
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("sn-btn__label");
        if (icon == null) label.AddToClassList("rp-label--solo");
        if (icon != null && !iconAfter) btn.Add(icon);
        btn.Add(label);
        if (icon != null && iconAfter)
        {
            label.AddToClassList("rp-label--before");
            btn.Add(icon);
        }
        if (onClick != null) btn.clickable = new Clickable(onClick);
        vc_UIAnim.Press(btn);
        return btn;
    }

    private static Label PillLabel(Button b) => b.Q<Label>(className: "sn-btn__label");

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
                _labels[i].style.color = vc_UITheme.Riasec(Letters[i]);
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
                if (ring == 5) { p.fillColor = vc_UITheme.CreamHi; p.Fill(); }
                p.strokeColor = vc_UITheme.PaperLine;
                p.lineWidth = ring == 3 ? 2f : 1f;
                p.Stroke();
            }
            for (int i = 0; i < 6; i++)
            {
                p.BeginPath(); p.MoveTo(Point(i, 0f)); p.LineTo(Point(i, R));
                p.strokeColor = vc_UITheme.PaperLine; p.lineWidth = 1f; p.Stroke();
            }

            // the player's shape
            p.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                Vector2 v = Point(i, R * Mathf.Clamp(_values[i], 1f, 5f) / 5f);
                if (i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
            Color fill = vc_UITheme.Gold;
            fill.a = 0.5f;
            p.fillColor = fill;
            p.Fill();
            p.strokeColor = vc_UITheme.GoldDark;
            p.lineWidth = 3f;
            p.lineJoin = LineJoin.Round;
            p.Stroke();

            for (int i = 0; i < 6; i++)
            {
                Vector2 v = Point(i, R * Mathf.Clamp(_values[i], 1f, 5f) / 5f);
                p.BeginPath();
                p.Arc(v, 5f, 0f, 360f);
                p.fillColor = vc_UITheme.Riasec(Letters[i]);
                p.Fill();
            }
        }
    }

    /// <summary>
    /// The day dial: a cream disc with the current stage's progress around the rim and a
    /// little sun in the middle (the stage number sits on it). Drawn with Painter2D.
    /// </summary>
    public class ProgressRing : VisualElement
    {
        private static readonly Color[] StageColors = { vc_UITheme.Green, vc_UITheme.GoldDark, vc_UITheme.Blue };
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
            float r = Mathf.Min(rc.width, rc.height) * 0.5f - 4f;
            if (r <= 12f) return;
            Painter2D p = ctx.painter2D;
            Vector2 c = rc.center;

            // thicker bottom edge (same depth cue as sn-panel / sn-btn)
            p.BeginPath();
            p.Arc(c + new Vector2(0f, 4f), r + 1.5f, 0f, 360f);
            p.ClosePath();
            p.fillColor = vc_UITheme.Brown;
            p.Fill();

            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.ClosePath();
            p.fillColor = vc_UITheme.Cream;
            p.Fill();
            p.strokeColor = vc_UITheme.Brown;
            p.lineWidth = 3f;
            p.Stroke();

            // progress track + arc
            float tr = r - 9f;
            p.lineWidth = 7f;
            p.BeginPath();
            p.Arc(c, tr, 0f, 360f);
            p.strokeColor = vc_UITheme.CreamLo;
            p.Stroke();
            if (_progress > 0.001f)
            {
                p.BeginPath();
                p.Arc(c, tr, -90f, -90f + 360f * _progress);
                p.strokeColor = StageColors[Mathf.Clamp(StageIndex, 0, StageColors.Length - 1)];
                p.lineCap = LineCap.Round;
                p.Stroke();
            }

            // sun: rays + disc (the stage number label sits on top)
            float sr = r * 0.36f;
            p.lineCap = LineCap.Round;
            p.lineWidth = 3.5f;
            p.strokeColor = vc_UITheme.GoldDark;
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.Deg2Rad * (k * 45f + 22.5f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + dir * (sr + 4f));
                p.LineTo(c + dir * Mathf.Min(sr + 10f, tr - 6f));
                p.Stroke();
            }
            p.BeginPath();
            p.Arc(c, sr, 0f, 360f);
            p.ClosePath();
            p.fillColor = vc_UITheme.Gold;
            p.Fill();
            p.strokeColor = vc_UITheme.GoldDark;
            p.lineWidth = 2f;
            p.Stroke();
        }
    }

    /// <summary>
    /// Small vector icons for the HUD (no icon art yet). Sized by its element; Empty draws the
    /// "used up" version (energy bolt, star). Swap for art via the .rp-icon--key hook in USS.
    /// </summary>
    public class HudIcon : VisualElement
    {
        public enum Glyph { None, Moon, House, Talk, Book, Bolt, Star, Arrow, Check }

        private static readonly Color BlueDark = new Color32(44, 88, 150, 255);
        private Glyph _glyph;
        private bool _empty;

        public Glyph Kind
        {
            get => _glyph;
            set { _glyph = value; MarkDirtyRepaint(); }
        }

        public bool Empty
        {
            get => _empty;
            set { if (_empty == value) return; _empty = value; MarkDirtyRepaint(); }
        }

        public HudIcon(Glyph glyph)
        {
            _glyph = glyph;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect rc = contentRect;
            float s = Mathf.Min(rc.width, rc.height);
            if (s < 4f) return;
            Vector2 c = rc.center;
            Painter2D p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            Vector2 P(float x, float y) => c + new Vector2(x, y) * s;

            switch (_glyph)
            {
                case Glyph.Moon:
                {
                    // crescent opening to the right: outer arc minus an offset inner circle
                    float R = 0.40f * s, r = 0.34f * s, d = 0.26f * s;
                    Vector2 o = c + new Vector2(-0.06f * s, 0f);
                    float ix = (R * R - r * r + d * d) / (2f * d);
                    float iy = Mathf.Sqrt(Mathf.Max(0f, R * R - ix * ix));
                    float a0 = Mathf.Atan2(iy, ix) * Mathf.Rad2Deg;
                    float t0 = Mathf.Atan2(-iy, ix - d) * Mathf.Rad2Deg; if (t0 < 0f) t0 += 360f;
                    float t1 = Mathf.Atan2(iy, ix - d) * Mathf.Rad2Deg; if (t1 < 0f) t1 += 360f;
                    p.BeginPath();
                    p.Arc(o, R, a0, 360f - a0, ArcDirection.Clockwise);
                    p.Arc(o + new Vector2(d, 0f), r, t0, t1, ArcDirection.CounterClockwise);
                    p.ClosePath();
                    FillStroke(p, vc_UITheme.Gold, vc_UITheme.GoldDark, 0.06f * s);
                    break;
                }
                case Glyph.House:
                    Poly(p, P(0f, -0.42f), P(0.46f, 0f), P(0.34f, 0f), P(0.34f, 0.40f),
                            P(-0.34f, 0.40f), P(-0.34f, 0f), P(-0.46f, 0f));
                    FillStroke(p, vc_UITheme.Brown, vc_UITheme.BrownDark, 0.05f * s);
                    Poly(p, P(-0.10f, 0.40f), P(-0.10f, 0.12f), P(0.10f, 0.12f), P(0.10f, 0.40f));
                    p.fillColor = vc_UITheme.Gold;
                    p.Fill();
                    break;
                case Glyph.Talk:
                    Poly(p, P(-0.22f, 0.16f), P(-0.32f, 0.44f), P(0.04f, 0.16f));
                    p.fillColor = vc_UITheme.Blue;
                    p.Fill();
                    RoundRect(p, new Rect(c.x - 0.44f * s, c.y - 0.36f * s, 0.88f * s, 0.58f * s), 0.16f * s);
                    FillStroke(p, vc_UITheme.Blue, BlueDark, 0.04f * s);
                    for (int i = -1; i <= 1; i++) Dot(p, P(0.2f * i, -0.07f), 0.06f * s, vc_UITheme.CreamHi);
                    break;
                case Glyph.Book:
                    Poly(p, P(-0.48f, -0.24f), P(0f, -0.16f), P(0.48f, -0.24f), P(0.48f, 0.40f), P(0f, 0.46f), P(-0.48f, 0.40f));
                    p.fillColor = vc_UITheme.Brown;
                    p.Fill();
                    Poly(p, P(-0.42f, -0.32f), P(-0.03f, -0.24f), P(-0.03f, 0.38f), P(-0.42f, 0.30f));
                    FillStroke(p, vc_UITheme.CreamHi, vc_UITheme.BrownDark, 0.045f * s);
                    Poly(p, P(0.42f, -0.32f), P(0.03f, -0.24f), P(0.03f, 0.38f), P(0.42f, 0.30f));
                    FillStroke(p, vc_UITheme.CreamHi, vc_UITheme.BrownDark, 0.045f * s);
                    p.strokeColor = vc_UITheme.PaperLine;
                    p.lineWidth = 0.04f * s;
                    for (int i = 0; i < 2; i++)
                    {
                        float yy = -0.10f + 0.16f * i;
                        p.BeginPath(); p.MoveTo(P(-0.34f, yy)); p.LineTo(P(-0.12f, yy + 0.04f)); p.Stroke();
                        p.BeginPath(); p.MoveTo(P(0.34f, yy)); p.LineTo(P(0.12f, yy + 0.04f)); p.Stroke();
                    }
                    break;
                case Glyph.Bolt:
                    Poly(p, P(0.10f, -0.46f), P(-0.30f, 0.06f), P(-0.02f, 0.06f), P(-0.12f, 0.46f), P(0.30f, -0.08f), P(0.02f, -0.08f));
                    if (_empty) FillStroke(p, vc_UITheme.CreamLo, vc_UITheme.PaperLine, 0.07f * s);
                    else FillStroke(p, vc_UITheme.Gold, vc_UITheme.GoldDark, 0.07f * s);
                    break;
                case Glyph.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.Deg2Rad * (-90f + 36f * i);
                        float rr = (i % 2 == 0 ? 0.46f : 0.20f) * s;
                        pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr + new Vector2(0f, 0.03f * s);
                    }
                    Poly(p, pts);
                    if (_empty) FillStroke(p, vc_UITheme.CreamLo, vc_UITheme.PaperLine, 0.07f * s);
                    else FillStroke(p, vc_UITheme.Gold, vc_UITheme.GoldDark, 0.07f * s);
                    break;
                }
                case Glyph.Arrow:
                    p.strokeColor = vc_UITheme.Ink;
                    p.lineWidth = 0.13f * s;
                    p.BeginPath();
                    p.MoveTo(P(-0.32f, 0f)); p.LineTo(P(0.30f, 0f));
                    p.MoveTo(P(0.06f, -0.24f)); p.LineTo(P(0.32f, 0f)); p.LineTo(P(0.06f, 0.24f));
                    p.Stroke();
                    break;
                case Glyph.Check:
                    p.strokeColor = vc_UITheme.CreamHi;
                    p.lineWidth = 0.16f * s;
                    p.BeginPath();
                    p.MoveTo(P(-0.30f, 0.02f)); p.LineTo(P(-0.08f, 0.24f)); p.LineTo(P(0.32f, -0.24f));
                    p.Stroke();
                    break;
            }
        }

        private static void Poly(Painter2D p, params Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
        }

        private static void RoundRect(Painter2D p, Rect r, float rad)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + rad, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), rad);
            p.ClosePath();
        }

        private static void Dot(Painter2D p, Vector2 at, float r, Color col)
        {
            p.BeginPath();
            p.Arc(at, r, 0f, 360f);
            p.ClosePath();
            p.fillColor = col;
            p.Fill();
        }

        private static void FillStroke(Painter2D p, Color fill, Color edge, float width)
        {
            p.fillColor = fill;
            p.Fill();
            p.strokeColor = edge;
            p.lineWidth = width;
            p.Stroke();
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
