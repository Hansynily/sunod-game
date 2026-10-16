using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C quest (C1). Get everyone's pay envelopes ready. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   inspect  C  best fit     check every stack; a stamp thumps on each                   ~2s
///   collect  C  workaround   gather the envelopes into the tray                          4s
///   direct   C  workaround   sort the envelopes by name into neat rows                   5s
///   plan     I  off-letter   make a pay schedule first, then fill the tray                8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (inspect) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_PaydayQuest : vc_FitQuestBase
{
    [Header("Desk")]
    [SerializeField] private Transform desk;
    [Tooltip("The three envelope stacks on the desk.")]
    [SerializeField] private Transform[] stacks;
    [Tooltip("The hidden 'envelopes ready' tray group; revealed when the work is done.")]
    [SerializeField] private GameObject trayGroup;
    [SerializeField] private Transform trayPoint;

    [Header("Art")]
    [SerializeField] private Sprite envelope;
    [SerializeField] private Sprite stampUp;
    [SerializeField] private Sprite stampDown;
    [SerializeField] private Sprite stampOk;
    [SerializeField] private Sprite nameTag;
    [SerializeField] private Sprite scheduleSheet;
    [SerializeField] private Sprite rowCheck;

    [Header("Effects")]
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] dustFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float directTime = 5f;
    [SerializeField] private float planTime = 8f;

    private const int PerStack = 4;
    private float _elapsed, _total = 1f;
    private float _idleClock;
    private int _gen;                      // bumped on cancel so in-flight envelopes drop out
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (desk != null ? desk.position : transform.position) + new Vector3(-0.5f, 1.8f, 0f);

    protected override void OnBegin()
    {
        CleanSpawned();
        if (trayGroup != null) trayGroup.SetActive(false);
        if (stacks != null) foreach (Transform s in stacks) if (s != null) s.gameObject.SetActive(true);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Payday Envelopes",
            "It's payday. Get everyone's pay envelopes ready.",
            new[] { "Prepare the envelopes" });
    }

    // ── Idle life: coins glint over the stacks, a top sheet flutters ─────

    private void Update()
    {
        if (!Started || Done || stacks == null || stacks.Length == 0) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.2f, 2.4f);
        Transform s = stacks[Random.Range(0, stacks.Length)];
        if (s == null || !s.gameObject.activeSelf) return;
        Fx(coinFrames, s.position + new Vector3(0.1f, 0.5f, 0f), 6f, 0.35f, 12);
        StartCoroutine(Flutter(s));
    }

    private IEnumerator Flutter(Transform s)
    {
        Vector3 rest = s.localPosition;
        s.localPosition = rest + new Vector3(0f, 1f / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (s != null) s.localPosition = rest;
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("collect")) { StartWork(CollectRoutine(collectTime, "All envelopes collected and ready!")); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }

        Toast("Payroll needs careful, orderly work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        _gen++;
        CleanSpawned();
        if (stacks != null) foreach (Transform s in stacks) if (s != null) s.gameObject.SetActive(true);
    }

    private void CleanSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    private void Timeline(float total)
    {
        _elapsed = 0f; _total = Mathf.Max(0.1f, total);
        ShowBar(true);
    }

    private void Tick(float dt)
    {
        _elapsed += dt;
        SetBar(_elapsed / _total);
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; Tick(Time.deltaTime); yield return null; }
    }

    // ── Inspect: a stamp thumps on every stack ───────────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Checking every stack...", inspectTime);
        Timeline(inspectTime);
        int n = stacks.Length;
        float per = (inspectTime - 0.3f) / n;

        GameObject stamp = Spawn("Stamp", stampUp, 28);
        for (int i = 0; i < n; i++)
        {
            Transform s = stacks[i];
            Vector3 over = s.position + new Vector3(0.05f, 1.1f, 0f), hit = s.position + new Vector3(0.05f, 0.55f, 0f);
            stamp.transform.position = Snap(over);
            yield return Wait(per * 0.45f);
            stamp.GetComponent<SpriteRenderer>().sprite = stampDown;
            stamp.transform.position = Snap(hit);
            SunodGame.Core.vc_Sfx.Play("stamp", 0.8f, 0.05f);
            StartCoroutine(Flutter(s));
            Fx(dustFrames, hit + new Vector3(0f, -0.1f, 0f), 10f, 0.15f, 29);
            GameObject mark = Spawn("StampOk", stampOk, 13);
            mark.transform.position = Snap(s.position + new Vector3(0.02f, 0.1f, 0f));
            yield return Wait(per * 0.3f);
            stamp.GetComponent<SpriteRenderer>().sprite = stampUp;
            stamp.transform.position = Snap(over);
            yield return Wait(per * 0.25f);
        }
        Destroy(stamp);
        yield return Wait(0.3f);

        RevealTray();
        Finish("Every stack checked and stamped!");
    }

    // ── Collect: envelopes fly into the tray ─────────────────────────────

    private IEnumerator CollectRoutine(float total, string msg)
    {
        Toast("Gathering the envelopes into the tray...", total);
        Timeline(total);
        yield return Gather(total, true);
        RevealTray();
        Finish(msg);
    }

    /// <summary>Flies PerStack envelopes from each stack to the tray, hiding a stack when its last one leaves.</summary>
    private IEnumerator Gather(float total, bool bar)
    {
        Vector3 to = trayPoint != null ? trayPoint.position : (desk != null ? desk.position : transform.position);
        int n = stacks.Length, count = n * PerStack;
        float flight = 0.45f, spacing = Mathf.Max(0.05f, (total - flight) / count);
        int shown = 0;
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k < PerStack; k++)
            {
                StartCoroutine(Fly(stacks[i].position + new Vector3(0f, 0.2f, 0f), to, flight, k == PerStack - 1 ? stacks[i] : null));
                SunodGame.Core.vc_Sfx.Play("shuffle", 0.25f, 0.2f, 0.1f);
                shown++;
                float s = 0f;
                while (s < spacing) { s += Time.deltaTime; if (bar) Tick(Time.deltaTime); yield return null; }
            }
        }
        float rest = flight;
        while (rest > 0f) { rest -= Time.deltaTime; if (bar) Tick(Time.deltaTime); yield return null; }
    }

    private IEnumerator Fly(Vector3 from, Vector3 to, float seconds, Transform hideAfter)
    {
        int gen = _gen;
        GameObject e = Spawn("Envelope", envelope, 14);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            if (e != null) e.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.8f, 0f));
            yield return null;
        }
        if (e != null) { _spawned.Remove(e); Destroy(e); }
        if (gen != _gen) yield break;
        if (hideAfter != null) hideAfter.gameObject.SetActive(false);
        SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.15f, 0.08f);
        Fx(sparkFrames, to + new Vector3(0f, 0.2f, 0f), 12f, 0.1f, 29);
        if (trayGroup != null && !trayGroup.activeSelf) RevealTray();
    }

    // ── Direct: sort by name into neat rows ──────────────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("Sorting the envelopes by name...", directTime);
        Timeline(directTime);
        Vector3 d = desk.position;
        int n = stacks.Length * 3;
        var env = new List<Transform>();
        var rest = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            Transform s = stacks[i % stacks.Length];
            GameObject e = Spawn("Envelope", envelope, 14);
            e.transform.position = Snap(s.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.4f), 0f));
            env.Add(e.transform);
        }
        foreach (Transform s in stacks) s.gameObject.SetActive(false);
        SunodGame.Core.vc_Sfx.Play("shuffle", 0.5f);
        yield return Wait(0.3f);

        float sortTime = directTime - 1.0f, per = sortTime / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 from = env[i].position;
            Vector3 to = Snap(d + new Vector3(-0.95f + (i % 5) * 0.48f, 0.55f + (i / 5) * -0.3f + 0.25f, 0f));
            float s = 0f;
            while (s < per)
            {
                s += Time.deltaTime; Tick(Time.deltaTime);
                float k = Mathf.Clamp01(s / (per * 0.7f));
                if (env[i] != null) env[i].position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.3f, 0f));
                yield return null;
            }
            if (env[i] != null) env[i].position = to;
            GameObject tag = Spawn("NameTag", nameTag, 15);
            tag.transform.position = Snap(to + new Vector3(0f, -0.2f, 0f));
            SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.12f);
        }
        Fx(checkFrames, d + new Vector3(0f, 1.4f, 0f), 3f, 0.3f, 29);
        yield return Wait(0.7f);
        foreach (Transform e in env) if (e != null) Destroy(e.gameObject);
        RevealTray();
        Finish("You sorted every envelope by name!");
    }

    // ── Plan: a pay schedule first, then fill the tray ───────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Making a pay schedule first...", planTime);
        Timeline(planTime);
        Vector3 at = (desk != null ? desk.position : transform.position) + new Vector3(-2.0f, 0.9f, 0f);
        GameObject sheet = Spawn("Schedule", scheduleSheet, 12);
        sheet.transform.position = Snap(at);
        StartCoroutine(PopScale(sheet.transform));
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);

        int rows = 6;
        float planPhase = planTime * 0.65f, per = planPhase / rows;
        for (int i = 0; i < rows; i++)
        {
            yield return Wait(per);
            GameObject c = Spawn("RowCheck", rowCheck, 13);
            c.transform.position = Snap(at + new Vector3(0.5f, 0.3f - i * 0.18f - 0.0f, 0f));
            c.transform.position = Snap(at + new Vector3(-0.35f + 0.9f, 0.45f - i * 0.18f, 0f));
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
        }

        Toast("Now filling the tray...", planTime - planPhase);
        yield return Gather(planTime - planPhase, true);
        RevealTray();
        Finish("Your pay schedule got every envelope ready!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void RevealTray()
    {
        if (stacks != null) foreach (Transform s in stacks) if (s != null) s.gameObject.SetActive(false);
        if (trayGroup != null && !trayGroup.activeSelf)
        {
            trayGroup.SetActive(true);
            StartCoroutine(PopScale(trayGroup.transform));
            Fx(sparkFrames, (trayPoint != null ? trayPoint.position : trayGroup.transform.position) + new Vector3(0.1f, 0.3f, 0f), 12f, 0.2f, 29);
            Fx(coinFrames, (trayPoint != null ? trayPoint.position : trayGroup.transform.position) + new Vector3(-0.2f, 0.5f, 0f), 6f, 0.4f, 29);
        }
    }

    private GameObject Spawn(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return go;
    }

    private IEnumerator PopScale(Transform tr)
    {
        Vector3 s0 = tr.localScale;
        foreach (float k in new[] { 0.5f, 0.8f, 1.15f, 1f })
        {
            if (tr == null) yield break;
            tr.localScale = s0 * k;
            yield return new WaitForSeconds(0.06f);
        }
        if (tr != null) tr.localScale = s0;
    }
}
