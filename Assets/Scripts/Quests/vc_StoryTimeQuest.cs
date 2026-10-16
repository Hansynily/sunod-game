using System.Collections;
using UnityEngine;

/// <summary>
/// S quest (S7). Help the little kids read the storybook. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   teach    S  best fit     letter bubbles pop over the kids; they read aloud            ~2s
///   guide    S  workaround   read the whole book together; the pages flip                  4s
///   heal     S  workaround   coax the shy kid out from behind the beanbag, then all read   5s
///   paint    A  off-letter   draw pictures for the story; the kids follow along            8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (teach) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_StoryTimeQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private Transform[] kids;
    [Tooltip("Index in Kids of the shy kid who starts hiding behind the beanbag.")]
    [SerializeField] private int shyKid = 2;
    [SerializeField] private Transform shyHome;
    [SerializeField] private SpriteRenderer storybook;
    [SerializeField] private Sprite[] bookFrames;
    [SerializeField] private Transform rug;

    [Header("Effects")]
    [SerializeField] private Sprite[] letterBubbles;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite heart;
    [SerializeField] private Sprite[] pictures;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float teachTime = 2f;
    [SerializeField] private float guideTime = 4f;
    [SerializeField] private float healTime = 5f;
    [SerializeField] private float paintTime = 8f;

    private SpriteRenderer[] _sr;
    private Vector3[] _rest;
    private int[] _restOrder;
    private Vector3 _shyPos, _readPos;
    private float _idleClock, _dotsClock;
    private bool _shy;
    private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    protected override Vector3 BarWorldPosition => (rug != null ? rug.position : transform.position) + new Vector3(-0.5f, 2.6f, 0f);

    private void Awake()
    {
        int n = kids != null ? kids.Length : 0;
        _sr = new SpriteRenderer[n];
        _rest = new Vector3[n];
        _restOrder = new int[n];
        for (int i = 0; i < n; i++)
        {
            _sr[i] = kids[i].GetComponent<SpriteRenderer>();
            _rest[i] = kids[i].position;
            _restOrder[i] = _sr[i] != null ? _sr[i].sortingOrder : 10;
        }
        if (shyKid >= 0 && shyKid < n) _readPos = _rest[shyKid];
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        for (int i = 0; i < _sr.Length; i++)
        {
            if (i == shyKid) continue;
            kids[i].position = _rest[i];
            _sr[i].sortingOrder = _restOrder[i];
        }
        _shy = false;
        if (shyKid >= 0 && shyKid < _sr.Length)
        {
            _rest[shyKid] = shyHome != null ? shyHome.position : _readPos;
            kids[shyKid].position = _rest[shyKid];
            if (shyHome != null)
            {
                _shy = true;
                _sr[shyKid].sortingOrder = 1; // tucked behind the beanbag
            }
            else _sr[shyKid].sortingOrder = _restOrder[shyKid];
        }

        if (storybook != null && bookFrames != null && bookFrames.Length > 0) storybook.sprite = bookFrames[0];
        _idleClock = 0f; _dotsClock = 1f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Story Time",
            "Toto and his friends are learning to read. Some words are hard. Help them!",
            new[] { "Read the storybook with the little kids" });
    }

    // ── Idle life: happy hops, the shy kid peeks out ─────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f && !Working)
        {
            _idleClock = Random.Range(1.2f, 2.6f);
            int i = Random.Range(0, _sr.Length);
            if (!(_shy && i == shyKid)) StartCoroutine(Hop(i, 2));
        }

        _dotsClock -= Time.deltaTime;
        if (_shy && _dotsClock <= 0f && dotsBubble != null)
        {
            _dotsClock = 2.6f;
            Fx(new[] { dotsBubble }, Top(shyKid), 1.5f, 0.2f, 24);
        }
    }

    private Vector3 Top(int i)
    {
        Bounds b = _sr[i] != null ? _sr[i].bounds : new Bounds(kids[i].position, Vector3.one);
        return new Vector3(b.center.x, b.max.y + 0.35f, 0f);
    }

    private IEnumerator Hop(int i, int px)
    {
        if (kids[i] == null) yield break;
        kids[i].position = _rest[i] + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (kids[i] != null) kids[i].position = _rest[i] + new Vector3(0f, px / 44f, 0f);
        yield return new WaitForSeconds(0.08f);
        if (kids[i] != null) kids[i].position = _rest[i];
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        Toast("Reading needs a patient teacher.", 2.2f);
    }

    // ── Teach: letter bubbles over each kid ──────────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("You teach the kids their letters...", teachTime);
        ShowBar(true);
        int n = _sr.Length;
        float per = (teachTime - 0.3f) / Mathf.Max(1, n), t = 0f;
        for (int i = 0; i < n; i++)
        {
            ReadAloud(i);
            yield return BarStep(per, t / teachTime, (t + per) / teachTime);
            t += per;
        }
        yield return BarStep(teachTime - t, t / teachTime, 1f);
        Finish("They sound out the words together. Well done!");
    }

    private void ReadAloud(int i)
    {
        if (letterBubbles != null && letterBubbles.Length > 0)
            Fx(new[] { letterBubbles[i % letterBubbles.Length] }, Top(i), 1.6f, 0.4f, 24);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.12f);
        StartCoroutine(Hop(i, 3));
    }

    // ── Guide: read the whole book, flipping pages ───────────────────────

    private IEnumerator GuideRoutine()
    {
        Toast("You read the whole book together...", guideTime);
        ShowBar(true);
        int flips = 4;
        float per = guideTime / flips, t = 0f;
        for (int f = 0; f < flips; f++)
        {
            SunodGame.Core.vc_Sfx.Play("page_next", 0.6f, 0.1f);
            if (storybook != null && bookFrames != null && bookFrames.Length >= 3)
            {
                storybook.sprite = bookFrames[1];
                yield return BarStep(per * 0.3f, t / guideTime, (t + per * 0.3f) / guideTime);
                storybook.sprite = bookFrames[f % 2 == 0 ? 2 : 0];
                yield return BarStep(per * 0.2f, (t + per * 0.3f) / guideTime, (t + per * 0.5f) / guideTime);
            }
            else yield return BarStep(per * 0.5f, t / guideTime, (t + per * 0.5f) / guideTime);
            ReadAloud(f % _sr.Length);
            yield return BarStep(per * 0.5f, (t + per * 0.5f) / guideTime, (t + per) / guideTime);
            t += per;
        }
        Finish("You read the whole book together. The end!");
    }

    // ── Heal: coax the shy kid out, then everyone reads ──────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You gently coax the shy kid out...", healTime);
        ShowBar(true);
        float total = healTime, t = 0f;

        if (_shy && heart != null) Fx(new[] { heart }, Top(shyKid), 1.5f, 0.3f, 24);
        SunodGame.Core.vc_Sfx.Play("quest_start", 0.35f);
        yield return BarStep(0.8f, 0f, 0.8f / total);
        t = 0.8f;

        if (_shy)
        {
            Vector3 from = kids[shyKid].position, to = _readPos;
            float walk = 2.2f, w = 0f;
            int hops = 5;
            while (w < walk)
            {
                w += Time.deltaTime;
                float k = Mathf.Clamp01(w / walk);
                float hopK = (k * hops) % 1f;
                kids[shyKid].position = Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(hopK * Mathf.PI) * 0.12f, 0f);
                if (k > 0.3f && _sr[shyKid].sortingOrder != _restOrder[shyKid]) _sr[shyKid].sortingOrder = _restOrder[shyKid];
                SetBar((t + w) / total);
                yield return null;
            }
            _rest[shyKid] = to;
            kids[shyKid].position = to;
            _shy = false;
            t += walk;
            if (heart != null) Fx(new[] { heart }, Top(shyKid), 1.5f, 0.3f, 24);
            SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        }

        float per = Mathf.Max(0.1f, (total - t) / Mathf.Max(1, _sr.Length));
        for (int i = 0; i < _sr.Length; i++)
        {
            ReadAloud(i);
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
        Finish("You helped the shy kid feel brave. Now everyone reads!");
    }

    // ── Paint: draw pictures for the story ───────────────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("You draw pictures to go with the story...", paintTime);
        ShowBar(true);
        Vector3 c = rug != null ? rug.position : transform.position;
        Vector3[] spots = { c + new Vector3(-1.2f, -1.1f, 0f), c + new Vector3(0f, -1.25f, 0f), c + new Vector3(1.2f, -1.1f, 0f) };
        int cards = Mathf.Min(3, pictures != null ? pictures.Length : 0);
        float per = (paintTime - 1.8f) / Mathf.Max(1, cards), t = 0f;
        for (int i = 0; i < cards; i++)
        {
            var go = new GameObject("Picture");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(spots[i]);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pictures[i]; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 12;
            _spawned.Add(go);
            StartCoroutine(PopScale(go.transform));
            Fx(sparkFrames, spots[i] + new Vector3(0.3f, 0.2f, 0f), 12f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.35f, 0.1f);
            // kids lean in and point
            for (int k = 0; k < _sr.Length; k++) StartCoroutine(Hop(k, 2));
            yield return BarStep(per, t / paintTime, (t + per) / paintTime);
            t += per;
        }
        float rest = (paintTime - t) / Mathf.Max(1, _sr.Length);
        for (int i = 0; i < _sr.Length; i++)
        {
            ReadAloud(i);
            yield return BarStep(rest, t / paintTime, (t + rest) / paintTime);
            t += rest;
        }
        Finish("The pictures helped! The kids read along happily.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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
