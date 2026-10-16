using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S quest (S6). A classmate is lost and crying; get them to class. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   guide    S  best fit     the kid takes your hand and follows you to class        ~2s + walk
///   heal     S  workaround   calm the crying kid first, then they come along         4s + walk
///   teach    S  workaround   show them how to read the room signs; they find the way 5s + walk
///   direct   C  off-letter   point out the way step by step; the kid walks it        7s
///   summon   E  off-letter   call the teacher, who comes and takes them to class     10s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (guide) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_LostFriendFitQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private Transform kid;
    [SerializeField] private Transform classroom;

    [Header("Kid frames (art faces right; flipped when walking left)")]
    [SerializeField] private Sprite[] sadFrames;
    [SerializeField] private Sprite[] happyFrames;   // 0 = wave, 1 = stand
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] holdFrames;

    [Header("Effects")]
    [SerializeField] private Sprite tearDrop;
    [SerializeField] private Sprite questionBubble;
    [SerializeField] private Sprite chevron;
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] plaques;
    [SerializeField] private Sprite[] checkFrames;

    [Header("Timing")]
    [SerializeField] private float guideApproachTime = 2f;
    [SerializeField] private float healTime = 4f;
    [SerializeField] private float teachTime = 5f;
    [SerializeField] private float directTime = 7f;
    [SerializeField] private float teacherWalkTime = 3.8f;
    [SerializeField] private float teacherMeetTime = 1.2f;
    [SerializeField] private float teacherBackTime = 5f;
    [SerializeField] private float kidSpeed = 2.4f;
    [SerializeField] private float arriveRange = 1.7f;

    private enum Mode { Sad, Calm }
    private Mode _mode;
    private SpriteRenderer _kidSr;
    private Vector3 _kidStart;
    private Vector3 _kidExact;   // un-snapped position: snapping a ~1px step each frame would round back and stall the walk
    private Transform _player;
    private Transform _bubble;
    private float _animClock, _tearClock, _sniffleClock;
    private int _sadFrame;
    private bool _following;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => KidBarPos();

    private Vector3 KidBarPos() => (kid != null ? kid.position : transform.position) + new Vector3(-0.5f, 1.75f, 0f);
    private Vector3 DoorPoint() => (classroom != null ? classroom.position : transform.position) + new Vector3(0f, -1.3f, 0f);

    private void Awake()
    {
        if (kid != null)
        {
            _kidSr = kid.GetComponent<SpriteRenderer>();
            _kidStart = kid.position;
            _kidExact = kid.position;
        }
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _following = false;
        if (kid != null) SetKidPos(_kidStart);
        if (_kidSr != null) _kidSr.flipX = false;
        SetMode(Mode.Sad);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Lost Friend",
            "Your classmate got lost on the way to class. Help them find their way.",
            new[] { "Get your friend to class" });
    }

    // ── Idle life: the lost kid sobs ─────────────────────────────────────

    private void SetMode(Mode m)
    {
        _mode = m;
        if (m == Mode.Sad)
        {
            SetSprite(sadFrames, 0);
            if (_bubble == null && questionBubble != null && kid != null)
            {
                var go = new GameObject("LostBubble");
                go.transform.SetParent(transform, true);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = questionBubble; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 24;
                _bubble = go.transform;
            }
        }
        else
        {
            SetSprite(happyFrames, 1);
            if (_bubble != null) { Destroy(_bubble.gameObject); _bubble = null; }
        }
    }

    private void SetSprite(Sprite[] set, int i)
    {
        if (_kidSr != null && set != null && set.Length > 0) _kidSr.sprite = set[Mathf.Clamp(i, 0, set.Length - 1)];
    }

    private void Update()
    {
        if (!Started || Done || _mode != Mode.Sad || kid == null) return;

        _animClock += Time.deltaTime;
        if (_animClock >= 0.5f) { _animClock = 0f; _sadFrame = 1 - _sadFrame; SetSprite(sadFrames, _sadFrame); }

        _tearClock += Time.deltaTime;
        if (_tearClock >= 0.9f && _sadFrame == 0)
        {
            _tearClock = 0f;
            Fx(new[] { tearDrop }, kid.position + new Vector3(0.18f, 0.95f, 0f), 2.5f, -0.25f);
            Fx(new[] { tearDrop }, kid.position + new Vector3(-0.18f, 0.95f, 0f), 2.5f, -0.25f);
        }

        _sniffleClock += Time.deltaTime;
        if (_sniffleClock >= 3.2f) { _sniffleClock = 0f; SunodGame.Core.vc_Sfx.Play("sniffle", 0.35f, 0.08f); }

        if (_bubble != null)
            _bubble.position = Snap(kid.position + new Vector3(0.1f, 1.45f + Mathf.Round(Mathf.Sin(Time.time * 3f) * 2f) / 22f, 0f));
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("guide"))
        {
            if (_following) { Toast("They're already holding your hand.", 1.5f); return; }
            StartWork(GuideRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(TeacherRoutine()); return; }

        Toast("Your friend needs someone to look after them.", 2.4f);
    }

    protected override void OnWorkCancelled()
    {
        _following = false;
    }

    // ── Guide: take their hand and lead them to class ────────────────────

    private IEnumerator GuideRoutine()
    {
        Toast("Your friend takes your hand. Lead the way!", 3f);
        SetMode(Mode.Calm);
        ShowBar(true);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        Fx(heartFrames, kid.position + new Vector3(0f, 1.3f, 0f), 3f, 0.3f);

        // runs to the player in ~2 seconds
        float t = 0f;
        while (t < guideApproachTime)
        {
            t += Time.deltaTime;
            Transform p = Player();
            if (p != null) StepKid(HandSpot(p), Mathf.Max(kidSpeed, Vector2.Distance(kid.position, p.position) / Mathf.Max(0.1f, guideApproachTime - t)), true);
            SetBar(t / guideApproachTime);
            MoveBar(KidBarPos());
            yield return null;
        }
        ShowBar(false);
        yield return FollowToClass();
        yield return Arrive("Friend guided to class!");
    }

    private IEnumerator FollowToClass()
    {
        _following = true;
        while (Vector2.Distance(kid.position, classroom.position) >= arriveRange)
        {
            Transform p = Player();
            if (p != null)
            {
                Vector3 target = HandSpot(p);
                float dist = Vector2.Distance(kid.position, target);
                if (dist > 0.08f) StepKid(target, Mathf.Max(kidSpeed, dist * 3f), true);
                else { SetSprite(holdFrames, 0); _kidSr.flipX = p.position.x < kid.position.x; }
            }
            yield return null;
        }
        _following = false;
    }

    private void SetKidPos(Vector3 p)
    {
        _kidExact = p;
        kid.position = Snap(p);
    }

    private Vector3 HandSpot(Transform p) => p.position + new Vector3(p.position.x >= kid.position.x ? -0.7f : 0.7f, -0.05f, 0f);

    private Transform Player()
    {
        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        return _player;
    }

    /// <summary>Moves the kid one frame toward a point, animating the walk (or hand-holding) frames.</summary>
    private void StepKid(Vector3 to, float speed, bool hold)
    {
        Vector3 from = _kidExact;
        Vector3 next = Vector3.MoveTowards(from, to, speed * Time.deltaTime);
        if (Mathf.Abs(next.x - from.x) > 0.001f) _kidSr.flipX = next.x < from.x;
        SetKidPos(next);
        SetSprite(hold ? holdFrames : walkFrames, (int)(Time.time * 8f) % 4);
    }

    private IEnumerator Arrive(string msg)
    {
        SetSprite(happyFrames, 0);
        _kidSr.flipX = false;
        SunodGame.Core.vc_Sfx.Play("chime", 0.7f);
        Fx(heartFrames, kid.position + new Vector3(-0.3f, 1.3f, 0f), 3f, 0.35f);
        Fx(heartFrames, kid.position + new Vector3(0.3f, 1.5f, 0f), 3f, 0.35f);
        yield return new WaitForSeconds(0.7f);
        Finish(msg);
    }

    // ── Heal: calm them down first, then they come along ─────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You calm your friend down...", healTime);
        ShowBar(true);
        float t = 0f, heart = 0f;
        bool calm = false, wave = false;
        while (t < healTime)
        {
            t += Time.deltaTime;
            heart += Time.deltaTime;
            if (heart >= 0.8f && t < 2.6f)
            {
                heart = 0f;
                Fx(heartFrames, kid.position + new Vector3(Random.Range(-0.3f, 0.3f), 1.3f, 0f), 3f, 0.35f);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            }
            if (!calm && t >= 2.6f) { calm = true; SetMode(Mode.Calm); }
            if (!wave && t >= 3.3f) { wave = true; SetSprite(happyFrames, 0); SunodGame.Core.vc_Sfx.Play("chime", 0.6f); }
            SetBar(t / healTime);
            yield return null;
        }
        ShowBar(false);
        Toast("Feeling better, your friend comes along.", 2.5f);
        yield return FollowToClass();
        yield return Arrive("You calmed your friend and got them to class!");
    }

    // ── Teach: show them how to read the signs; they find the way ────────

    private IEnumerator TeachRoutine()
    {
        Toast("You show them how to read the signs...", teachTime);
        ShowBar(true);
        float t = 0f;
        int shown = 0;
        float[] when = { 0.3f, 1.4f, 2.5f };
        float[] dx = { -0.7f, 0f, 0.7f };
        while (t < teachTime)
        {
            t += Time.deltaTime;
            if (shown < 3 && plaques != null && shown < plaques.Length && t >= when[shown])
            {
                Fx(new[] { plaques[shown] }, kid.position + new Vector3(dx[shown], 1.5f, 0f), 0.75f, 0.25f);
                SunodGame.Core.vc_Sfx.Play("page_next", 0.5f, 0.1f);
                shown++;
            }
            if (_mode == Mode.Sad && t >= 3.2f) SetMode(Mode.Calm);
            if (t >= 4.2f) SetSprite(happyFrames, 0);
            SetBar(t / teachTime);
            yield return null;
        }
        ShowBar(false);
        SunodGame.Core.vc_Sfx.Play("idea", 0.5f);
        Fx(checkFrames, kid.position + new Vector3(0f, 1.4f, 0f), 2.5f, 0.3f);
        Toast("Your friend heads off to class.", 2.5f);

        Vector3 door = DoorPoint();
        while (Vector2.Distance(kid.position, door) > 0.1f && Vector2.Distance(kid.position, classroom.position) >= arriveRange)
        {
            StepKid(door, kidSpeed, false);
            yield return null;
        }
        yield return Arrive("Your friend read the signs and found class!");
    }

    // ── Direct: point out the way, step by step ──────────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("You point the way, step by step...", 3f);
        SetMode(Mode.Calm);
        ShowBar(true);
        Vector3 start = kid.position, door = DoorPoint();
        int steps = 7;
        float per = directTime / steps, t = 0f;

        for (int i = 0; i < steps; i++)
        {
            Vector3 p = Vector3.Lerp(start, door, (i + 1f) / steps);
            var go = new GameObject("Chevron");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(p + new Vector3(0f, -0.05f, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = chevron; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 3;
            _spawned.Add(go);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.45f, 0.1f);

            float s = 0f;
            Vector3 from = kid.position;
            while (s < per)
            {
                s += Time.deltaTime; t += Time.deltaTime;
                float k = Mathf.Clamp01(s / per);
                Vector3 next = Vector3.Lerp(from, p, k);
                if (Mathf.Abs(next.x - kid.position.x) > 0.001f) _kidSr.flipX = next.x < kid.position.x;
                SetKidPos(next);
                SetSprite(walkFrames, (int)(Time.time * 8f) % 4);
                SetBar(t / directTime);
                MoveBar(KidBarPos());
                yield return null;
            }
        }
        ShowBar(false);
        yield return Arrive("You pointed the way and your friend made it to class!");
    }

    // ── Call: the teacher comes and takes them to class ──────────────────

    private IEnumerator TeacherRoutine()
    {
        Toast("You call the teacher over...", 3f);
        ShowBar(true);
        Vector3 door = DoorPoint();
        Vector3 from = door + new Vector3(0f, 0.4f, 0f);
        Vector3 meet = kid.position + new Vector3(0.8f, 0f, 0f);
        SpawnHelper(from, 7);
        float total = teacherWalkTime + teacherMeetTime + teacherBackTime, t = 0f;

        yield return HelperWalk(from, meet, teacherWalkTime, e => { SetBar(e / total); MoveBar(KidBarPos()); });
        t = teacherWalkTime;

        // they meet: the kid cheers up
        Toast("The teacher found your friend!", teacherMeetTime + 1f);
        SetMode(Mode.Calm);
        SetSprite(happyFrames, 0);
        SunodGame.Core.vc_Sfx.Play("chime", 0.7f);
        Fx(heartFrames, kid.position + new Vector3(0f, 1.3f, 0f), 3f, 0.35f);
        float m = 0f;
        while (m < teacherMeetTime)
        {
            m += Time.deltaTime;
            SetBar((t + m) / total);
            yield return null;
        }
        t += teacherMeetTime;

        // hand in hand back to class
        Vector3 kidFrom = kid.position, teacherFrom = meet;
        Vector3 kidTo = door + new Vector3(-0.4f, 0f, 0f), teacherTo = door + new Vector3(0.4f, 0.1f, 0f);
        float b = 0f;
        while (b < teacherBackTime)
        {
            b += Time.deltaTime;
            float k = Mathf.Clamp01(b / teacherBackTime);
            Vector3 kn = Vector3.Lerp(kidFrom, kidTo, k);
            _kidSr.flipX = kn.x < kid.position.x ? true : _kidSr.flipX;
            SetKidPos(kn);
            SetSprite(holdFrames, (int)(Time.time * 8f) % 4);
            MoveHelper(Vector3.Lerp(teacherFrom, teacherTo, k));
            SetBar((t + b) / total);
            MoveBar(KidBarPos());
            yield return null;
        }
        ShowBar(false);
        yield return Arrive("The teacher took your friend to class. Thanks for calling!");
    }

    private void MoveHelper(Vector3 pos)
    {
        Transform h = transform.Find("Helper");
        if (h == null) return;
        if (helperWalk != null && helperWalk.Length > 0)
            h.GetComponent<SpriteRenderer>().sprite = helperWalk[(int)(Time.time * 8f) % helperWalk.Length];
        h.position = Snap(pos);
    }
}
