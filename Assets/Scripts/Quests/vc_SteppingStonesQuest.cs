using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R quest (R2). Get across the river. Any APPROPRIATE skill makes a way across - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   build    R  best fit     lay a clean stone path, slab by slab, splash each one      ~2s
///   push     R  workaround   shove a boulder into the river per press (splash)          x3
///   repair   R  workaround   mend the broken old plank bridge beside the crossing       4s
///   plan     I  off-letter   work out a shallow ford (footprints appear), then it shows  7s
///   summon   E  off-letter   Mang Ben's helper walks over and lays new planks           10s
///   walking round to the far bank still works with no skill at all (existing path)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (build) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_SteppingStonesQuest : vc_FitQuestBase
{
    [Header("Bridge (repair / call paths)")]
    [SerializeField] private SpriteRenderer bridge;
    [SerializeField] private Sprite bridgeBroken;
    [SerializeField] private Sprite bridgeFixed;

    [Header("Stone path (build / plan paths)")]
    [Tooltip("Slabs across the river, left to right. Inactive until laid.")]
    [SerializeField] private Transform[] slabs;

    [Header("Boulders (push path)")]
    [SerializeField] private Transform[] boulders;
    [Tooltip("Where each boulder ends up in the river (same count as boulders).")]
    [SerializeField] private Transform[] boulderSlots;

    [Header("Walk-around (no skill)")]
    [SerializeField] private Transform farBank;
    [SerializeField] private float farBankRange = 1.5f;

    [Header("Effects")]
    [SerializeField] private Sprite[] splashFrames;
    [SerializeField] private Sprite[] rippleFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite footprint;

    [Header("Timing (seconds)")]
    [SerializeField] private float buildTime = 2f;
    [SerializeField] private float repairTime = 4f;
    [SerializeField] private float planTime = 7f;
    [SerializeField] private float helperWalkTime = 6f;
    [SerializeField] private float helperFixTime = 4f;

    private int _pushes;
    private bool _finishing;
    private Vector3 _bridgeRest;
    private readonly Vector3[] _boulderRest = new Vector3[8];
    private readonly List<GameObject> _prints = new List<GameObject>();
    private float _rippleClock;
    private Transform _player;

    protected override Vector3 BarWorldPosition => new Vector3(transform.position.x + 1.1f, transform.position.y + 1.25f, 0f);

    private void Awake()
    {
        if (bridge != null) _bridgeRest = bridge.transform.localPosition;
        if (boulders != null)
            for (int i = 0; i < boulders.Length && i < _boulderRest.Length; i++)
                if (boulders[i] != null) _boulderRest[i] = boulders[i].localPosition;
    }

    protected override void OnBegin()
    {
        _pushes = 0;
        _finishing = false;
        _rippleClock = 0f;
        ClearPrints();

        if (bridge != null)
        {
            bridge.sprite = bridgeBroken;
            bridge.transform.localPosition = _bridgeRest;
        }
        if (slabs != null)
            foreach (Transform s in slabs)
                if (s != null) { s.gameObject.SetActive(false); ResetSlab(s); }
        if (boulders != null)
            for (int i = 0; i < boulders.Length && i < _boulderRest.Length; i++)
            {
                if (boulders[i] == null) continue;
                boulders[i].localPosition = _boulderRest[i];
                boulders[i].gameObject.SetActive(true);
                var col = boulders[i].GetComponent<Collider2D>();
                if (col != null) col.enabled = true;
            }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Stepping Stones",
            "The river is too deep to wade. Make a way across.",
            new[] { "Get across the river" });
    }

    // ── Idle life + walk-around ──────────────────────────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        // wading round the long way still solves it, with no skill at all
        if (_player != null && farBank != null &&
            Vector2.Distance(_player.position, farBank.position) <= farBankRange)
        {
            CancelWork();
            Finish("You waded the long way around.");
            return;
        }

        // ripples drift across the crossing while nothing has been built
        _rippleClock += Time.deltaTime;
        if (_rippleClock >= 1.1f)
        {
            _rippleClock = Random.Range(0f, 0.5f);
            Vector3 at = transform.position + new Vector3(Random.Range(1.5f, 3.4f), Random.Range(-1.0f, 0.5f), 0f);
            Fx(rippleFrames, at, 4f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (_finishing && !d.HasTag("build")) return;

        // best fit always wins, even over a slow fix already running
        if (d.HasTag("build"))
        {
            _finishing = true;
            StartWork(BuildRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("push")) { PushBoulder(); return; }
        if (d.HasTag("repair")) { StartWork(BridgeRepairRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(HelperRoutine()); return; }

        Toast("You need to make a way across.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (bridge != null) bridge.transform.localPosition = _bridgeRest;
        ClearPrints();
    }

    // ── Build: slabs laid left to right ──────────────────────────────────

    private IEnumerator BuildRoutine()
    {
        Toast("Laying a stone path across...", 2f);
        ShowBar(true);
        int n = slabs != null ? slabs.Length : 0;
        float per = buildTime / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (slabs[i] != null) yield return PlaceSlab(slabs[i], Color.white, 0.12f);
            SetBar((i + 1f) / n);
            yield return new WaitForSeconds(Mathf.Max(0f, per - 0.12f));
        }
        Finish("You laid a clean stone path across!");
    }

    private IEnumerator PlaceSlab(Transform s, Color tint, float dropTime)
    {
        var sr = s.GetComponent<SpriteRenderer>();
        Vector3 rest = s.localPosition;
        ResetSlab(s);
        if (sr != null) sr.color = tint;
        s.gameObject.SetActive(true);
        s.localPosition = rest + new Vector3(0f, 6f / 22f, 0f);
        yield return new WaitForSeconds(dropTime * 0.5f);
        s.localPosition = rest + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(dropTime * 0.5f);
        s.localPosition = rest;
        SunodGame.Core.vc_Sfx.Play("splash", 0.5f, 0.12f);
        Fx(splashFrames, s.position + new Vector3(0f, 0.05f, 0f), 10f, 0.15f);
    }

    private static void ResetSlab(Transform s)
    {
        var sr = s.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
    }

    // ── Push: one boulder rolled into the river per press ────────────────

    private void PushBoulder()
    {
        if (boulders == null || _pushes >= boulders.Length) return;
        int i = _pushes++;
        bool last = _pushes >= boulders.Length;
        if (last) _finishing = true;

        Toast(last ? "The last boulder splashes in!" : $"You shove a boulder into the river ({_pushes}/{boulders.Length})", 2f);
        StartCoroutine(RollBoulder(i, last));
    }

    private IEnumerator RollBoulder(int i, bool last)
    {
        Transform b = boulders[i];
        Transform slot = boulderSlots != null && i < boulderSlots.Length ? boulderSlots[i] : null;
        if (b == null || slot == null) { if (last) Finish("The boulders make a path across!"); yield break; }

        var col = b.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 from = b.position, to = slot.position;
        Fx(dustFrames, from + new Vector3(0.3f, 0.05f, 0f), 10f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("scrape", 0.45f, 0.1f);
        float dur = 0.7f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 0.7f;
            b.position = Snap(p);
            yield return null;
        }
        b.position = to;
        SunodGame.Core.vc_Sfx.Play("splash", 0.7f, 0.1f);
        Fx(splashFrames, to + new Vector3(0f, 0.1f, 0f), 10f, 0.25f);
        Fx(rippleFrames, to + new Vector3(0f, -0.2f, 0f), 5f);
        StartCoroutine(Shake(b, b.localPosition, 0.2f, 1));

        if (last)
        {
            yield return new WaitForSeconds(0.35f);
            Finish("The boulders make a path across the river!");
        }
    }

    // ── Repair: mend the old plank bridge ────────────────────────────────

    private IEnumerator BridgeRepairRoutine()
    {
        Toast("Mending the old plank bridge...", repairTime);
        ShowBar(true);
        yield return HammerBridge(repairTime, 0f, 1f);
        MendBridge();
        Finish("You mended the old plank bridge!");
    }

    /// <summary>Hammer swings along the bridge for 'seconds' while the bar runs k0..k1.</summary>
    private IEnumerator HammerBridge(float seconds, float k0, float k1)
    {
        float t = 0f, tick = 0f;
        int hit = 0;
        Vector3 c = bridge != null ? bridge.transform.position : transform.position + new Vector3(2.5f, 0.15f, 0f);
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            if (tick >= 0.5f)
            {
                tick = 0f;
                float x = c.x + (hit++ % 3 - 1) * 0.55f;
                Fx(hammerFrames, new Vector3(x + 0.1f, c.y + 0.5f, 0f), 8f);
                SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.08f);
                Fx(sparkFrames, new Vector3(x, c.y + 0.1f, 0f), 12f, 0.1f);
                if (bridge != null) StartCoroutine(Shake(bridge.transform, _bridgeRest, 0.12f, 1));
            }
            yield return null;
        }
    }

    private void MendBridge()
    {
        if (bridge == null) return;
        bridge.sprite = bridgeFixed;
        bridge.transform.localPosition = _bridgeRest;
        Fx(dustFrames, bridge.transform.position + new Vector3(0f, 0.05f, 0f), 10f, 0.2f);
        Fx(sparkFrames, bridge.transform.position + new Vector3(0f, 0.1f, 0f), 12f, 0.15f);
    }

    // ── Plan: footprints reveal a shallow ford ───────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Working out a safe way across...", planTime);
        ShowBar(true);

        int n = slabs != null ? slabs.Length : 0;
        float walkPart = planTime * 0.8f;
        int steps = Mathf.Max(1, n + 2);
        float per = walkPart / steps;
        float x0 = transform.position.x + 0.9f, x1 = transform.position.x + 3.9f;
        float y = transform.position.y - 0.85f;
        float elapsed = 0f;

        for (int i = 0; i < steps; i++)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)(steps - 1));
            SpawnPrint(new Vector3(x, y + (i % 2 == 0 ? 0.1f : -0.1f), 0f), i % 2 == 1);
            SunodGame.Core.vc_Sfx.Play("footstep_" + (i % 4), 0.35f, 0.1f);
            yield return BarStep(per, elapsed / planTime, (elapsed + per) / planTime);
            elapsed += per;
        }

        // the ford shows itself: half-sunk stones
        Toast("Found a shallow ford!", 1.5f);
        float rest = planTime - elapsed;
        float slabPer = rest / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (slabs[i] != null) StartCoroutine(PlaceSlab(slabs[i], new Color(0.72f, 0.84f, 0.95f, 1f), 0.12f));
            yield return BarStep(slabPer, (elapsed) / planTime, (elapsed + slabPer) / planTime);
            elapsed += slabPer;
        }
        Finish("You found a shallow ford and crossed safely!");
    }

    private void SpawnPrint(Vector3 pos, bool flip)
    {
        if (footprint == null) return;
        var go = new GameObject("Footprint");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(pos);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = footprint;
        sr.flipX = flip;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 8;
        _prints.Add(go);
        Fx(rippleFrames, pos + new Vector3(0f, -0.05f, 0f), 5f);
    }

    private void ClearPrints()
    {
        foreach (GameObject g in _prints) if (g != null) Destroy(g);
        _prints.Clear();
    }

    // ── Call: helper walks over and lays new planks ──────────────────────

    private IEnumerator HelperRoutine()
    {
        Toast("You call Mang Ben's helper...", 3f);
        ShowBar(true);

        Vector3 c = bridge != null ? bridge.transform.position : transform.position + new Vector3(2.5f, 0.15f, 0f);
        Vector3 to = new Vector3(c.x - 1.9f, c.y - 0.35f, 0f);
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        SpawnHelper(from, 9);
        float total = helperWalkTime + helperFixTime;

        yield return HelperWalk(from, to, helperWalkTime, t => SetBar(t / total));

        Toast("The helper lays the new planks...", helperFixTime);
        yield return HammerBridge(helperFixTime, helperWalkTime / total, 1f);
        MendBridge();
        Finish("Mang Ben's helper laid new planks. Thanks!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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
