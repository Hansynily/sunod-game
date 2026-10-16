using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// I quest (I6). Find out how many fish live in the pond. Any APPROPRIATE skill works - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     scan the pond; a tally number pops over every fish           ~1.5s
///   plan     I  workaround   lay a counting grid over the water, tick off row by row        4s
///   unlock   I  workaround   work out how they school, then count them in formation         5s
///   collect  C  off-letter   net each fish into a bucket, count them, let them go            8s
///   walking up to the far rocks still works with no skill (existing): you see every fish
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (survey) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_FishCountQuest : vc_FitQuestBase
{
    [Header("Pond")]
    [SerializeField] private Transform pond;
    [SerializeField] private Transform[] fish;
    [Tooltip("3 looks x 2 tail frames: look*2 + frame.")]
    [SerializeField] private Sprite[] fishFrames;
    [SerializeField] private Transform rocks;
    [SerializeField] private float rocksRange = 1f;

    [Header("Props")]
    [SerializeField] private SpriteRenderer notes;
    [SerializeField] private Transform bucket;
    [SerializeField] private SpriteRenderer net;

    [Header("Effects")]
    [SerializeField] private Sprite[] digits;
    [SerializeField] private Sprite[] rippleFrames;
    [SerializeField] private Sprite[] splashFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] checkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 1.5f;
    [SerializeField] private float planTime = 4f;
    [SerializeField] private float solveTime = 5f;
    [SerializeField] private float collectCatchTime = 5.4f;
    [SerializeField] private float collectCountTime = 1.1f;
    [SerializeField] private float collectReleaseTime = 1.5f;

    private class FishState
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float cx, cy, rx, ry, speed, phase;
        public int look;
        public bool hidden;
        public Vector3 pos, overridePos;
    }

    private readonly List<FishState> _fish = new List<FishState>();
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Sprite _px;
    private float _t, _w, _rippleClock, _formationW;
    private Vector3 _netRest;
    private Transform _player;

    private Vector3 PondCenter => pond != null ? pond.position : transform.position;
    protected override Vector3 BarWorldPosition => PondCenter + new Vector3(-0.5f, 2.4f, 0f);

    private void Awake()
    {
        var rng = new System.Random(11);
        System.Func<double, double, float> R = (a, b) => (float)(a + rng.NextDouble() * (b - a));
        Vector3 c = PondCenter;
        for (int i = 0; fish != null && i < fish.Length; i++)
        {
            var f = new FishState
            {
                tr = fish[i], sr = fish[i].GetComponent<SpriteRenderer>(),
                cx = c.x + R(-0.15, 0.15), cy = c.y + R(-0.1, 0.1),
                rx = R(0.55, 1.55), ry = R(0.35, 1.05),
                speed = R(0.35, 0.7) * (i % 2 == 0 ? 1f : -1f),
                phase = R(0, 6.28), look = i % 3
            };
            f.pos = Orbit(f, 0f);
            _fish.Add(f);
        }
        if (net != null) _netRest = net.transform.position;
    }

    private Vector3 Orbit(FishState f, float t)
    {
        float a = f.phase + f.speed * t;
        return new Vector3(f.cx + Mathf.Cos(a) * f.rx, f.cy + Mathf.Sin(a) * f.ry * (1f + 0.1f * Mathf.Sin(t * 0.7f + f.phase)), 0f);
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _w = 0f;
        foreach (FishState f in _fish) { f.hidden = false; f.sr.enabled = true; }
        if (notes != null) notes.gameObject.SetActive(false);
        if (net != null) { net.gameObject.SetActive(false); net.transform.position = _netRest; }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Fish Count",
            "Find out how many fish live in this pond.",
            new[] { "Count the fish" });
    }

    // ── Idle life: swimming fish, ripples; walk-to-the-rocks ─────────────

    private void Update()
    {
        _t += Time.deltaTime;
        for (int i = 0; i < _fish.Count; i++)
        {
            FishState f = _fish[i];
            Vector3 orbit = Orbit(f, _t);
            Vector3 target = _w > 0.001f ? Vector3.Lerp(orbit, f.overridePos, _w) : orbit;
            float dx = target.x - f.pos.x;
            if (Mathf.Abs(dx) > 0.0005f) f.sr.flipX = dx < 0f;
            f.pos = target;
            f.tr.position = Snap(target);
            int frame = ((int)(_t * 4f + f.phase * 3f)) % 2;
            int k = f.look * 2 + frame;
            if (fishFrames != null && k < fishFrames.Length) f.sr.sprite = fishFrames[k];
        }

        if (!Done)
        {
            _rippleClock += Time.deltaTime;
            if (_rippleClock >= 0.8f && _fish.Count > 0)
            {
                _rippleClock = Random.Range(0f, 0.4f);
                FishState f = _fish[Random.Range(0, _fish.Count)];
                if (!f.hidden) Fx(rippleFrames, f.pos + new Vector3(0f, -0.1f, 0f), 5f, 0f, 3);
            }
        }

        if (!Started || Done || Working || rocks == null) return;
        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        if (_player != null && Vector2.Distance(_player.position, rocks.position) <= rocksRange)
            StartWork(OverviewRoutine());
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }

        if (BlockedByWork()) return;

        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }

        Toast("Counting fish needs a sharp eye.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        _w = 0f;
        foreach (FishState f in _fish) { f.hidden = false; if (f.sr != null) f.sr.enabled = true; }
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        if (net != null) { net.gameObject.SetActive(false); net.transform.position = _netRest; }
    }

    // ── Survey: a scan line sweeps the pond, tally numbers pop ───────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Scanning the pond...", surveyTime);
        ShowBar(true);
        SunodGame.Core.vc_Sfx.Play("scan", 0.5f);

        Vector3 c = PondCenter;
        float sweep = surveyTime * 0.8f, t = 0f;
        Transform line = MakeQuad("ScanLine", new Color(0.75f, 0.95f, 1f, 0.85f), new Vector2(0.07f, 3.9f), 6);
        Transform glow = MakeQuad("ScanGlow", new Color(0.75f, 0.95f, 1f, 0.18f), new Vector2(0.45f, 3.9f), 5);
        var tagged = new HashSet<FishState>();
        int count = 0;

        while (t < sweep)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(c.x - 2.3f, c.x + 2.3f, t / sweep);
            line.position = glow.position = Snap(new Vector3(x, c.y, 0f));
            foreach (FishState f in _fish)
                if (!tagged.Contains(f) && f.pos.x <= x) { tagged.Add(f); Tally(f, ++count); }
            SetBar(t / surveyTime);
            yield return null;
        }
        foreach (FishState f in _fish) if (!tagged.Contains(f)) { tagged.Add(f); Tally(f, ++count); }
        if (line != null) Destroy(line.gameObject);
        if (glow != null) Destroy(glow.gameObject);

        ShowNotes();
        Fx(checkFrames, c + new Vector3(0f, 2.0f, 0f), 2f, 0.2f, 24);
        StartCoroutine(PopNumber(_fish.Count, c + new Vector3(0f, 1.6f, 0f), 1.6f, 1.2f));
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);
        yield return BarStep(Mathf.Max(0.05f, surveyTime - sweep), sweep / surveyTime, 1f);
        Finish($"Counted them all: {_fish.Count} fish!");
    }

    private void Tally(FishState f, int n)
    {
        StartCoroutine(PopNumber(n, f.pos + new Vector3(0f, 0.45f, 0f), 1f, 0.7f));
        SunodGame.Core.vc_Sfx.Play("tally", 0.5f, 0.06f, 0.04f);
    }

    // ── Plan: a counting grid, row by row ────────────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Laying a counting grid over the pond...", planTime);
        ShowBar(true);
        Vector3 c = PondCenter;
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);

        var lineColor = new Color(1f, 1f, 1f, 0.55f);
        for (int i = 0; i < 4; i++)
            MakeQuad("GridH", lineColor, new Vector2(4.7f, 0.0625f), 6).position = Snap(c + new Vector3(0f, -1.875f + 1.25f * i, 0f));
        for (int i = 0; i < 5; i++)
            MakeQuad("GridV", lineColor, new Vector2(0.0625f, 3.75f), 6).position = Snap(c + new Vector3(-2.3f + 1.15f * i, 0f, 0f));

        float t = 0f;
        yield return BarStep(0.6f, 0f, 0.6f / planTime);
        t = 0.6f;

        // assign each fish to a band once, so the rows always add up to the real total
        var counts = new int[3];
        foreach (FishState f in _fish)
        {
            int band = f.pos.y > c.y + 0.625f ? 0 : (f.pos.y < c.y - 0.625f ? 2 : 1);
            counts[band]++;
        }

        for (int b = 0; b < 3; b++)
        {
            Transform hi = MakeQuad("Row", new Color(1f, 1f, 1f, 0.22f), new Vector2(4.6f, 1.22f), 5);
            hi.position = Snap(c + new Vector3(0f, 1.25f - 1.25f * b, 0f));
            StartCoroutine(PopNumber(counts[b], c + new Vector3(2.55f, 1.25f - 1.25f * b, 0f), 1f, 1.0f));
            SunodGame.Core.vc_Sfx.Play("tally", 0.6f, 0.05f);
            yield return BarStep(1.0f, t / planTime, (t + 1.0f) / planTime);
            t += 1.0f;
        }

        StartCoroutine(PopNumber(_fish.Count, c + new Vector3(0f, 2.15f, 0f), 1.6f, 1.2f));
        Fx(checkFrames, c + new Vector3(1.0f, 2.2f, 0f), 2f, 0.2f, 24);
        ShowNotes();
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        yield return BarStep(Mathf.Max(0.05f, planTime - t), t / planTime, 1f);
        Finish($"Row by row, the grid says {_fish.Count} fish!");
    }

    // ── Solve: work out how they school, count them in formation ─────────

    private IEnumerator SolveRoutine()
    {
        Toast("Working out how the fish move together...", solveTime);
        ShowBar(true);
        Vector3 c = PondCenter + new Vector3(0f, 0.2f, 0f);
        float gather = 2.2f, school = 1.6f, release = solveTime - gather - school;
        float t = 0f;
        int tallied = 0;
        bool bulb = false;

        while (t < solveTime)
        {
            t += Time.deltaTime;
            SetBar(t / solveTime);

            Vector3 fc = c;
            if (t > gather) { float a = (t - gather) * 3f; fc = c + new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.3f, 0f); }
            for (int i = 0; i < _fish.Count; i++)
                _fish[i].overridePos = fc + new Vector3(-1.25f + 0.5f * (i % 6), 0.3f - 0.6f * (i / 6), 0f);

            if (t < gather) _w = Mathf.SmoothStep(0f, 1f, t / gather);
            else if (t < gather + school) _w = 1f;
            else _w = Mathf.SmoothStep(1f, 0f, (t - gather - school) / Mathf.Max(0.01f, release));

            if (t > gather && tallied < _fish.Count && t - gather >= tallied * (school / _fish.Count))
            {
                Tally(_fish[tallied], tallied + 1);
                tallied++;
            }
            if (!bulb && t >= gather + school)
            {
                bulb = true;
                Fx(bulbFrames, c + new Vector3(0f, 1.7f, 0f), 2f, 0.3f, 25);
                StartCoroutine(PopNumber(_fish.Count, c + new Vector3(0.8f, 1.7f, 0f), 1.4f, 1.2f));
                SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
            }
            yield return null;
        }
        _w = 0f;
        ShowNotes();
        Finish($"They school in rows. All {_fish.Count} counted!");
    }

    // ── Collect: net each fish into the bucket, count, release ───────────

    private IEnumerator CollectRoutine()
    {
        Toast("Netting the fish into a bucket...", collectCatchTime);
        ShowBar(true);
        float total = collectCatchTime + collectCountTime + collectReleaseTime, t = 0f;
        int n = _fish.Count;
        float per = collectCatchTime / Mathf.Max(1, n);
        Vector3 home = (bucket != null ? bucket.position : transform.position) + new Vector3(0.1f, 0.55f, 0f);
        net.gameObject.SetActive(true);
        net.transform.position = Snap(home);

        for (int i = 0; i < n; i++)
        {
            FishState f = _fish[i];
            float leg = per * 0.38f;
            yield return MoveNet(home, f, leg);
            f.hidden = true; f.sr.enabled = false;
            Fx(splashFrames, f.pos, 10f, 0.1f, 4);
            SunodGame.Core.vc_Sfx.Play("plop", 0.5f, 0.1f);
            yield return MoveNetTo(f.pos, home, leg);
            if (bucket != null) StartCoroutine(Shake(bucket, bucket.localPosition, 0.12f, 1));
            StartCoroutine(PopNumber(i + 1, home + new Vector3(0.1f, 0.5f, 0f), 1f, 0.45f));
            SunodGame.Core.vc_Sfx.Play("tally", 0.45f, 0.05f, 0.04f);
            float rest = per - leg * 2f;
            if (rest > 0f) yield return new WaitForSeconds(rest);
            t += per;
            SetBar(t / total);
        }

        StartCoroutine(PopNumber(n, home + new Vector3(0.1f, 0.95f, 0f), 1.6f, 1.1f));
        Fx(checkFrames, home + new Vector3(0.9f, 1.0f, 0f), 2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);
        ShowNotes();
        yield return BarStep(collectCountTime, t / total, (t + collectCountTime) / total);
        t += collectCountTime;
        net.gameObject.SetActive(false);

        float relPer = collectReleaseTime / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            FishState f = _fish[i];
            f.hidden = false; f.sr.enabled = true;
            Fx(splashFrames, f.pos, 10f, 0.1f, 4);
            SunodGame.Core.vc_Sfx.Play("plop", 0.3f, 0.15f, 0.05f);
            yield return BarStep(relPer, (t + i * relPer) / total, (t + (i + 1) * relPer) / total);
        }
        Finish($"Netted, counted and let go: {n} fish!");
    }

    private IEnumerator MoveNet(Vector3 from, FishState target, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            net.transform.position = Snap(Vector3.Lerp(from, target.pos, t / seconds));
            yield return null;
        }
    }

    private IEnumerator MoveNetTo(Vector3 from, Vector3 to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            net.transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
            yield return null;
        }
    }

    // ── Walk to the rocks: see them all from above ───────────────────────

    private IEnumerator OverviewRoutine()
    {
        Toast("From up here you can see every fish!", 2f);
        for (int i = 0; i < _fish.Count; i++)
        {
            Tally(_fish[i], i + 1);
            yield return new WaitForSeconds(0.07f);
        }
        ShowNotes();
        yield return new WaitForSeconds(0.2f);
        Finish("From up on the rocks, you can see every fish!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void ShowNotes()
    {
        if (notes == null || notes.gameObject.activeSelf) return;
        notes.gameObject.SetActive(true);
        StartCoroutine(PopScale(notes.transform));
    }

    private IEnumerator PopScale(Transform tr)
    {
        float[] s = { 0.4f, 0.7f, 1.2f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.06f);
        }
    }

    /// <summary>A tally number (1-2 digits) that rises and vanishes.</summary>
    private IEnumerator PopNumber(int n, Vector3 pos, float scale, float life)
    {
        if (digits == null || digits.Length < 10) yield break;
        string s = n.ToString();
        var root = new GameObject("Num");
        root.transform.SetParent(transform, true);
        root.transform.position = Snap(pos);
        root.transform.localScale = Vector3.one * scale;
        float w = 0.3f;
        for (int i = 0; i < s.Length; i++)
        {
            var go = new GameObject("d");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3((i - (s.Length - 1) * 0.5f) * w, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = digits[s[i] - '0'];
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = 24;
        }
        float t = 0f;
        while (t < life && root != null)
        {
            t += Time.deltaTime;
            root.transform.position = Snap(pos + new Vector3(0f, 0.35f * Mathf.Min(1f, t / 0.4f), 0f));
            yield return null;
        }
        if (root != null) Destroy(root);
    }

    private Transform MakeQuad(string name, Color color, Vector2 size, int order)
    {
        if (_px == null) _px = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _px; sr.color = color;
        sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return go.transform;
    }

    private IEnumerator BarStep(float seconds, float k0, float k1)
    {
        float e = 0f;
        while (e < seconds)
        {
            e += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, e / seconds));
            yield return null;
        }
    }
}
