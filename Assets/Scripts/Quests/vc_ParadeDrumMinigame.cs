using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// A quest (A6), minigame version of vc_ParadeDrumQuest ("New" gameplay, see vc_GameplayMode):
/// "Keep the beat". Paolo plays a short drum pattern, the player copies it on a big drum pad.
///   - One-time tap check per phone (saved on the device, kept across loops): tap along to a
///     steady beat; how unsteady the taps are widens the timing windows a little.
///   - Judged on the GAPS between taps, not on when the first tap lands, so phone touch lag
///     cancels out. Wide windows: Perfect within 120 ms, Good within 250 ms (+ tap check).
///   - 3 rounds, from a pool of patterns; each loop draws a different set.
/// Score per round = (Perfect + 0.6 x Good - 0.25 x extra taps) / gaps. Average of the rounds:
///   85%+ = 5 stars, 65%+ = 4, 45%+ = 3, else 2 (1 if the player never tapped).
/// No countdown: the stars come from the minigame (vc_QuestTimer.SetStarOverride).
/// </summary>
[DisallowMultipleComponent]
public class vc_ParadeDrumMinigame : vc_FitQuestBase
{
    [Header("Drum")]
    [SerializeField] private SpriteRenderer drum;
    [SerializeField] private Sprite drumDented;
    [SerializeField] private Sprite drumClean;

    [Header("Effects")]
    [SerializeField] private Sprite[] notes;
    [SerializeField] private Sprite[] ringFrames;

    [Header("Minigame")]
    [Tooltip("Seconds per beat for the patterns and the tap check.")]
    [SerializeField] private float beat = 0.5f;
    [SerializeField] private int rounds = 3;

    private const float PerfectMs = 120f, GoodMs = 250f;
    private const string JitterKey = "sunod.drum_jitter_ms";

    /// <summary>
    /// Patterns as gaps between hits, in beats. Ordered roughly easy to hard; each loop draws
    /// its own set (one from each third of the list).
    /// </summary>
    private static readonly float[][] Patterns =
    {
        new[] { 1f, 1f, 1f },
        new[] { 1f, 0.5f, 0.5f },
        new[] { 0.5f, 0.5f, 1f },
        new[] { 1f, 1f, 0.5f, 0.5f },
        new[] { 0.5f, 0.5f, 1f, 1f },
        new[] { 1f, 0.5f, 0.5f, 1f },
        new[] { 0.5f, 1f, 0.5f, 1f },
        new[] { 1f, 0.5f, 0.5f, 0.5f, 0.5f },
        new[] { 0.5f, 0.5f, 0.5f, 0.5f, 1f },
        new[] { 0.5f, 1f, 0.5f, 0.5f, 0.5f },
        new[] { 1f, 0.5f, 1f, 0.5f, 0.5f },
        new[] { 0.5f, 0.5f, 1f, 0.5f, 0.5f },
    };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Testing only: when 0 or more, the minigame taps for the player with this much random gap error (ms).</summary>
    public static float DebugAutoTapJitterMs = -1f;
#endif

    private vc_MinigameOverlay _ui;
    private Vector3 _drumRest;
    private readonly List<double> _taps = new List<double>();
    private bool _listening;
    private VisualElement _pad;
    private readonly List<VisualElement> _dots = new List<VisualElement>();

    protected override Vector3 BarWorldPosition => (drum != null ? drum.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);
    // minigames score themselves: no press limit or skill XP
    protected override bool UsesSkillSystem => false;

    private void Awake()
    {
        if (drum != null) _drumRest = drum.transform.localPosition;
        _ui = GetComponent<vc_MinigameOverlay>();
        if (_ui == null) _ui = gameObject.AddComponent<vc_MinigameOverlay>();
    }

    protected override void OnBegin()
    {
        if (drum != null)
        {
            drum.transform.localPosition = _drumRest;
            if (drumDented != null) drum.sprite = drumDented;
        }
        if (vc_QuestTimer.Instance != null)
        {
            vc_QuestTimer.Instance.ExternalHold = true;
            vc_QuestTimer.Instance.SetStarsVisible(false);
        }
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Parade Drum",
            "Paolo needs a drummer for the parade. Copy his beat.",
            new[] { "Copy Paolo's beat" });
        StartWork(Flow());
    }

    protected override void OnSkill(vc_SkillData d) => Toast("Listen to Paolo's beat, then tap the drum.", 2f);

    protected override void OnWorkCancelled()
    {
        _listening = false;
        _ui?.Close();
        if (drum != null) drum.transform.localPosition = _drumRest;
    }

    // ── Flow ──────────────────────────────────────────────────────────────

    private IEnumerator Flow()
    {
        yield return new WaitForSeconds(0.5f);

        if (!PlayerPrefs.HasKey(JitterKey))
            yield return Calibrate();
        float jitter = PlayerPrefs.GetFloat(JitterKey, 60f);
        float allowance = Mathf.Clamp(jitter * 0.5f, 0f, 80f);   // shaky phones get a little more room

        List<float[]> set = PatternsForThisLoop();
        float total = 0f;
        bool anyTap = false;
        for (int r = 0; r < set.Count; r++)
        {
            float[] gaps = set[r];
            _ui.Open($"Round {r + 1} of {set.Count}");
            BuildPad(gaps.Length + 1);

            _ui.SetMessage("Listen...", vc_MinigameOverlay.Ink);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PlayPattern(gaps);

            _ui.SetMessage("Your turn!", vc_MinigameOverlay.Good);
            yield return Listen(gaps);
            if (_taps.Count > 0) anyTap = true;

            (float acc, int perfect, int good, int miss) = Judge(gaps, _taps, allowance, beat);
            total += acc;
            _ui.SetMessage($"Perfect {perfect}   Good {good}   Miss {miss}",
                acc >= 0.65f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
            yield return new WaitForSecondsRealtime(1.3f);
        }
        _ui.Close();

        int pct = Mathf.RoundToInt(total / Mathf.Max(1, set.Count) * 100f);
        int stars = !anyTap ? 1 : pct >= 85 ? 5 : pct >= 65 ? 4 : pct >= 45 ? 3 : 2;
        vc_QuestTimer.Instance?.SetStarOverride(stars);
        Debug.Log($"[Minigame] A6 drum: loop {vc_LoopMeta.Loop}, accuracy {pct}%, jitter {jitter:0} ms -> {stars} stars");

        yield return Payoff();
        Finish(pct >= 45 ? "The parade has its beat!" : "Paolo nods. Good enough to march to!");
    }

    /// <summary>A different set of patterns each loop (one from each third of the list).</summary>
    private List<float[]> PatternsForThisLoop()
    {
        var rng = new System.Random(7919 * Mathf.Max(1, vc_LoopMeta.Loop) + 13);
        var set = new List<float[]>();
        int n = Mathf.Clamp(rounds, 1, 3);
        int third = Patterns.Length / 3;
        for (int i = 0; i < n; i++) set.Add(Patterns[i * third + rng.Next(third)]);
        return set;
    }

    // ── Tap check (once per phone) ────────────────────────────────────────

    private IEnumerator Calibrate()
    {
        _ui.Open("Tap check");
        _ui.SetMessage("Tap the drum on every flash. 8 beats.", vc_MinigameOverlay.Ink);
        BuildPad(8);
        yield return new WaitForSecondsRealtime(1.2f);

        _taps.Clear();
        _listening = true;
        double start = Time.realtimeSinceStartupAsDouble;
        for (int i = 0; i < 8; i++)
        {
            while (Time.realtimeSinceStartupAsDouble < start + i * beat * 1.2f) { AutoTap(start, i, beat * 1.2f); yield return null; }
            Flash(i);
            SunodGame.Core.vc_Sfx.Play("beep", 0.5f);
        }
        float wait = beat * 1.2f;
        while (wait > 0f) { wait -= Time.unscaledDeltaTime; AutoTap(start, 8, beat * 1.2f); yield return null; }
        _listening = false;

        // steadiness = how much the gaps between taps wander around the beat
        float jitter = 60f;
        if (_taps.Count >= 4)
        {
            float sum = 0f;
            for (int i = 1; i < _taps.Count; i++) sum += Mathf.Abs((float)(_taps[i] - _taps[i - 1]) - beat * 1.2f) * 1000f;
            jitter = Mathf.Clamp(sum / (_taps.Count - 1), 0f, 160f);
        }
        PlayerPrefs.SetFloat(JitterKey, jitter);
        PlayerPrefs.Save();
        _ui.SetMessage("Got it!", vc_MinigameOverlay.Good);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    // ── Paolo plays, the player copies ────────────────────────────────────

    private IEnumerator PlayPattern(float[] gaps)
    {
        Hit(0);
        for (int i = 0; i < gaps.Length; i++)
        {
            yield return new WaitForSecondsRealtime(gaps[i] * beat);
            Hit(i + 1);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        ResetDots();
    }

    private IEnumerator Listen(float[] gaps)
    {
        _taps.Clear();
        _listening = true;
        float length = 0f;
        foreach (float g in gaps) length += g * beat;

        // up to 5 s to start; then until every hit is in, or the pattern's length + 1.2 s passes
        double opened = Time.realtimeSinceStartupAsDouble;
        while (_taps.Count == 0 && Time.realtimeSinceStartupAsDouble - opened < 5.0)
        {
            AutoTapPattern(gaps);
            yield return null;
        }
        while (_taps.Count > 0 && _taps.Count < gaps.Length + 1
               && Time.realtimeSinceStartupAsDouble - _taps[0] < length + 1.2f)
        {
            AutoTapPattern(gaps);
            yield return null;
        }
        _listening = false;
        yield return new WaitForSecondsRealtime(0.25f);
    }

    /// <summary>Accuracy 0..1 from the gaps between taps, plus the Perfect / Good / Miss counts.</summary>
    private static (float acc, int perfect, int good, int miss) Judge(float[] gaps, List<double> taps, float allowanceMs, float beatSeconds)
    {
        int perfect = 0, good = 0;
        int playerGaps = Mathf.Max(0, taps.Count - 1);
        for (int i = 0; i < gaps.Length && i < playerGaps; i++)
        {
            float err = Mathf.Abs((float)(taps[i + 1] - taps[i]) - gaps[i] * beatSeconds) * 1000f;
            err = Mathf.Max(0f, err - allowanceMs);
            if (err <= PerfectMs) perfect++;
            else if (err <= GoodMs) good++;
        }
        int miss = gaps.Length - perfect - good;
        int extra = Mathf.Max(0, taps.Count - (gaps.Length + 1));
        float acc = Mathf.Clamp01((perfect + 0.6f * good - 0.25f * extra) / gaps.Length);
        return (acc, perfect, good, miss);
    }

    // ── Pad UI ────────────────────────────────────────────────────────────

    private void BuildPad(int hits)
    {
        _ui.Content.Clear();
        _dots.Clear();

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 14;
        for (int i = 0; i < hits; i++)
        {
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.style.width = 30; dot.style.height = 30;
            dot.style.marginLeft = 6; dot.style.marginRight = 6;
            dot.style.borderTopLeftRadius = 15; dot.style.borderTopRightRadius = 15;
            dot.style.borderBottomLeftRadius = 15; dot.style.borderBottomRightRadius = 15;
            dot.style.backgroundColor = new Color(0.82f, 0.76f, 0.66f);
            row.Add(dot);
            _dots.Add(dot);
        }
        _ui.Content.Add(row);

        _pad = new VisualElement { pickingMode = PickingMode.Position };
        _pad.style.width = 300; _pad.style.height = 220;
        _pad.style.backgroundColor = new Color(0.86f, 0.36f, 0.26f);
        _pad.style.borderTopLeftRadius = 110; _pad.style.borderTopRightRadius = 110;
        _pad.style.borderBottomLeftRadius = 110; _pad.style.borderBottomRightRadius = 110;
        _pad.style.borderBottomWidth = 12;
        _pad.style.borderBottomColor = new Color(0.55f, 0.20f, 0.14f);
        _pad.style.alignItems = Align.Center;
        _pad.style.justifyContent = Justify.Center;
        var label = vc_MinigameOverlay.MakeLabel("TAP", 48);
        label.style.color = Color.white;
        _pad.Add(label);
        // pointer DOWN, not click (which waits for release): taps are timed on touch
        _pad.RegisterCallback<PointerDownEvent>(_ => Tap());
        _ui.Content.Add(_pad);
    }

    private void Update()
    {
        if (_listening && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Tap();
    }

    private void Tap()
    {
        if (!_listening) return;
        _taps.Add(Time.realtimeSinceStartupAsDouble);
        SunodGame.Core.vc_Sfx.Play("drum_hit", 0.8f, 0.04f);
        if (_taps.Count - 1 < _dots.Count) _dots[_taps.Count - 1].style.backgroundColor = new Color(0.98f, 0.78f, 0.25f);
        if (_pad != null) StartCoroutine(PadPress());
        if (drum != null) StartCoroutine(Bounce());
    }

    private void Hit(int i)
    {
        SunodGame.Core.vc_Sfx.Play("drum_hit", 0.8f, 0.04f);
        Flash(i);
        if (drum != null) StartCoroutine(Bounce());
        if (notes != null && notes.Length > 0 && drum != null)
            Fx(new[] { notes[i % notes.Length] }, drum.transform.position + new Vector3(0.3f, 0.9f, 0f), 2f, 0.4f, 25);
    }

    private void Flash(int i)
    {
        if (i < _dots.Count) _dots[i].style.backgroundColor = new Color(0.98f, 0.78f, 0.25f);
        if (_pad != null) StartCoroutine(PadPress());
    }

    private void ResetDots()
    {
        foreach (VisualElement d in _dots) d.style.backgroundColor = new Color(0.82f, 0.76f, 0.66f);
    }

    private IEnumerator PadPress()
    {
        _pad.style.scale = new Scale(new Vector3(0.94f, 0.94f, 1f));
        yield return new WaitForSecondsRealtime(0.08f);
        if (_pad != null) _pad.style.scale = new Scale(Vector3.one);
    }

    private IEnumerator Bounce()
    {
        drum.transform.localPosition = _drumRest + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(0.06f);
        if (drum != null) drum.transform.localPosition = _drumRest;
    }

    private IEnumerator Payoff()
    {
        if (drum == null) yield break;
        if (drumClean != null) drum.sprite = drumClean;
        Fx(ringFrames, drum.transform.position + new Vector3(0f, 0.5f, 0f), 10f, 0f, 24);
        SunodGame.Core.vc_Sfx.Play("cheer", 0.6f);
        for (int i = 0; i < 4; i++)
        {
            if (notes != null && notes.Length > 0)
                Fx(new[] { notes[i % notes.Length] }, drum.transform.position + new Vector3(-0.6f + i * 0.4f, 0.9f, 0f), 2f, 0.6f, 25);
            yield return Bounce();
            yield return new WaitForSeconds(0.18f);
        }
    }

    // ── Testing only: auto taps ───────────────────────────────────────────

    private void AutoTap(double start, int beatIndex, float period)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugAutoTapJitterMs < 0f || !_listening) return;
        int due = Mathf.Min(8, beatIndex + 1);
        if (_taps.Count < due && Time.realtimeSinceStartupAsDouble >= start + _taps.Count * period + 0.08)
            Tap();
#endif
    }

    private double _autoNext = -1;

    private void AutoTapPattern(float[] gaps)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugAutoTapJitterMs < 0f || !_listening) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (_taps.Count == 0) { if (_autoNext < 0) _autoNext = now + 0.4; }
        if (_autoNext >= 0 && now >= _autoNext && _taps.Count < gaps.Length + 1)
        {
            Tap();
            if (_taps.Count < gaps.Length + 1)
                _autoNext = now + gaps[_taps.Count - 1] * beat + Random.Range(-DebugAutoTapJitterMs, DebugAutoTapJitterMs) / 1000.0;
            else _autoNext = -1;
        }
#endif
    }
}
