using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C quest (C5). Count the harvest and write the totals down. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   inspect  C  best fit     count each basket; tally marks appear on the board             ~2s
///   collect  C  workaround   gather the baskets into one spot, then count them               4s
///   direct   C  workaround   line the baskets up in a row and write the totals               5s
///   plan     I  off-letter   rule a tally sheet first, then fill it in                       7s
///   walking to the shed and reading the farmer's notes still works with no skill (existing)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (inspect) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_HarvestTallyQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer[] baskets;
    [Tooltip("How many tomatoes each basket holds (same order as baskets).")]
    [SerializeField] private int[] counts = { 4, 6, 3 };
    [SerializeField] private Transform[] gatherSlots;
    [SerializeField] private Transform[] rowSlots;
    [SerializeField] private SpriteRenderer board;
    [SerializeField] private Sprite[] boardFrames;
    [SerializeField] private Transform shed;
    [SerializeField] private Transform scarecrow;
    [SerializeField] private SpriteRenderer[] crows;
    [SerializeField] private Sprite crowStand, crowPeck, crowFly;

    [Header("Walk to the shed (no skill)")]
    [SerializeField] private float shedRange = 1.3f;
    [SerializeField] private float shedSeconds = 1f;

    [Header("Effects")]
    [SerializeField] private Sprite[] digits;
    [Tooltip("Tally groups of 1..5 marks (index 0 = one mark).")]
    [SerializeField] private Sprite[] tallies;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite magnifier;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float directTime = 5f;
    [SerializeField] private float planTime = 7f;

    private Vector3[] _basketRest, _crowRest;
    private Vector3 _scarecrowRest;
    private float _clock, _near;
    private bool _atShed;
    private Transform _player;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (board != null ? board.transform.position : transform.position) + new Vector3(-0.5f, 2.3f, 0f);

    private void Awake()
    {
        int n = baskets != null ? baskets.Length : 0;
        _basketRest = new Vector3[n];
        for (int i = 0; i < n; i++) _basketRest[i] = baskets[i].transform.position;
        int c = crows != null ? crows.Length : 0;
        _crowRest = new Vector3[c];
        for (int i = 0; i < c; i++) _crowRest[i] = crows[i].transform.position;
        if (scarecrow != null) _scarecrowRest = scarecrow.position;
    }

    protected override void OnBegin()
    {
        foreach (GameObject o in _spawned) if (o != null) Destroy(o);
        _spawned.Clear();
        for (int i = 0; i < baskets.Length; i++) baskets[i].transform.position = _basketRest[i];
        SetBoard(0);
        for (int i = 0; i < crows.Length; i++)
        {
            crows[i].gameObject.SetActive(true);
            crows[i].transform.position = _crowRest[i];
            crows[i].sprite = crowStand;
        }
        _atShed = false; _near = 0f; _clock = 0f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Harvest Tally",
            "The harvest is in. Count it up and write the totals down.",
            new[] { "Record the harvest" });
    }

    // ── Idle life: the scarecrow sways, crows hop and scatter ────────────

    private void Update()
    {
        if (!Started || Done) return;
        _clock += Time.deltaTime;

        if (scarecrow != null)
            scarecrow.position = _scarecrowRest + new Vector3(((int)(_clock * 1.1f) % 2 == 0 ? 0f : 1f / 22f), 0f, 0f);

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        for (int i = 0; i < crows.Length; i++)
        {
            if (!crows[i].gameObject.activeSelf || crows[i].sprite == crowFly) continue;
            crows[i].sprite = ((int)((_clock + i * 1.3f) * 1.5f) % 3 == 0) ? crowPeck : crowStand;
            if (_player != null && Vector2.Distance(_player.position, crows[i].transform.position) < 1.4f)
                StartCoroutine(Scatter(i));
        }

        // walking to the shed and reading the farmer's notes still works with no skill
        if (_player == null || shed == null || Working) { StopShed(); return; }
        if (Vector2.Distance(_player.position, shed.position) <= shedRange)
        {
            if (!_atShed)
            {
                _atShed = true; _near = 0f;
                ShowBar(true);
                Toast("You read the farmer's notes...", shedSeconds + 0.5f);
                SunodGame.Core.vc_Sfx.Play("page_next", 0.5f);
            }
            _near += Time.deltaTime;
            SetBar(_near / shedSeconds);
            if (_near >= shedSeconds)
            {
                _atShed = false;
                FillBoard();
                Finish("The farmer's notes had the totals. Recorded!");
            }
        }
        else StopShed();
    }

    private void StopShed()
    {
        if (!_atShed) return;
        _atShed = false;
        ShowBar(false);
    }

    private IEnumerator Scatter(int i)
    {
        SpriteRenderer c = crows[i];
        c.sprite = crowFly;
        SunodGame.Core.vc_Sfx.Play("caw", 0.35f, 0.15f);
        Vector3 from = c.transform.position, to = from + new Vector3(-2.2f, 3.4f, 0f);
        float t = 0f;
        while (t < 1.3f)
        {
            t += Time.deltaTime;
            c.transform.position = Vector3.Lerp(from, to, t / 1.3f);
            c.flipX = ((int)(t * 12f) % 2) == 0;
            yield return null;
        }
        c.gameObject.SetActive(false);
        yield return new WaitForSeconds(5f);
        if (c == null || Done) yield break;
        c.flipX = false;
        c.transform.position = _crowRest[i];
        c.sprite = crowStand;
        c.gameObject.SetActive(true);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StopShed(); StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;
        StopShed();
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        Toast("This needs careful counting.", 2.2f);
    }

    // ── Inspect: count each basket, tally marks appear ───────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Counting each basket...", inspectTime);
        ShowBar(true);
        yield return Timeline(inspectTime, 0f,
            At(0f, () => SetBoard(1)),
            At(0.1f, () => CountBasket(0)),
            At(0.6f, () => CountBasket(1)),
            At(1.1f, () => CountBasket(2)),
            At(inspectTime - 0.4f, ShowTotal));
        Finish("You counted every basket. The tally is done!");
    }

    private void CountBasket(int i)
    {
        if (i >= baskets.Length) return;
        Vector3 p = baskets[i].transform.position;
        Fx(ringFrames, p + new Vector3(0f, 0.2f, 0f), 6f);
        Fx(new[] { magnifier }, p + new Vector3(0.2f, 0.9f, 0f), 2.2f, 0.1f, 22);
        Digit(counts[i], p + new Vector3(0f, 1.0f, 0f));
        Tally(i, counts[i]);
        SunodGame.Core.vc_Sfx.Play("tally", 0.6f, 0.1f);
    }

    private int Total() { int s = 0; for (int i = 0; i < baskets.Length && i < counts.Length; i++) s += counts[i]; return s; }

    private void ShowTotal()
    {
        if (board == null) return;
        int total = Total();
        Vector3 p = board.transform.position + new Vector3(0f, 2.0f, 0f);
        if (total >= 10) { Digit(total / 10, p + new Vector3(-0.2f, 0f, 0f)); Digit(total % 10, p + new Vector3(0.2f, 0f, 0f)); }
        else Digit(total, p);
        Fx(checkFrames, p + new Vector3(0.7f, 0f, 0f), 2.2f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
    }

    // ── Collect: gather the baskets, then count them ─────────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("Gathering the baskets together...", collectTime);
        ShowBar(true);
        for (int i = 0; i < baskets.Length; i++)
        {
            int k = i;
            StartCoroutine(Slide(baskets[k].transform, gatherSlots != null && k < gatherSlots.Length ? gatherSlots[k].position : _basketRest[k], 0.1f + k * 0.4f, 0.9f));
        }
        yield return Timeline(collectTime, 0f,
            At(1.9f, () => SetBoard(1)),
            At(2.1f, () => CountBasket(0)),
            At(2.6f, () => CountBasket(1)),
            At(3.1f, () => CountBasket(2)),
            At(collectTime - 0.4f, ShowTotal));
        Finish("All baskets collected and counted!");
    }

    // ── Direct: line up the baskets and write the totals ─────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("Lining the baskets up in a row...", directTime);
        ShowBar(true);
        for (int i = 0; i < baskets.Length; i++)
        {
            int k = i;
            StartCoroutine(Slide(baskets[k].transform, rowSlots != null && k < rowSlots.Length ? rowSlots[k].position : _basketRest[k], 0.3f + k * 0.5f, 0.9f));
        }
        yield return Timeline(directTime, 0f,
            At(2.5f, () => SetBoard(1)),
            At(2.8f, () => CountBasket(0)),
            At(3.3f, () => CountBasket(1)),
            At(3.8f, () => CountBasket(2)),
            At(directTime - 0.5f, ShowTotal));
        Finish("You lined up the baskets and wrote the totals!");
    }

    // ── Plan: rule the tally sheet first, then fill it ───────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Ruling a tally sheet first...", planTime);
        ShowBar(true);
        yield return Timeline(planTime, 0f,
            At(0.4f, () => { SunodGame.Core.vc_Sfx.Play("scribble", 0.5f); Fx(sparkFrames, board.transform.position + new Vector3(-0.5f, 1.5f, 0f), 12f, 0.1f); }),
            At(1.2f, () => { SetBoard(1); SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.4f); Fx(sparkFrames, board.transform.position + new Vector3(0f, 1.0f, 0f), 12f, 0.1f); }),
            At(2.4f, () => SunodGame.Core.vc_Sfx.Play("scribble", 0.4f)),
            At(3.4f, () => CountBasket(0)),
            At(4.4f, () => CountBasket(1)),
            At(5.4f, () => CountBasket(2)),
            At(planTime - 0.5f, ShowTotal));
        Finish("With the sheet ready, the tally was easy!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetBoard(int frame)
    {
        if (board != null && boardFrames != null && frame < boardFrames.Length) board.sprite = boardFrames[frame];
    }

    private void FillBoard()
    {
        SetBoard(1);
        for (int i = 0; i < baskets.Length; i++) Tally(i, counts[i]);
    }

    /// <summary>Draws the tally marks for a basket's row on the board (groups of five, then the rest).</summary>
    private void Tally(int row, int n)
    {
        if (board == null || tallies == null || tallies.Length < 5) return;
        float[] rowY = { 12.5f, 23.5f, 34.5f };
        float[] colX = { 19f, 29f };
        Vector3 origin = board.transform.position;
        var groups = new List<int>();
        while (n >= 5) { groups.Add(5); n -= 5; }
        if (n > 0) groups.Add(n);
        for (int g = 0; g < groups.Count && g < colX.Length && row < rowY.Length; g++)
        {
            var go = new GameObject("Tally");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(origin + new Vector3((colX[g] - 20f) / 22f, (52f - rowY[row]) / 22f, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tallies[groups[g] - 1]; sr.sortingLayerName = sortingLayer; sr.sortingOrder = board.sortingOrder + 1;
            _spawned.Add(go);
        }
    }

    private IEnumerator Slide(Transform tr, Vector3 to, float delay, float dur)
    {
        yield return new WaitForSeconds(delay);
        Vector3 from = tr.position;
        float t = 0f, scrape = 0f;
        SunodGame.Core.vc_Sfx.Play("scrape", 0.35f, 0.1f);
        while (t < dur && tr != null)
        {
            t += Time.deltaTime;
            scrape += Time.deltaTime;
            tr.position = Snap(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)));
            if (scrape > 0.3f) { scrape = 0f; Fx(dustFrames, tr.position + new Vector3(0f, -0.4f, 0f), 10f, 0.15f); }
            yield return null;
        }
        if (tr != null) tr.position = to;
        SunodGame.Core.vc_Sfx.Play("clack", 0.5f, 0.1f);
    }

    private struct Ev { public float at; public Action act; }
    private static Ev At(float t, Action a) => new Ev { at = t, act = a };

    private IEnumerator Timeline(float total, float startK, params Ev[] events)
    {
        float t = 0f; int next = 0;
        while (t < total)
        {
            t += Time.deltaTime;
            SetBar(startK + (1f - startK) * (t / total));
            while (next < events.Length && t >= events[next].at) { events[next].act?.Invoke(); next++; }
            yield return null;
        }
        while (next < events.Length) { events[next].act?.Invoke(); next++; }
    }

    private void Digit(int n, Vector3 pos)
    {
        if (digits != null && n >= 0 && n < digits.Length) Fx(new[] { digits[n] }, pos, 1.6f, 0.35f, 24);
    }
}
