using System.Collections;
using UnityEngine;

/// <summary>
/// I quest (I8). Map the river bottom for Prof. Lim. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     a sonar ripple sweeps the river; depths pop up, the map fills   ~2s
///   unlock   I  workaround   drop a weighted rope at each buoy and read the depth            x3
///   plan     I  workaround   set up a buoy grid, then measure row by row                     5s
///   map      E  off-letter   sketch the map from the shore, slowly                           7s
///   collect  C  off-letter   gather the depth readings from the boat log                     8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (survey) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_RiverbedMapQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer mapBoard;
    [Tooltip("0 = blank, 1..3 = more contour lines.")]
    [SerializeField] private Sprite[] mapFrames;
    [SerializeField] private SpriteRenderer[] buoys;
    [SerializeField] private Sprite[] buoyFrames;
    [SerializeField] private Transform boat;

    [Header("Effects")]
    [SerializeField] private Sprite[] rippleFrames;
    [SerializeField] private Sprite ropeWeight;
    [Tooltip("Depth tags shown at buoy 0, 1, 2.")]
    [SerializeField] private Sprite[] depthTags;
    [SerializeField] private Sprite[] splashFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite gridDot;
    [SerializeField] private Sprite boatLog;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 2f;
    [SerializeField] private int ropePresses = 3;
    [SerializeField] private float planTime = 5f;
    [SerializeField] private float mapTime = 7f;
    [SerializeField] private float collectTime = 8f;

    private int _stage, _ropes;
    private float _bobClock, _rippleClock;
    private int _bobFrame;
    private Vector3 _boatRest;
    private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    protected override Vector3 BarWorldPosition =>
        (mapBoard != null ? mapBoard.transform.position : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

    private void Awake()
    {
        if (boat != null) _boatRest = boat.position;
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        _stage = 0; _ropes = 0;
        SetMap(0);
        _bobClock = 0f; _rippleClock = 1.5f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Riverbed Map",
            "Prof. Lim needs the river bottom mapped for the field trip.",
            new[] { "Map the riverbed" });
    }

    protected override void OnWorkCancelled() => ClearSpawned();

    // ── Idle life: bobbing buoys and boat, ripples ───────────────────────

    private void Update()
    {
        if (buoys == null) return;
        _bobClock += Time.deltaTime;
        if (_bobClock >= 0.5f)
        {
            _bobClock = 0f;
            _bobFrame ^= 1;
            for (int i = 0; i < buoys.Length; i++)
                if (buoys[i] != null && buoyFrames != null && buoyFrames.Length >= 2) buoys[i].sprite = buoyFrames[(_bobFrame + i) % 2];
            if (boat != null) boat.position = _boatRest + new Vector3(0f, _bobFrame / 22f, 0f);
        }
        if (!Started || Done) return;
        _rippleClock -= Time.deltaTime;
        if (_rippleClock <= 0f && buoys.Length > 0)
        {
            _rippleClock = Random.Range(1.4f, 2.4f);
            Fx(rippleFrames, BuoyBase(Random.Range(0, buoys.Length)), 5f, 0f, 2);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("unlock")) { RopePress(); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("map")) { StartWork(SketchRoutine()); return; }
        if (d.HasTag("collect")) { StartWork(LogRoutine()); return; }
        Toast("Mapping takes a careful eye.", 2.2f);
    }

    // ── Survey: sonar sweep ──────────────────────────────────────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("You sweep the river with sonar...", surveyTime);
        ShowBar(true);
        int n = buoys.Length;
        float per = (surveyTime - 0.2f) / Mathf.Max(1, n), t = 0f;
        for (int i = 0; i < n; i++)
        {
            SunodGame.Core.vc_Sfx.Play("scan", 0.5f, 0.1f);
            Fx(rippleFrames, BuoyBase(i), 8f, 0f, 3);
            Fx(rippleFrames, BuoyBase(i) + new Vector3(0.35f, -0.2f, 0f), 6f, 0f, 3);
            yield return BarStep(per * 0.5f, t / surveyTime, (t + per * 0.5f) / surveyTime);
            Reading(i);
            yield return BarStep(per * 0.5f, (t + per * 0.5f) / surveyTime, (t + per) / surveyTime);
            t += per;
        }
        Finish("Sonar done. The riverbed map is complete!");
    }

    // ── Solve: weighted rope at each buoy ────────────────────────────────

    private void RopePress()
    {
        if (_ropes >= buoys.Length) return;
        int i = _ropes++;
        StartCoroutine(DropRope(i));
        if (_ropes < Mathf.Min(ropePresses, buoys.Length))
        {
            Toast($"You read the depth at this buoy ({_ropes}/{ropePresses})", 2f);
            return;
        }
        StartCoroutine(FinishAfter(0.8f, "Every buoy measured. Map complete!"));
    }

    private IEnumerator FinishAfter(float s, string msg)
    {
        Working = true; // no new paths while the last rope sinks; best fit may still take over
        yield return new WaitForSeconds(s);
        Finish(msg);
    }

    private IEnumerator DropRope(int i)
    {
        Vector3 at = BuoyBase(i) + new Vector3(0.3f, 0f, 0f);
        GameObject rope = null;
        if (ropeWeight != null)
        {
            rope = Spawn("Rope", ropeWeight, at + new Vector3(0f, 0.9f, 0f), 6);
            float t = 0f;
            while (t < 0.45f && rope != null)
            {
                t += Time.deltaTime;
                rope.transform.position = Snap(at + new Vector3(0f, Mathf.Lerp(0.9f, 0.1f, t / 0.45f), 0f));
                yield return null;
            }
        }
        SunodGame.Core.vc_Sfx.Play("splash", 0.6f, 0.1f);
        Fx(splashFrames, at, 10f, 0f, 7);
        if (rope != null) { _spawned.Remove(rope); Destroy(rope); }
        Reading(i);
    }

    // ── Plan: buoy grid, then row by row ─────────────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("You set up a measuring grid...", planTime);
        ShowBar(true);
        float t = 0f, setup = 1.6f;
        // grid dots across the water
        if (gridDot != null && buoys.Length > 0)
        {
            Vector3 a = BuoyBase(0), c = BuoyBase(buoys.Length - 1);
            int k = 0;
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 2; col++)
                {
                    Vector3 p = Vector3.Lerp(a, c, r / 2f) + new Vector3(col * 0.7f - 0.35f, 0f, 0f);
                    Spawn("GridDot", gridDot, p, 3);
                    k++;
                }
            SunodGame.Core.vc_Sfx.Play("plop", 0.4f, 0.2f);
        }
        yield return BarStep(setup, 0f, setup / planTime);
        t = setup;
        int n = buoys.Length;
        float per = (planTime - t) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            Fx(rippleFrames, BuoyBase(i), 6f, 0f, 3);
            yield return BarStep(per * 0.6f, t / planTime, (t + per * 0.6f) / planTime);
            Reading(i);
            yield return BarStep(per * 0.4f, (t + per * 0.6f) / planTime, (t + per) / planTime);
            t += per;
        }
        Finish("Grid measured, row by row. Map done!");
    }

    // ── Map: sketch from the shore ───────────────────────────────────────

    private IEnumerator SketchRoutine()
    {
        Toast("You sketch the river from the shore...", mapTime);
        ShowBar(true);
        float t = 0f;
        int nextStage = 1;
        while (t < mapTime)
        {
            Fx(scribbleFrames, BoardCenter() + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.4f), 0f), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.3f, 0.15f, 0.3f);
            if (nextStage <= 3 && t >= mapTime * nextStage / 3.4f) { SetMap(nextStage); nextStage++; }
            yield return BarStep(0.45f, t / mapTime, (t + 0.45f) / mapTime);
            t += 0.45f;
        }
        SetMap(3);
        Finish("A slow sketch, but the map is done.");
    }

    // ── Collect: depth readings from the boat log ────────────────────────

    private IEnumerator LogRoutine()
    {
        Toast("You gather readings from the boat log...", collectTime);
        ShowBar(true);
        Vector3 at = (boat != null ? boat.position : transform.position) + new Vector3(0f, 0.9f, 0f);
        if (boatLog != null) Spawn("BoatLog", boatLog, at, 8);
        int n = buoys.Length;
        float per = collectTime / Mathf.Max(1, n), t = 0f;
        for (int i = 0; i < n; i++)
        {
            SunodGame.Core.vc_Sfx.Play("page_next", 0.5f, 0.1f);
            yield return BarStep(per * 0.7f, t / collectTime, (t + per * 0.7f) / collectTime);
            Reading(i);
            yield return BarStep(per * 0.3f, (t + per * 0.7f) / collectTime, (t + per) / collectTime);
            t += per;
        }
        Finish("All the boat's readings are on the map.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>A depth tag pops at buoy i and the map gains a contour.</summary>
    private void Reading(int i)
    {
        if (depthTags != null && i < depthTags.Length && depthTags[i] != null)
            Fx(new[] { depthTags[i] }, BuoyBase(i) + new Vector3(0f, 1.0f, 0f), 0.7f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("tally", 0.5f, 0.1f);
        _stage = Mathf.Min(3, _stage + 1);
        SetMap(_stage);
        Fx(sparkFrames, BoardCenter(), 12f, 0.1f);
    }

    private void SetMap(int s)
    {
        if (mapBoard != null && mapFrames != null && s < mapFrames.Length && mapFrames[s] != null) mapBoard.sprite = mapFrames[s];
    }

    private Vector3 BuoyBase(int i) => buoys[i] != null ? buoys[i].transform.position : transform.position;

    private Vector3 BoardCenter()
    {
        if (mapBoard == null) return transform.position;
        Bounds b = mapBoard.bounds;
        return new Vector3(b.center.x, b.center.y + b.extents.y * 0.25f, 0f);
    }

    private GameObject Spawn(string name, Sprite s, Vector3 at, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(at);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return go;
    }

    private void ClearSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    private IEnumerator BarStep(float seconds, float k0, float k1)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            yield return null;
        }
    }
}
