using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S quest (S2). A sparrow is hurt on the forest floor. Any APPROPRIATE skill helps it - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   heal     S  best fit     bandage the wing; the sparrow hops up and flutters off       ~2s
///   guide    S  workaround   guide it hop by hop into the nest box                          4s
///   teach    S  workaround   two kids walk in and learn to care for it; it perks up        5s
///   craft    A  off-letter   gather twigs, make a little splint, fit it on the wing         8s
///   summon   E  off-letter   the volunteer vet walks over and treats it                    10s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (heal) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_SparrowRescueQuest : vc_FitQuestBase
{
    [Header("Sparrow")]
    [SerializeField] private SpriteRenderer bird;
    [SerializeField] private Sprite hurt0, hurt1, sit, hop, bandaged, splint;
    [SerializeField] private Sprite[] fly;
    [SerializeField] private Transform nest;

    [Header("Kids (teach path)")]
    [SerializeField] private Sprite[] kidWalk;
    [SerializeField] private Sprite[] kidHappy;

    [Header("Effects")]
    [SerializeField] private Sprite[] heartFx;
    [SerializeField] private Sprite[] dotsFx;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] feathers;
    [SerializeField] private Sprite bandageRoll;
    [SerializeField] private Sprite medkit;
    [SerializeField] private Sprite twig;
    [SerializeField] private Sprite tape;
    [SerializeField] private Sprite chevron;

    [Header("Timing (seconds)")]
    [SerializeField] private float healTime = 2f;
    [SerializeField] private float guideTime = 4f;
    [SerializeField] private float teachTime = 5f;
    [SerializeField] private float craftTime = 8f;
    [SerializeField] private float vetWalkTime = 6f;
    [SerializeField] private float vetFixTime = 4f;

    private Vector3 _birdRest;
    private bool _hurt, _flipRest;
    private float _breathClock, _chirpClock;
    private int _breathFrame;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Coroutine _ending;

    protected override Vector3 BarWorldPosition => (bird != null ? bird.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        if (bird != null) { _birdRest = bird.transform.position; _flipRest = bird.flipX; }
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        if (_ending != null) { StopCoroutine(_ending); _ending = null; }
        if (bird != null)
        {
            bird.transform.position = _birdRest;
            bird.flipX = _flipRest;
            bird.sprite = hurt0;
            bird.sortingOrder = 6;
            bird.gameObject.SetActive(true);
        }
        _hurt = true; _breathClock = 0f; _chirpClock = 1.5f; _breathFrame = 0;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Fallen Sparrow",
            "A small bird is hurt and needs your help. Treat its injuries.",
            new[] { "Help the hurt sparrow" });
    }

    // ── Idle life: it breathes, cheeps weakly ────────────────────────────

    private void Update()
    {
        if (!Started || Done || !_hurt || bird == null) return;

        _breathClock += Time.deltaTime;
        if (_breathClock >= 0.7f)
        {
            _breathClock = 0f;
            _breathFrame = 1 - _breathFrame;
            bird.sprite = _breathFrame == 0 ? hurt0 : hurt1;
        }

        _chirpClock -= Time.deltaTime;
        if (_chirpClock <= 0f)
        {
            _chirpClock = 3.2f;
            Fx(dotsFx, bird.transform.position + new Vector3(0f, 0.75f, 0f), 1.5f, 0.2f);
            SunodGame.Core.vc_Sfx.Play("chirp", 0.22f, 0.1f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(VetRoutine()); return; }
        Toast("The bird needs gentle care.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        if (bird == null) return;
        // a half-done fix leaves the bird where it was, still hurt
        if (_hurt) bird.sprite = hurt0;
    }

    // ── Heal: bandage the wing ───────────────────────────────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You gently bandage the wing...", healTime);
        ShowBar(true);
        Vector3 b = bird.transform.position;
        Fx(new[] { bandageRoll }, b + new Vector3(0.1f, 0.9f, 0f), 1.8f, -0.45f);
        SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.4f);
        yield return BarStep(0.7f, 0f, 0.35f);
        SetBandaged();
        Fx(sparkFrames, b + new Vector3(0.1f, 0.2f, 0f), 12f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        yield return BarStep(0.6f, 0.35f, 0.7f);
        Fx(heartFx, b + new Vector3(0f, 0.8f, 0f), 2f, 0.4f);
        bird.sprite = sit;
        SunodGame.Core.vc_Sfx.Play("chirp", 0.6f);
        yield return BarStep(healTime - 1.3f, 0.7f, 1f);
        _ending = StartCoroutine(FlyAway());
        Finish("You bandage the wing. The sparrow hops up!");
    }

    private void SetBandaged()
    {
        _hurt = false;
        bird.sprite = bandaged;
    }

    // ── Guide: hop by hop into the nest box ──────────────────────────────

    private IEnumerator GuideRoutine()
    {
        Toast("You guide the sparrow toward the nest...", guideTime);
        ShowBar(true);
        SetBandaged();
        bird.sprite = sit;
        Vector3 from = bird.transform.position;
        Vector3 to = (nest != null ? nest.position : from + new Vector3(2.5f, 0.5f, 0f)) + new Vector3(0f, 0.35f, 0f);
        bird.flipX = to.x > from.x;
        int hops = 4;
        float per = guideTime / hops;
        for (int h = 0; h < hops; h++)
        {
            Vector3 a = Vector3.Lerp(from, to, h / (float)hops);
            Vector3 c = Vector3.Lerp(from, to, (h + 1f) / hops);
            Fx(new[] { chevron }, a + new Vector3(0.45f, 0.5f, 0f), 1.8f, 0.1f);
            SunodGame.Core.vc_Sfx.Play("chirp", 0.35f, 0.12f);
            float t = 0f;
            while (t < per)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / per);
                float arc = Mathf.Sin(Mathf.Clamp01((k - 0.25f) / 0.5f) * Mathf.PI) * 0.35f;
                bird.transform.position = Vector3.Lerp(a, c, Mathf.Clamp01((k - 0.2f) / 0.6f)) + new Vector3(0f, arc, 0f);
                bird.sprite = (k > 0.2f && k < 0.8f) ? hop : sit;
                SetBar((h + k) / hops);
                yield return null;
            }
            Fx(dustFrames, c + new Vector3(0f, -0.3f, 0f), 12f, 0.1f);
        }
        bird.transform.position = to;
        bird.sprite = sit;
        bird.sortingOrder = 7;
        Fx(heartFx, to + new Vector3(0f, 0.7f, 0f), 2f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("chirp", 0.7f);
        Finish("The sparrow settles into its nest. Safe and warm!");
    }

    // ── Teach: two kids learn how to care for it ─────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("You show the kids how to care for it...", teachTime);
        ShowBar(true);
        Vector3 b = bird.transform.position;
        Vector3[] to = { b + new Vector3(-1.0f, -0.35f, 0f), b + new Vector3(1.0f, -0.35f, 0f) };
        Vector3[] from = { to[0] + new Vector3(-5.5f, 0f, 0f), to[1] + new Vector3(5.5f, 0.3f, 0f) };
        var kids = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Kid");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(from[i]);
            kids[i] = go.AddComponent<SpriteRenderer>();
            kids[i].sprite = kidWalk != null && kidWalk.Length > 0 ? kidWalk[0] : null;
            kids[i].sortingLayerName = sortingLayer; kids[i].sortingOrder = 7;
            kids[i].flipX = i == 1;
            _spawned.Add(go);
        }

        float walk = 2.4f, t = 0f;
        while (t < walk)
        {
            t += Time.deltaTime;
            for (int i = 0; i < 2; i++)
            {
                kids[i].transform.position = Snap(Vector3.Lerp(from[i], to[i], t / walk));
                if (kidWalk != null && kidWalk.Length > 0) kids[i].sprite = kidWalk[(int)(t * 8f) % kidWalk.Length];
            }
            SetBar(t / teachTime);
            yield return null;
        }
        for (int i = 0; i < 2; i++) if (kidHappy != null && kidHappy.Length > 0) kids[i].sprite = kidHappy[0];

        // each kid copies you: bandage, then hearts
        Fx(new[] { bandageRoll }, kids[0].transform.position + new Vector3(0.5f, 1.1f, 0f), 1.8f, -0.4f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        yield return BarStep(0.9f, walk / teachTime, (walk + 0.9f) / teachTime);
        SetBandaged();
        Fx(sparkFrames, b + new Vector3(0.1f, 0.2f, 0f), 12f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        Fx(heartFx, kids[1].transform.position + new Vector3(0f, 1.2f, 0f), 2f, 0.4f);
        yield return BarStep(0.9f, (walk + 0.9f) / teachTime, (walk + 1.8f) / teachTime);

        bird.sprite = sit;
        for (int i = 0; i < 2; i++) if (kidHappy != null && kidHappy.Length > 1) kids[i].sprite = kidHappy[1];
        Fx(heartFx, b + new Vector3(0f, 0.8f, 0f), 2f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("chirp", 0.6f);
        yield return BarStep(teachTime - walk - 1.8f, (walk + 1.8f) / teachTime, 1f);
        _ending = StartCoroutine(Hops(bird, 2));
        Finish("The kids learned to help. The sparrow perks up!");
    }

    // ── Craft: gather twigs, make a splint, fit it ───────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You make a little splint for the wing...", craftTime);
        ShowBar(true);
        Vector3 b = bird.transform.position;
        float t = 0f;

        // gather three twigs
        Vector3[] spots = { b + new Vector3(-1.3f, -0.2f, 0f), b + new Vector3(-0.2f, -0.55f, 0f), b + new Vector3(1.1f, -0.25f, 0f) };
        for (int i = 0; i < 3; i++)
        {
            Fx(new[] { twig }, spots[i] + new Vector3(0f, 0.5f, 0f), 1.4f, -0.3f);
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.12f);
            yield return BarStep(0.9f, t / craftTime, (t + 0.9f) / craftTime);
            t += 0.9f;
        }

        // tie them with tape and tap into shape
        Fx(new[] { tape }, b + new Vector3(-0.2f, 0.9f, 0f), 1.4f, 0.1f);
        for (int i = 0; i < 4; i++)
        {
            Fx(hammerFrames, b + new Vector3(-0.1f + 0.1f * (i % 2), 0.9f, 0f), 8f);
            yield return new WaitForSeconds(0.14f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.4f, 0.1f);
            Fx(sparkFrames, b + new Vector3(0f, 0.55f, 0f), 12f, 0.1f);
            yield return BarStep(0.5f, t / craftTime, (t + 0.5f) / craftTime);
            t += 0.5f;
        }

        // fit the splint
        Fx(new[] { twig }, b + new Vector3(0.2f, 0.6f, 0f), 2f, -0.3f);
        yield return BarStep(0.7f, t / craftTime, (t + 0.7f) / craftTime);
        t += 0.7f;
        _hurt = false;
        bird.sprite = splint;
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        Fx(sparkFrames, b + new Vector3(0.1f, 0.25f, 0f), 12f, 0.15f);
        yield return BarStep(0.6f, t / craftTime, (t + 0.6f) / craftTime);
        t += 0.6f;
        Fx(heartFx, b + new Vector3(0f, 0.8f, 0f), 2f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("chirp", 0.6f);
        yield return BarStep(Mathf.Max(0.05f, craftTime - t), t / craftTime, 1f);
        _ending = StartCoroutine(FlyAway());
        Finish("The little splint holds. The sparrow flaps free!");
    }

    // ── Call: the volunteer vet walks over and treats it ─────────────────

    private IEnumerator VetRoutine()
    {
        Toast("You call the volunteer vet...", 3f);
        ShowBar(true);
        Vector3 b = bird.transform.position;
        Vector3 to = b + new Vector3(-1.1f, -0.3f, 0f);
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        SpawnHelper(from, 7);
        float total = vetWalkTime + vetFixTime;

        yield return HelperWalk(from, to, vetWalkTime, t => SetBar(t / total));

        Toast("The vet gets to work...", vetFixTime);
        Fx(new[] { medkit }, to + new Vector3(0.5f, 1.2f, 0f), 1.6f, -0.2f);
        SunodGame.Core.vc_Sfx.Play("quest_start", 0.35f);
        float t = vetWalkTime;
        yield return BarStep(1.2f, t / total, (t + 1.2f) / total); t += 1.2f;
        Fx(new[] { bandageRoll }, b + new Vector3(0.1f, 0.9f, 0f), 1.8f, -0.45f);
        yield return BarStep(1.2f, t / total, (t + 1.2f) / total); t += 1.2f;
        SetBandaged();
        Fx(sparkFrames, b + new Vector3(0.1f, 0.2f, 0f), 12f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        yield return BarStep(0.9f, t / total, (t + 0.9f) / total); t += 0.9f;
        bird.sprite = sit;
        Fx(heartFx, b + new Vector3(0f, 0.8f, 0f), 2f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("chirp", 0.6f);
        yield return BarStep(Mathf.Max(0.05f, total - t), t / total, 1f);
        _ending = StartCoroutine(FlyAway());
        Finish("The vet fixed the wing. Thanks for calling!");
    }

    // ── Endings ──────────────────────────────────────────────────────────

    private IEnumerator Hops(SpriteRenderer sr, int n)
    {
        Vector3 rest = sr.transform.position;
        for (int i = 0; i < n; i++)
        {
            sr.sprite = hop;
            sr.transform.position = rest + new Vector3(0f, 0.3f, 0f);
            yield return new WaitForSeconds(0.14f);
            sr.sprite = sit;
            sr.transform.position = rest;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator FlyAway()
    {
        yield return Hops(bird, 1);
        Vector3 from = bird.transform.position;
        Vector3 to = from + new Vector3(-2.2f, 3.2f, 0f);
        bird.flipX = false;
        SunodGame.Core.vc_Sfx.Play("chirp", 0.7f, 0.1f);
        Fx(feathers, from + new Vector3(0f, 0.3f, 0f), 4f, -0.3f);
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            bird.transform.position = Snap(Vector3.Lerp(from, to, t / 1.4f));
            if (fly != null && fly.Length > 0) bird.sprite = fly[(int)(t * 10f) % fly.Length];
            yield return null;
        }
        bird.gameObject.SetActive(false);
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
