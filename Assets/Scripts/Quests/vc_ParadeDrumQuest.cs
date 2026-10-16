using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quest (A6). Get the parade drum sounding for the parade. Any APPROPRIATE skill works - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   craft    A  best fit     improvise a beat: patch the head and drum it out        ~2s
///   paint    A  workaround   paint the drum bright, then play it loud                 4s
///   barrier  A  workaround   reshape the dented drumhead, then play                   5s
///   teach    S  off-letter   teach the band kids the rhythm; they march in time       8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (craft) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_ParadeDrumQuest : vc_FitQuestBase
{
    [Header("Drum")]
    [SerializeField] private SpriteRenderer drum;
    [SerializeField] private Sprite drumDented;
    [SerializeField] private Sprite drumHalf;
    [SerializeField] private Sprite drumPatched;
    [SerializeField] private Sprite drumClean;
    [SerializeField] private Sprite drumPainted;

    [Header("Ambient")]
    [SerializeField] private Transform[] pennants;

    [Header("Band kids (teach path)")]
    [SerializeField] private Sprite[] kidWalkA;
    [SerializeField] private Sprite[] kidWalkB;
    [SerializeField] private Sprite[] kidWalkC;

    [Header("Effects")]
    [SerializeField] private Sprite[] notes;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] dabs;
    [SerializeField] private Sprite brush;
    [SerializeField] private Sprite[] bulbFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private float paintTime = 4f;
    [SerializeField] private float barrierTime = 5f;
    [SerializeField] private float teachTime = 8f;

    private Vector3 _drumRest;
    private float _idleClock, _sway;
    private bool _swayFlip;
    private Vector3[] _pennantRest;
    private readonly List<GameObject> _kids = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (drum != null ? drum.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        if (drum != null) _drumRest = drum.transform.localPosition;
        int n = pennants != null ? pennants.Length : 0;
        _pennantRest = new Vector3[n];
        for (int i = 0; i < n; i++) _pennantRest[i] = pennants[i].localPosition;
    }

    protected override void OnBegin()
    {
        foreach (GameObject k in _kids) if (k != null) Destroy(k);
        _kids.Clear();
        if (drum != null)
        {
            drum.transform.localPosition = _drumRest;
            if (drumDented != null) drum.sprite = drumDented;
        }
        _idleClock = 0f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Parade Drum",
            "The parade drum is dented and silent. Get it sounding for the parade.",
            new[] { "Play the drum" });
    }

    // ── Idle life: swaying pennants, a stray note now and then ───────────

    private void Update()
    {
        _sway += Time.deltaTime;
        if (_sway >= 0.55f)
        {
            _sway = 0f;
            _swayFlip = !_swayFlip;
            for (int i = 0; i < _pennantRest.Length; i++)
                pennants[i].localPosition = _pennantRest[i] + new Vector3(((i + (_swayFlip ? 1 : 0)) % 2 == 0 ? 1 : -1) / 22f, 0f, 0f);
        }

        if (!Started || Done || Working || drum == null) return;
        _idleClock += Time.deltaTime;
        if (_idleClock >= 3.2f)
        {
            _idleClock = 0f;
            StartCoroutine(Bounce(true));
            SunodGame.Core.vc_Sfx.Play("drum_hit", 0.2f, 0.1f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft"))
        {
            StartWork(CraftRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("barrier")) { StartWork(BarrierRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }

        Toast("That won't make music.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        foreach (GameObject k in _kids) if (k != null) Destroy(k);
        _kids.Clear();
        if (drum != null) drum.transform.localPosition = _drumRest;
    }

    // ── Craft: improvise a beat ──────────────────────────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You tap out an improvised beat...", craftTime);
        ShowBar(true);
        int hits = 4;
        float per = craftTime / hits;
        for (int i = 0; i < hits; i++)
        {
            if (i == 1 && drumPatched != null) drum.sprite = drumPatched;
            Hit();
            yield return BarStep(per, i / (float)hits, (i + 1f) / hits);
        }
        Finish("Boom! You made up a beat!");
    }

    // ── Paint: splash on colour, then play it loud ───────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("Painting the drum bright...", paintTime);
        ShowBar(true);
        Vector3 c = drum.transform.position;
        float paintPhase = paintTime * 0.58f;
        int strokes = 6;
        float per = paintPhase / strokes, t = 0f;
        for (int i = 0; i < strokes; i++)
        {
            Vector3 p = c + new Vector3(Random.Range(-0.65f, 0.65f), Random.Range(-0.35f, 0.4f), 0f);
            if (dabs != null && dabs.Length > 0) Fx(new[] { dabs[i % dabs.Length] }, p, 2.5f, 0f, 14);
            Fx(new[] { brush }, p + new Vector3(0.3f, 0.3f, 0f), 4f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("brush", 0.4f, 0.1f);
            yield return BarStep(per, t / paintTime, (t + per) / paintTime);
            t += per;
        }
        if (drumPainted != null) drum.sprite = drumPainted;
        Fx(bulbFrames, c + new Vector3(0f, 1.2f, 0f), 2f, 0.3f, 25);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        Toast("Now play it loud!", 1.5f);

        int hits = 4;
        float rest = paintTime - t, hper = rest / hits;
        for (int i = 0; i < hits; i++)
        {
            Hit();
            Confetti(c);
            yield return BarStep(hper, (t + hper * i) / paintTime, (t + hper * (i + 1)) / paintTime);
        }
        Finish("You painted the drum and played it loud and proud!");
    }

    private void Confetti(Vector3 c)
    {
        if (dabs == null) return;
        for (int i = 0; i < 4; i++)
            Fx(new[] { dabs[Random.Range(0, dabs.Length)] }, c + new Vector3(Random.Range(-0.8f, 0.8f), 0.6f, 0f), 2.2f, Random.Range(0.5f, 1.1f), 21);
    }

    // ── Barrier: reshape the dented head, then play ──────────────────────

    private IEnumerator BarrierRoutine()
    {
        Toast("Reshaping the dented drumhead...", barrierTime);
        ShowBar(true);
        float shapePhase = barrierTime * 0.66f, t = 0f;
        Vector3 c = drum.transform.position;
        float[] marks = { 0.05f, 0.4f, 0.75f };
        int stage = 0;
        while (t < shapePhase)
        {
            t += Time.deltaTime;
            SetBar(t / barrierTime);
            float k = t / shapePhase;
            if (stage < marks.Length && k >= marks[stage])
            {
                stage++;
                SunodGame.Core.vc_Sfx.Play(stage == 3 ? "clack" : "hammer", 0.55f, 0.1f);
                Fx(ringFrames, c + new Vector3(-0.2f, 0.4f, 0f), 6f, 0f, 21);
                StartCoroutine(Shake(drum.transform, _drumRest, 0.18f, 1));
                if (stage == 2 && drumHalf != null) drum.sprite = drumHalf;
                if (stage == 3 && drumClean != null) drum.sprite = drumClean;
            }
            yield return null;
        }
        if (drumClean != null) drum.sprite = drumClean;

        int hits = 3;
        float rest = barrierTime - shapePhase, hper = rest / hits;
        for (int i = 0; i < hits; i++)
        {
            Hit();
            yield return BarStep(hper, (shapePhase + hper * i) / barrierTime, (shapePhase + hper * (i + 1)) / barrierTime);
        }
        Finish("You reshaped the drumhead. It rings out clean!");
    }

    // ── Teach: the band kids learn the rhythm and march in time ──────────

    private IEnumerator TeachRoutine()
    {
        Toast("You teach the band kids the rhythm...", 3f);
        ShowBar(true);
        Sprite[][] sets = { kidWalkA, kidWalkB, kidWalkC };
        Vector3[] slots = { new Vector3(-2.0f, 0.1f, 0f), new Vector3(2.0f, 0.1f, 0f), new Vector3(0f, -0.9f, 0f) };
        var kids = new SpriteRenderer[3];
        var from = new Vector3[3];
        var to = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            to[i] = new Vector3(slots[i].x, drum.transform.position.y - 1.5f + slots[i].y, 0f);
            from[i] = new Vector3(to[i].x - 6f, to[i].y, 0f);
            var go = new GameObject("BandKid" + i);
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(from[i]);
            kids[i] = go.AddComponent<SpriteRenderer>();
            if (sets[i] != null && sets[i].Length > 0) kids[i].sprite = sets[i][0];
            kids[i].sortingLayerName = sortingLayer;
            kids[i].sortingOrder = 12;
            _kids.Add(go);
        }

        float walkEnd = 3.4f, beat = 0.5f, nextBeat = walkEnd, t = 0f;
        bool taught = false;
        while (t < teachTime)
        {
            t += Time.deltaTime;
            SetBar(t / teachTime);
            for (int i = 0; i < 3; i++)
            {
                Sprite[] set = sets[i];
                if (set == null || set.Length == 0) continue;
                float p = Mathf.Clamp01((t - i * 0.4f) / 2.6f);
                if (p < 1f)
                {
                    kids[i].transform.position = Snap(Vector3.Lerp(from[i], to[i], p));
                    kids[i].sprite = set[(int)(t * 8f) % set.Length];
                }
                else
                {
                    bool up = ((int)(t * 6f) + i) % 2 == 0;
                    kids[i].transform.position = Snap(to[i] + new Vector3(0f, up ? 1f / 22f : 0f, 0f));
                    kids[i].sprite = set[(int)(t * 6f) % set.Length];
                }
            }
            if (!taught && t >= walkEnd)
            {
                taught = true;
                Toast("They're marching in time!", 3f);
            }
            if (t >= nextBeat)
            {
                nextBeat += beat;
                Hit();
            }
            yield return null;
        }
        Finish("The band kids caught the beat and marched in time!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>One drum hit: bounce, ring, floating note and a thump.</summary>
    private void Hit()
    {
        StartCoroutine(Bounce(false));
        Vector3 c = drum.transform.position;
        Fx(ringFrames, c + new Vector3(0f, 0.2f, 0f), 7f, 0f, 21);
        if (notes != null && notes.Length > 0)
            Fx(new[] { notes[Random.Range(0, notes.Length)] }, c + new Vector3(Random.Range(-0.5f, 0.5f), 0.9f, 0f), 1.4f, 0.8f, 22);
        SunodGame.Core.vc_Sfx.Play("drum_hit", 0.75f, 0.08f);
    }

    private IEnumerator Bounce(bool soft)
    {
        if (drum == null) yield break;
        drum.transform.localPosition = _drumRest + new Vector3(0f, (soft ? -1f : -3f) / 22f, 0f);
        yield return new WaitForSeconds(0.05f);
        if (drum == null) yield break;
        drum.transform.localPosition = _drumRest + new Vector3(0f, (soft ? 0f : 2f) / 22f, 0f);
        yield return new WaitForSeconds(0.06f);
        if (drum != null) drum.transform.localPosition = _drumRest;
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
