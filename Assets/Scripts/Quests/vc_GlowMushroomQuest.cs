using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// I quest (I5). Find out why the mushrooms glow. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     scan the cluster; data notes pop up                           ~2s
///   unlock   I  workaround   crack the secret of the glow: the roots are revealed           4s
///   plan     I  workaround   set up a light-vs-dark jar experiment                          5s
///   collect  C  off-letter   take a sample from each mushroom, label every jar              8s
///   inspect  C  off-letter   look at each mushroom up close with a lens, note the details   7s
///   walking up to the deep grove still works with no skill (existing)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (survey) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_GlowMushroomQuest : vc_FitQuestBase
{
    [Header("Mushrooms")]
    [Tooltip("First 3 = the study cluster. Any more = the deep-grove beacon. Frame i*2 = dim, i*2+1 = bright (looks repeat per 3).")]
    [SerializeField] private Transform[] mushrooms;
    [SerializeField] private Sprite[] mushFrames;
    [SerializeField] private SpriteRenderer[] auras;
    [SerializeField] private Transform grove;
    [SerializeField] private float groveRange = 1.7f;

    [Header("Lab")]
    [SerializeField] private SpriteRenderer[] jars;
    [SerializeField] private SpriteRenderer jarCover;
    [SerializeField] private SpriteRenderer lightBeam;
    [SerializeField] private SpriteRenderer roots;
    [SerializeField] private SpriteRenderer notesProp;
    [SerializeField] private Sprite jarEmpty;
    [SerializeField] private Sprite jarDim;
    [SerializeField] private Sprite jarLit;
    [SerializeField] private Sprite jarDark;

    [Header("Effects")]
    [SerializeField] private Sprite scanBeam;
    [SerializeField] private Sprite probe;
    [Tooltip("Magnifying lens for the Inspect path.")]
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite[] sporeFrames;
    [SerializeField] private Sprite[] noteIcons;
    [SerializeField] private Sprite[] labels;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 2f;
    [SerializeField] private float solveTime = 4f;
    [SerializeField] private float planTime = 5f;
    [SerializeField] private float collectTime = 8f;
    [SerializeField] private float inspectTime = 7f;

    private float _pulseClock, _sporeClock;
    private int _pulseState;
    private bool _boost;
    private Vector3 _barPos;
    private Transform _player;
    private bool _arrived;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => _barPos;

    private Vector3 Cluster()
    {
        if (mushrooms == null || mushrooms.Length < 3) return transform.position;
        return (mushrooms[0].position + mushrooms[1].position + mushrooms[2].position) / 3f;
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _boost = false; _arrived = false; _pulseClock = 0f; _pulseState = 0;
        SetPulse(0);
        if (jars != null) foreach (SpriteRenderer j in jars) j.sprite = jarEmpty;
        if (jarCover != null) jarCover.gameObject.SetActive(false);
        if (lightBeam != null) lightBeam.gameObject.SetActive(false);
        if (roots != null) roots.gameObject.SetActive(false);
        if (notesProp != null) notesProp.gameObject.SetActive(false);
        _barPos = Cluster() + new Vector3(-0.5f, 2.1f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Glowing Mushrooms",
            "The mushrooms glow in the dark. Find out why.",
            new[] { "Study the mushroom clusters" });
    }

    // ── Idle life: pulsing glow, drifting spores, walk-up detection ──────

    private void Update()
    {
        if (!Started || Done) return;

        _pulseClock += Time.deltaTime;
        if (_pulseClock >= (_boost ? 0.25f : 0.55f))
        {
            _pulseClock = 0f;
            _pulseState = (_pulseState + 1) % 4;
            SetPulse(_pulseState);
        }

        _sporeClock += Time.deltaTime;
        if (_sporeClock >= (_boost ? 0.12f : 0.4f) && mushrooms != null && mushrooms.Length >= 3 && sporeFrames != null)
        {
            _sporeClock = 0f;
            Vector3 p = mushrooms[Random.Range(0, 3)].position + new Vector3(Random.Range(-0.4f, 0.4f), 0.3f, 0f);
            Fx(sporeFrames, p, 2.2f, Random.Range(0.6f, 1.0f), 12);
        }

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        if (!Working && !_arrived && _player != null && grove != null &&
            Vector2.Distance(_player.position, grove.position) <= groveRange)
        {
            _arrived = true;
            StartCoroutine(GroveReveal());
        }
    }

    private void SetPulse(int state)
    {
        if (mushrooms == null) return;
        for (int i = 0; i < mushrooms.Length; i++)
        {
            bool bright = _boost || ((state + i) % 4 >= 2);
            SpriteRenderer sr = mushrooms[i].GetComponent<SpriteRenderer>();
            int k = (i % 3) * 2 + (bright ? 1 : 0);
            if (sr != null && mushFrames != null && k < mushFrames.Length) sr.sprite = mushFrames[k];
            if (auras != null && i < auras.Length && auras[i] != null)
            {
                Color c = auras[i].color;
                c.a = _boost ? 1f : (bright ? 0.85f : 0.5f);
                auras[i].color = c;
            }
        }
    }

    private IEnumerator GroveReveal()
    {
        Toast("Up close, you finally see it...", 2.5f);
        _boost = true;
        Fx(bulbFrames, grove.position + new Vector3(0f, 1.6f, 0f), 2f, 0.3f, 25);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        Fx(sparkFrames, grove.position + new Vector3(0f, 0.4f, 0f), 12f, 0.3f);
        yield return new WaitForSeconds(0.7f);
        Finish("Up close, you finally see it: the roots feed on moonlight!");
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }

        if (BlockedByWork()) return;

        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }

        Toast("This needs a scientist's eye.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        _boost = false;
    }

    // ── Survey ★ ─────────────────────────────────────────────────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Scanning the glowing cluster...", surveyTime);
        ShowBar(true);
        Vector3 c = Cluster();
        var beam = new GameObject("ScanBeam");
        beam.transform.SetParent(transform, true);
        var bs = beam.AddComponent<SpriteRenderer>();
        bs.sprite = scanBeam; bs.sortingLayerName = sortingLayer; bs.sortingOrder = 22;
        _spawned.Add(beam);
        SunodGame.Core.vc_Sfx.Play("scan", 0.6f);

        float sweep = 1.2f, t = 0f;
        bool[] noted = new bool[3];
        while (t < sweep)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(c.x - 1.5f, c.x + 1.9f, t / sweep);
            beam.transform.position = Snap(new Vector3(x, c.y + 0.2f, 0f));
            for (int i = 0; i < 3 && mushrooms != null && i < mushrooms.Length; i++)
            {
                if (!noted[i] && x >= mushrooms[i].position.x - 0.1f)
                {
                    noted[i] = true;
                    if (noteIcons != null && i < noteIcons.Length)
                        Fx(new[] { noteIcons[i] }, mushrooms[i].position + new Vector3(0f, 0.95f, 0f), 1.5f, 0.4f);
                    SunodGame.Core.vc_Sfx.Play("text_blip", 0.55f, 0.1f);
                    Fx(sparkFrames, mushrooms[i].position + new Vector3(0f, 0.2f, 0f), 12f, 0.2f);
                }
            }
            SetBar(t / surveyTime);
            yield return null;
        }
        Destroy(beam);
        PopProp(notesProp);
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);
        yield return BarStep(Mathf.Max(0.1f, surveyTime - sweep), sweep / surveyTime, 1f);
        _boost = true;
        Finish("You mapped the glow. It pulses like a heartbeat!");
    }

    // ── Solve ◆: reveal the roots ────────────────────────────────────────

    private IEnumerator SolveRoutine()
    {
        Toast("Working out the secret of the glow...", solveTime);
        ShowBar(true);
        Vector3 c = Cluster();
        _boost = true;
        if (roots != null)
        {
            roots.gameObject.SetActive(true);
            roots.transform.position = Snap(c + new Vector3(0f, -0.25f, 0f));
        }
        float t = 0f, shake = 0f;
        bool bulb = false;
        while (t < solveTime)
        {
            t += Time.deltaTime;
            shake += Time.deltaTime;
            float k = t / solveTime;
            SetBar(k);
            if (roots != null)
            {
                // roots grow in, step by step
                float g = Mathf.Clamp01((k - 0.15f) / 0.6f);
                roots.transform.localScale = new Vector3(1f, Mathf.Round(g * 6f) / 6f, 1f);
            }
            if (shake >= 0.45f)
            {
                shake = 0f;
                SunodGame.Core.vc_Sfx.Play("rustle", 0.35f, 0.1f);
                Fx(dustFrames, c + new Vector3(Random.Range(-0.8f, 0.8f), -0.2f, 0f), 10f, 0.25f);
            }
            if (!bulb && t >= solveTime - 0.9f)
            {
                bulb = true;
                Fx(bulbFrames, c + new Vector3(0f, 1.9f, 0f), 2f, 0.3f, 25);
                SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
            }
            yield return null;
        }
        Finish("You cracked the secret: the roots feed on moonlight!");
    }

    // ── Plan ◆: light vs dark jars ───────────────────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Setting up a light-versus-dark test...", planTime);
        ShowBar(true);
        Vector3 c = Cluster();
        SpriteRenderer a = jars != null && jars.Length > 0 ? jars[0] : null;
        SpriteRenderer b = jars != null && jars.Length > 1 ? jars[1] : null;
        float t = 0f;

        // 1) fill two jars with spores
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer j = i == 0 ? a : b;
            if (j == null) continue;
            StartCoroutine(Fly(c + new Vector3(0f, 0.4f, 0f), j.transform.position + new Vector3(0f, 0.1f, 0f), 0.6f));
            SunodGame.Core.vc_Sfx.Play("plop", 0.5f, 0.1f);
            yield return BarStep(0.55f, t / planTime, (t + 0.55f) / planTime);
            t += 0.55f;
            j.sprite = jarDim;
        }

        // 2) cover one jar, put the other in the light
        if (jarCover != null && b != null)
        {
            jarCover.gameObject.SetActive(true);
            jarCover.transform.position = b.transform.position + new Vector3(0f, 0.05f, 0f);
            b.sprite = jarDark;
            Fx(dustFrames, b.transform.position + new Vector3(0f, -0.2f, 0f), 10f, 0.2f);
        }
        if (lightBeam != null && a != null)
        {
            lightBeam.gameObject.SetActive(true);
            lightBeam.transform.position = Snap(a.transform.position + new Vector3(0f, 1.0f, 0f));
        }
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        yield return BarStep(1.2f, t / planTime, (t + 1.2f) / planTime);
        t += 1.2f;

        // 3) wait and watch: the lit jar starts to glow, the dark one doesn't
        float rest = Mathf.Max(0.5f, planTime - t);
        if (a != null) { a.sprite = jarLit; SunodGame.Core.vc_Sfx.Play("grow", 0.5f); Fx(sparkFrames, a.transform.position + new Vector3(0f, 0.3f, 0f), 12f, 0.4f); }
        yield return BarStep(rest * 0.5f, t / planTime, (t + rest * 0.5f) / planTime);
        t += rest * 0.5f;
        if (a != null) Fx(checkFrames, a.transform.position + new Vector3(0f, 0.9f, 0f), 2f, 0.3f);
        yield return BarStep(rest * 0.5f, t / planTime, 1f);
        _boost = true;
        Finish("The lit jar glows, the dark one doesn't. Light feeds it!");
    }

    // ── Collect ○: a sample from each mushroom ───────────────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("Taking a sample from each mushroom...", 3f);
        ShowBar(true);
        Vector3 c = Cluster();
        var pr = new GameObject("Probe");
        pr.transform.SetParent(transform, true);
        var ps = pr.AddComponent<SpriteRenderer>();
        ps.sprite = probe; ps.sortingLayerName = sortingLayer; ps.sortingOrder = 22;
        _spawned.Add(pr);

        float per = (collectTime - 1.1f) / 3f, t = 0f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 m = mushrooms[i].position;
            pr.transform.position = Snap(m + new Vector3(0.45f, 0.35f, 0f));
            SunodGame.Core.vc_Sfx.Play("snip", 0.5f, 0.1f);
            Fx(sparkFrames, m + new Vector3(0.1f, 0.2f, 0f), 12f, 0.2f);
            yield return BarStep(per * 0.35f, t / collectTime, (t + per * 0.35f) / collectTime);
            t += per * 0.35f;

            if (jars != null && i < jars.Length)
            {
                StartCoroutine(Fly(m + new Vector3(0f, 0.3f, 0f), jars[i].transform.position + new Vector3(0f, 0.1f, 0f), 0.7f));
                SunodGame.Core.vc_Sfx.Play("plop", 0.5f, 0.1f);
            }
            yield return BarStep(per * 0.45f, t / collectTime, (t + per * 0.45f) / collectTime);
            t += per * 0.45f;

            if (jars != null && i < jars.Length)
            {
                jars[i].sprite = jarLit;
                if (labels != null && i < labels.Length)
                    Fx(new[] { labels[i] }, jars[i].transform.position + new Vector3(0f, 0.8f, 0f), 1.4f, 0.2f);
                SunodGame.Core.vc_Sfx.Play("tally", 0.5f, 0.05f);
            }
            yield return BarStep(per * 0.2f, t / collectTime, (t + per * 0.2f) / collectTime);
            t += per * 0.2f;
        }
        Destroy(pr);
        for (int i = 0; jars != null && i < jars.Length; i++)
            Fx(checkFrames, jars[i].transform.position + new Vector3(0f, 0.9f, 0f), 2f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.6f);
        yield return BarStep(Mathf.Max(0.1f, collectTime - t), t / collectTime, 1f);
        _boost = true;
        Finish("Every sample is bottled and labeled!");
    }

    // ── Inspect ○: a close look at each mushroom ─────────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Looking at each mushroom up close...", inspectTime);
        ShowBar(true);
        var lens = new GameObject("Lens");
        lens.transform.SetParent(transform, true);
        var ls = lens.AddComponent<SpriteRenderer>();
        ls.sprite = magnifier != null ? magnifier : probe;
        ls.sortingLayerName = sortingLayer; ls.sortingOrder = 22;
        _spawned.Add(lens);

        float per = (inspectTime - 0.9f) / 3f, t = 0f;
        for (int i = 0; i < 3 && mushrooms != null && i < mushrooms.Length; i++)
        {
            Vector3 m = mushrooms[i].position;
            // the lens drifts over the cap, then down to the stem and roots
            lens.transform.position = Snap(m + new Vector3(0.35f, 0.55f, 0f));
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.45f, 0.1f);
            yield return BarStep(per * 0.5f, t / inspectTime, (t + per * 0.5f) / inspectTime);
            t += per * 0.5f;

            lens.transform.position = Snap(m + new Vector3(0.35f, 0.05f, 0f));
            Fx(sparkFrames, m + new Vector3(0f, 0.1f, 0f), 12f, 0.2f);
            if (noteIcons != null && i < noteIcons.Length)
                Fx(new[] { noteIcons[i] }, m + new Vector3(0f, 0.95f, 0f), 1.5f, 0.4f);
            SunodGame.Core.vc_Sfx.Play("tally", 0.45f, 0.05f);
            yield return BarStep(per * 0.5f, t / inspectTime, (t + per * 0.5f) / inspectTime);
            t += per * 0.5f;
        }
        Destroy(lens);
        PopProp(notesProp);
        Fx(bulbFrames, Cluster() + new Vector3(0f, 1.9f, 0f), 2f, 0.3f, 25);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        yield return BarStep(Mathf.Max(0.1f, inspectTime - t), t / inspectTime, 1f);
        _boost = true;
        Finish("Up close, every glowing cap has the same tiny roots. That's where the light comes from!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void PopProp(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(true);
        StartCoroutine(PopScale(sr.transform));
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

    private IEnumerator Fly(Vector3 from, Vector3 to, float seconds)
    {
        var go = new GameObject("Sample");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sporeFrames != null && sporeFrames.Length > 0 ? sporeFrames[0] : null;
        sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            yield return null;
        }
        Destroy(go);
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
