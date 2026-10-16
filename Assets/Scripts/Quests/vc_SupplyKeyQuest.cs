using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C quest (C2). The spare key is lost somewhere in the supply room; taking stock finds it.
/// Any APPROPRIATE skill works - how well it fits decides how fast it goes (and so the stars);
/// skills that make no sense do nothing.
///
///   inspect  C  best fit     check every shelf label (check marks); the key box glints       ~2s
///   collect  C  workaround   gather the loose supplies into the bin; the key drops out         4s
///   direct   C  workaround   label and sort the shelves; the key turns up                      5s
///   survey   I  off-letter   sweep a scanner beam over the room until something shines         7s
///   summon   E  off-letter   the locksmith walks over and opens the door                      10s
///   anything else: a hint, no effect
///
/// Every key path ends the same way: the key flies to the padlock on the storeroom door.
/// While a slow fix is running the best fit (inspect) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_SupplyKeyQuest : vc_FitQuestBase
{
    [Header("Room")]
    [SerializeField] private Transform[] shelves;
    [SerializeField] private SpriteRenderer keyBox;
    [SerializeField] private Sprite boxClosed;
    [SerializeField] private Sprite boxOpen;
    [SerializeField] private SpriteRenderer key;
    [SerializeField] private Transform door;
    [SerializeField] private SpriteRenderer padlock;
    [SerializeField] private Transform bin;
    [SerializeField] private SpriteRenderer[] looseItems;
    [SerializeField] private SpriteRenderer scanBeam;
    [Tooltip("Things the scanner beam sparkles on while passing: shelves, boxes, bin.")]
    [SerializeField] private Transform[] scanTargets;

    [Header("Effects")]
    [SerializeField] private Sprite[] labelSprites;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] ringFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float directTime = 5f;
    [SerializeField] private float surveyTime = 7f;
    [SerializeField] private float locksmithWalkTime = 6f;
    [SerializeField] private float locksmithFixTime = 4f;
    [SerializeField] private float keyFlightTime = 0.6f;

    private Vector3 _padlockRest, _binRest;
    private Vector3[] _itemRest;
    private Vector3[] _shelfRest;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private float _dustClock;

    protected override Vector3 BarWorldPosition => new Vector3(-0.5f, 2.9f, 0f);

    private void Awake()
    {
        if (padlock != null) _padlockRest = padlock.transform.localPosition;
        if (bin != null) _binRest = bin.localPosition;
        if (looseItems != null)
        {
            _itemRest = new Vector3[looseItems.Length];
            for (int i = 0; i < looseItems.Length; i++) if (looseItems[i] != null) _itemRest[i] = looseItems[i].transform.localPosition;
        }
        if (shelves != null)
        {
            _shelfRest = new Vector3[shelves.Length];
            for (int i = 0; i < shelves.Length; i++) if (shelves[i] != null) _shelfRest[i] = shelves[i].localPosition;
        }
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        if (key != null) key.gameObject.SetActive(false);
        if (scanBeam != null) scanBeam.gameObject.SetActive(false);
        if (keyBox != null) keyBox.sprite = boxClosed;
        if (padlock != null) { padlock.gameObject.SetActive(true); padlock.transform.localPosition = _padlockRest; }
        if (bin != null) bin.localPosition = _binRest;
        if (looseItems != null)
            for (int i = 0; i < looseItems.Length; i++)
                if (looseItems[i] != null) { looseItems[i].gameObject.SetActive(true); looseItems[i].transform.localPosition = _itemRest[i]; looseItems[i].transform.localScale = Vector3.one; }
        if (shelves != null)
            for (int i = 0; i < shelves.Length; i++) if (shelves[i] != null) shelves[i].localPosition = _shelfRest[i];

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Missing Key",
            "The spare key is lost somewhere in the supply room. Take stock and find it.",
            new[] { "Find the key and open the storeroom" });
    }

    protected override void OnWorkCancelled()
    {
        if (scanBeam != null) scanBeam.gameObject.SetActive(false);
        if (key != null) key.gameObject.SetActive(false);
    }

    // ── Idle life: dust drifting off the shelves ─────────────────────────

    private void Update()
    {
        if (!Started || Done || shelves == null || shelves.Length == 0 || dustFrames == null || dustFrames.Length == 0) return;
        _dustClock -= Time.deltaTime;
        if (_dustClock > 0f) return;
        _dustClock = Random.Range(1.0f, 2.2f);
        Transform s = shelves[Random.Range(0, shelves.Length)];
        if (s == null) return;
        Fx(new[] { dustFrames[dustFrames.Length - 1] }, s.position + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.4f, 1.4f), 0f), 1.6f, -0.35f, 6);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(LocksmithRoutine()); return; }

        Toast("Try a careful, orderly approach.", 2.2f);
    }

    // ── Inspect: check every label, the key box glints ───────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Checking every label against the list...", inspectTime);
        ShowBar(true);
        float t = 0f;
        int total = (shelves != null ? shelves.Length : 0) * 3;
        float per = 1.2f / Mathf.Max(1, total);
        for (int s = 0; s < (shelves != null ? shelves.Length : 0); s++)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = shelves[s].position + new Vector3(-0.65f + 0.65f * k, 1.55f, 0f);
                Fx(checkFrames, p, 2.5f, 0.2f);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.12f);
                yield return BarStep(per, t / inspectTime, (t + per) / inspectTime);
                t += per;
            }
        }
        yield return GlintAndOpenBox(0.3f);
        t += 0.3f;
        SetBar(t / inspectTime);
        yield return KeyFromBox();
        yield return KeyToDoor(keyBox.transform.position + new Vector3(0f, 0.6f, 0f));
        Finish("You checked every label and found the key!");
    }

    // ── Collect: supplies into the bin, the key drops out ────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("Gathering the supplies into the bin...", collectTime);
        ShowBar(true);
        float t = 0f;
        int n = looseItems != null ? looseItems.Length : 0;
        float gap = 0.55f, fly = 0.5f;
        for (int i = 0; i < n; i++)
        {
            if (looseItems[i] != null) StartCoroutine(FlyItem(looseItems[i], bin.position + new Vector3(0f, 0.45f, 0f), fly, 0.9f));
            yield return BarStep(gap, t / collectTime, (t + gap) / collectTime);
            t += gap;
        }
        yield return BarStep(fly, t / collectTime, (t + fly) / collectTime);
        t += fly;

        // the bin tips and the key drops out
        SunodGame.Core.vc_Sfx.Play("scrape", 0.4f);
        StartCoroutine(Shake(bin, _binRest, 0.4f, 2));
        yield return BarStep(0.4f, t / collectTime, (t + 0.4f) / collectTime);
        t += 0.4f;
        Vector3 from = bin.position + new Vector3(0f, 0.5f, 0f);
        RevealKey(from);
        yield return Fly(key.transform, from, bin.position + new Vector3(-0.8f, -0.1f, 0f), 0.3f, 0.35f);
        yield return KeyToDoor(key.transform.position);
        Finish("The key dropped out of the supplies!");
    }

    private IEnumerator FlyItem(SpriteRenderer sr, Vector3 to, float seconds, float height)
    {
        yield return Fly(sr.transform, sr.transform.position, to, seconds, height);
        SunodGame.Core.vc_Sfx.Play("clack", 0.35f, 0.12f);
        if (sr != null) sr.gameObject.SetActive(false);
        Fx(dustFrames, to, 12f, 0.1f);
    }

    // ── Direct: label and sort the shelves ───────────────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("Labelling and sorting the shelves...", directTime);
        ShowBar(true);
        float t = 0f;
        int ns = shelves != null ? shelves.Length : 0;
        float per = 3.2f / Mathf.Max(1, ns * 3);
        for (int s = 0; s < ns; s++)
        {
            StartCoroutine(Shake(shelves[s], _shelfRest[s], 0.3f, 1));
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = shelves[s].position + new Vector3(-0.65f + 0.65f * k, 0.28f + 0.55f * (k % 3), 0f);
                SpawnLabel(p, (s + k) % 3);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.45f, 0.12f);
                yield return BarStep(per, t / directTime, (t + per) / directTime);
                t += per;
            }
        }
        // the key slips out from between the boxes
        Transform mid = shelves[ns / 2];
        Vector3 from = mid.position + new Vector3(0.2f, 1.0f, 0f);
        SunodGame.Core.vc_Sfx.Play("scrape", 0.35f);
        RevealKey(from);
        float drop = 0.5f;
        yield return Fly(key.transform, from, mid.position + new Vector3(0.4f, -0.35f, 0f), drop, 0.2f);
        t += drop;
        SetBar(t / directTime);
        yield return KeyToDoor(key.transform.position);
        Finish("Everything is labelled and sorted. The key turned up!");
    }

    private void SpawnLabel(Vector3 pos, int idx)
    {
        if (labelSprites == null || labelSprites.Length == 0) return;
        var go = new GameObject("Label");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(pos);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = labelSprites[idx % labelSprites.Length];
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 8;
        _spawned.Add(go);
        Fx(sparkFrames, pos, 12f, 0.1f);
    }

    // ── Survey: a scanner beam sweeps the room ───────────────────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Sweeping the room with a scanner...", surveyTime);
        ShowBar(true);
        if (scanBeam != null)
        {
            scanBeam.gameObject.SetActive(true);
            scanBeam.transform.localScale = new Vector3(1f, 2.2f, 1f);
        }
        float t = 0f, tick = 0f;
        float x0 = -4.6f, x1 = 4.6f, cy = 1.4f;
        var lit = new HashSet<Transform>();
        float keyX = keyBox.transform.position.x;
        bool glinted = false;
        float sweepEnd = surveyTime - 1.4f;
        while (t < sweepEnd)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            float phase = t / (sweepEnd / 2f);
            float x = phase < 1f ? Mathf.Lerp(x0, x1, phase) : Mathf.Lerp(x1, x0, Mathf.Min(1f, phase - 1f));
            if (scanBeam != null) scanBeam.transform.position = Snap(new Vector3(x, cy, 0f));
            SetBar(t / surveyTime);
            if (tick >= 0.35f) { tick = 0f; SunodGame.Core.vc_Sfx.Play("timer_tick", 0.3f, 0.1f); }
            if (scanTargets != null && phase < 1f)
                foreach (Transform tg in scanTargets)
                    if (tg != null && !lit.Contains(tg) && x >= tg.position.x)
                    {
                        lit.Add(tg);
                        Fx(sparkFrames, tg.position + new Vector3(0f, 0.7f, 0f), 12f, 0.2f);
                    }
            if (!glinted && phase >= 1f && x <= keyX)
            {
                glinted = true;
                for (int i = 0; i < 3; i++) Fx(sparkFrames, keyBox.transform.position + new Vector3(-0.3f + 0.3f * i, 0.5f + 0.15f * (i % 2), 0f), 10f, 0.25f);
                SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
            }
            yield return null;
        }
        if (scanBeam != null) scanBeam.gameObject.SetActive(false);
        yield return GlintAndOpenBox(0.2f);
        t += 0.2f;
        yield return KeyFromBox();
        yield return KeyToDoor(keyBox.transform.position + new Vector3(0f, 0.6f, 0f));
        Finish("Your scan found a shiny key!");
    }

    // ── Call: the locksmith opens the door ───────────────────────────────

    private IEnumerator LocksmithRoutine()
    {
        Toast("You call the locksmith...", 3f);
        ShowBar(true);
        Vector3 to = door.position + new Vector3(-1.3f, -0.9f, 0f);
        Vector3 from = to + new Vector3(-8f, 0f, 0f);
        SpawnHelper(from, 6);
        float total = locksmithWalkTime + locksmithFixTime;
        yield return HelperWalk(from, to, locksmithWalkTime, t => SetBar(t / total));

        Toast("The locksmith works on the lock...", locksmithFixTime);
        float tt = 0f, tick = 0f;
        while (tt < locksmithFixTime)
        {
            tt += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar((locksmithWalkTime + tt) / total);
            if (tick >= 0.5f)
            {
                tick = 0f;
                SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.1f);
                if (padlock != null) StartCoroutine(Shake(padlock.transform, _padlockRest, 0.15f, 1));
            }
            yield return null;
        }
        PopPadlock();
        Finish("The locksmith opened the door. Thanks for calling!");
    }

    // ── Key and padlock ──────────────────────────────────────────────────

    private IEnumerator GlintAndOpenBox(float seconds)
    {
        Vector3 b = keyBox.transform.position;
        Fx(sparkFrames, b + new Vector3(0.2f, 0.7f, 0f), 12f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        yield return new WaitForSeconds(seconds);
    }

    private IEnumerator KeyFromBox()
    {
        keyBox.sprite = boxOpen;
        SunodGame.Core.vc_Sfx.Play("clack", 0.5f);
        StartCoroutine(Shake(keyBox.transform, keyBox.transform.localPosition, 0.2f, 1));
        Vector3 from = keyBox.transform.position + new Vector3(0f, 0.7f, 0f);
        RevealKey(from);
        yield return Fly(key.transform, from, from + new Vector3(0f, 0.6f, 0f), 0.25f, 0.1f);
    }

    private void RevealKey(Vector3 at)
    {
        key.gameObject.SetActive(true);
        key.transform.position = Snap(at);
        SunodGame.Core.vc_Sfx.Play("key_jingle", 0.7f);
        Fx(sparkFrames, at + new Vector3(0f, 0.2f, 0f), 12f, 0.2f);
    }

    /// <summary>The key arcs over to the padlock, which pops off.</summary>
    private IEnumerator KeyToDoor(Vector3 from)
    {
        Vector3 to = padlock != null ? padlock.transform.position : door.position;
        yield return Fly(key.transform, from, to, keyFlightTime, 0.8f);
        key.gameObject.SetActive(false);
        PopPadlock();
        yield return new WaitForSeconds(0.1f);
    }

    private void PopPadlock()
    {
        if (padlock == null) return;
        SunodGame.Core.vc_Sfx.Play("door_open", 0.8f);
        Fx(sparkFrames, padlock.transform.position, 12f, 0.2f);
        StartCoroutine(PadlockFall());
    }

    private IEnumerator PadlockFall()
    {
        Transform tr = padlock.transform;
        Vector3 from = tr.position;
        float t = 0f, v = 2.2f;
        while (t < 0.45f && tr != null)
        {
            t += Time.deltaTime;
            v -= 9f * Time.deltaTime;
            tr.position = Snap(tr.position + new Vector3(0.9f * Time.deltaTime, v * Time.deltaTime, 0f));
            yield return null;
        }
        if (padlock != null) padlock.gameObject.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator Fly(Transform tr, Vector3 from, Vector3 to, float seconds, float height)
    {
        float t = 0f;
        while (t < seconds && tr != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            tr.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * height, 0f));
            yield return null;
        }
        if (tr != null) tr.position = Snap(to);
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
