using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E quest (E3). Get the cranky guest checked in and to their room. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   command  E  best fit     staff takes the guest up (bell ding)                      ~2s
///   persuade E  workaround   talk the guest through a smooth check-in                  3s
///   charm    E  workaround   the cranky guest smiles and goes up by themselves        3s
///   summon   E  workaround   the bellhop and luggage cart roll in and take the bags    4s
///   map      E  workaround   plan the route; the guest follows it to the room         4s
///   direct   C  off-letter   tidy the messy bookings first, then check in             8s
///   guide    S  off-letter   walk the guest to the room yourself                       8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (command) takes over from wherever the guest is.
/// </summary>
[DisallowMultipleComponent]
public class vc_HotelCheckInQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer guest;
    [SerializeField] private Sprite[] guestWalk;
    [SerializeField] private Transform desk;
    [SerializeField] private Transform room;
    [SerializeField] private Sprite cartSprite;
    [SerializeField] private Vector3 cartScale = new Vector3(1.4f, 1.4f, 1f);

    [Header("Art")]
    [SerializeField] private Sprite bubbleDots;
    [SerializeField] private Sprite bubbleAngry;
    [SerializeField] private Sprite bubbleSmile;
    [SerializeField] private Sprite keycard;
    [SerializeField] private Sprite suitcase;
    [SerializeField] private Sprite routeDot;
    [SerializeField] private Sprite[] papers;

    [Header("Effects")]
    [SerializeField] private Sprite[] bellFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float commandWalk = 1.5f;
    [SerializeField] private float persuadeTime = 3f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float callTime = 4f;
    [SerializeField] private float mapTime = 4f;
    [SerializeField] private float directTime = 8f;
    [SerializeField] private float guideTime = 8f;

    private Vector3 _guestStart;
    private Vector3[] _path;
    private float[] _segLen;
    private float _pathLen;
    private float _frac;                 // how far along the path the guest is (0..1)
    private float _deskFrac;             // where the desk stop sits on the path
    private float _angryClock;
    private float _elapsed, _total = 1f;   // one monotonic bar for the whole fix
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (desk != null ? desk.position : transform.position) + new Vector3(-0.5f, 2.1f, 0f);

    private void Awake()
    {
        if (guest != null) _guestStart = guest.transform.position;
    }

    protected override void OnBegin()
    {
        CleanSpawned();
        if (guest == null || desk == null || room == null) return;

        Vector3 deskFront = desk.position + new Vector3(0.3f, -1.3f, 0f);
        _path = new[]
        {
            _guestStart,
            deskFront,
            new Vector3(room.position.x - 0.2f, deskFront.y + 0.1f, 0f),
            room.position,
        };
        _segLen = new float[_path.Length - 1];
        _pathLen = 0f;
        for (int i = 0; i < _segLen.Length; i++) { _segLen[i] = Vector3.Distance(_path[i], _path[i + 1]); _pathLen += _segLen[i]; }
        _deskFrac = _segLen[0] / _pathLen;

        _frac = 0f;
        guest.gameObject.SetActive(true);
        guest.color = Color.white;
        guest.flipX = false;
        guest.transform.position = _guestStart;
        if (guestWalk != null && guestWalk.Length > 0) guest.sprite = guestWalk[0];
        _angryClock = 0.8f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Hotel Check-In",
            "A cranky guest is waiting at the lobby. Get them checked in and up to their room.",
            new[] { "Check in the guest" });
    }

    // ── Idle life: the guest grumbles and taps a foot ────────────────────

    private void Update()
    {
        if (!Started || Done || Working || guest == null) return;
        _angryClock -= Time.deltaTime;
        if (_angryClock <= 0f)
        {
            _angryClock = 2.6f;
            Fx(new[] { bubbleAngry }, guest.transform.position + new Vector3(0f, 1.1f, 0f), 1.4f, 0.15f, 26);
            StartCoroutine(Tap());
        }
    }

    private IEnumerator Tap()
    {
        Vector3 rest = guest.transform.position;
        for (int i = 0; i < 3; i++)
        {
            if (guest == null || Working) yield break;
            guest.transform.position = rest + new Vector3(0f, 1f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            guest.transform.position = rest;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (d.HasTag("charm")) { StartWork(CharmRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine()); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }

        Toast("Running a hotel needs a leader.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        CleanSpawned();
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

    private void CleanSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    // ── Command: the staff take the guest up ─────────────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You send your staff over...", 2f);
        float cw = Mathf.Max(0.6f, commandWalk * (1f - _frac));
        Timeline(0.5f + cw);
        Ding();

        Vector3 g = guest.transform.position;
        Vector3 from = (desk != null ? desk.position : g) + new Vector3(1.8f, -1.2f, 0f);
        SpawnHelper(from, 7);
        float last = 0f;
        yield return HelperWalk(from, g + new Vector3(0.9f, 0f, 0f), 0.5f, t => { Tick(t - last); last = t; });

        Fx(new[] { bubbleSmile }, g + new Vector3(0f, 1.1f, 0f), 1.4f, 0.15f, 26);
        yield return WalkGuest(1f, cw, 0.04f);
        yield return EnterRoom();
        Finish("Your staff takes the guest to their room!");
    }

    // ── Persuade: talk them through the check-in ─────────────────────────

    private IEnumerator PersuadeRoutine()
    {
        Toast("You talk the guest through check-in...", persuadeTime);
        Timeline(persuadeTime);
        Vector3 g = guest.transform.position;
        Fx(new[] { bubbleDots }, g + new Vector3(0f, 1.1f, 0f), 2f, 0.1f, 26);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        yield return Wait(0.5f);
        Fx(new[] { bubbleSmile }, g + new Vector3(0f, 1.1f, 0f), 2f, 0.1f, 26);

        yield return WalkGuest(_deskFrac, 0.8f, 0f);
        Ding();
        yield return CardToGuest(0.5f);
        yield return WalkGuest(1f, persuadeTime - 1.8f, 0f);
        yield return EnterRoom();
        Finish("You talked the guest through a smooth check-in!");
    }

    // ── Charm: the cranky guest smiles ───────────────────────────────────

    private IEnumerator CharmRoutine()
    {
        Toast("You flash your best smile...", charmTime);
        Timeline(charmTime);
        Vector3 g = guest.transform.position;
        Fx(new[] { bubbleAngry }, g + new Vector3(0f, 1.1f, 0f), 3f, 0f, 26);
        yield return Wait(0.35f);
        Fx(sparkFrames, g + new Vector3(0f, 0.9f, 0f), 10f, 0.2f, 27);
        Fx(new[] { bubbleSmile }, g + new Vector3(0f, 1.1f, 0f), 1.5f, 0.2f, 26);
        SunodGame.Core.vc_Sfx.Play("idea", 0.5f);
        yield return Wait(0.5f);

        yield return WalkGuest(1f, charmTime - 0.85f, 0f, () => { Ding(); Fx(sparkFrames, guest.transform.position + new Vector3(0f, 0.9f, 0f), 10f, 0.2f, 27); });
        yield return EnterRoom();
        Finish("The cranky guest smiled and went up on their own!");
    }

    // ── Call: bellhop and luggage cart roll in ───────────────────────────

    private IEnumerator CallRoutine()
    {
        Toast("You call the bellhop...", 2.5f);
        Timeline(callTime);
        Ding();

        Vector3 g = guest.transform.position;
        Vector3 from = g + new Vector3(-7f, 0f, 0f);
        SpawnHelper(from, 7);
        GameObject cart = Spawn("Cart", cartSprite, 6);
        cart.transform.localScale = cartScale;
        float walk = callTime * 0.38f, callLast = 0f;
        yield return HelperWalk(from, g + new Vector3(-0.9f, 0f, 0f), walk, t =>
        {
            Tick(t - callLast); callLast = t;
            if (cart != null) cart.transform.position = Snap(Vector3.Lerp(from, g + new Vector3(-0.9f, 0f, 0f), t / walk) + new Vector3(0.8f, 0.45f, 0f));
        });

        // load the bags
        GameObject bag = Spawn("Suitcase", suitcase, 7);
        bag.transform.position = Snap(g + new Vector3(0.5f, -0.5f, 0f));
        SunodGame.Core.vc_Sfx.Play("clack", 0.6f);
        Fx(sparkFrames, g + new Vector3(0.5f, 0f, 0f), 10f, 0.2f, 27);
        yield return Wait(callTime * 0.12f);

        float secs = Mathf.Max(0.6f, callTime * 0.5f * (1f - _frac));
        yield return WalkGuest(1f, secs, 0.04f, null, bag);
        yield return EnterRoom();
        Finish("The bellhop carried the bags up. The guest is settled in!");
    }

    // ── Map: route dots, then the guest follows them ─────────────────────

    private IEnumerator MapRoutine()
    {
        Toast("Planning the route to the room...", mapTime);
        Timeline(mapTime);
        int dots = 14;
        float lay = mapTime * 0.45f, per = lay / dots;
        var list = new List<GameObject>();
        for (int i = 0; i < dots; i++)
        {
            float k = (i + 1f) / dots;
            GameObject dot = Spawn("RouteDot", routeDot, 4);
            dot.transform.position = Snap(PathAt(k * 0.97f));
            list.Add(dot);
            if (i % 3 == 0) SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            float s = 0f;
            while (s < per) { s += Time.deltaTime; Tick(Time.deltaTime); yield return null; }
        }
        Fx(checkFrames, room.position + new Vector3(0f, 1f, 0f), 3f, 0.3f, 27);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);

        float walk = mapTime - lay;
        float f0 = _frac, w = 0f;
        while (w < walk)
        {
            w += Time.deltaTime; Tick(Time.deltaTime);
            SetFrac(Mathf.Lerp(f0, 1f, w / walk));
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && (i + 1f) / dots * 0.97f <= _frac) Destroy(list[i]);
            yield return null;
        }
        yield return EnterRoom();
        Finish("You planned the route. The guest is settled in!");
    }

    // ── Direct: tidy the bookings, then check in ─────────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("Sorting out the messy bookings...", directTime);
        Timeline(directTime);
        Vector3 d = desk.position;
        int n = Mathf.Max(3, papers != null ? papers.Length * 2 : 6);
        var sheets = new List<Transform>();
        for (int i = 0; i < n; i++)
        {
            GameObject p = Spawn("Booking", papers[i % papers.Length], 8);
            p.transform.position = Snap(d + new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(0.35f, 1.0f), 0f));
            sheets.Add(p.transform);
            SunodGame.Core.vc_Sfx.Play("shuffle", 0.25f, 0.2f);
        }

        float tidy = directTime * 0.6f, per = tidy / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 from = sheets[i].position;
            Vector3 to = Snap(d + new Vector3(-0.75f + i * (1.5f / (n - 1)), 0.75f, 0f));
            float s = 0f;
            while (s < per)
            {
                s += Time.deltaTime; Tick(Time.deltaTime);
                float k = Mathf.Clamp01(s / (per * 0.6f));
                if (sheets[i] != null) sheets[i].position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.25f, 0f));
                yield return null;
            }
            if (sheets[i] != null) sheets[i].position = to;
            SunodGame.Core.vc_Sfx.Play("clack", 0.35f, 0.1f);
        }
        Fx(checkFrames, d + new Vector3(0f, 1.4f, 0f), 3f, 0.3f, 27);
        Toast("Bookings sorted. Checking the guest in...", directTime - tidy);

        float walkTime = directTime - tidy;
        yield return WalkGuest(_deskFrac, walkTime * 0.4f, 0f);
        Ding();
        yield return CardToGuest(0.4f);
        yield return WalkGuest(1f, walkTime * 0.6f - 0.4f, 0f);
        yield return EnterRoom();
        Finish("You sorted the bookings and checked the guest in.");
    }

    // ── Guide: walk the guest to the room yourself ───────────────────────

    private IEnumerator GuideRoutine()
    {
        Toast("You walk the guest to their room...", guideTime);
        Timeline(guideTime);
        float chunk = guideTime / 4f;
        for (int i = 0; i < 4; i++)
        {
            Fx(new[] { i == 3 ? bubbleSmile : bubbleDots }, guest.transform.position + new Vector3(0f, 1.1f, 0f), 1.6f, 0.15f, 26);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            yield return WalkGuest((i + 1) / 4f, chunk, 0f);
        }
        yield return EnterRoom();
        Finish("You walked the guest to their room. They're grateful!");
    }

    // ── Movement helpers ─────────────────────────────────────────────────

    private Vector3 PathAt(float frac)
    {
        float d = Mathf.Clamp01(frac) * _pathLen;
        for (int i = 0; i < _segLen.Length; i++)
        {
            if (d <= _segLen[i] || i == _segLen.Length - 1)
                return Vector3.Lerp(_path[i], _path[i + 1], _segLen[i] > 0f ? Mathf.Clamp01(d / _segLen[i]) : 1f);
            d -= _segLen[i];
        }
        return _path[_path.Length - 1];
    }

    private void SetFrac(float f)
    {
        Vector3 before = guest.transform.position;
        _frac = Mathf.Clamp01(f);
        guest.transform.position = Snap(PathAt(_frac));
        Vector3 delta = guest.transform.position - before;
        if (Mathf.Abs(delta.x) > 0.0005f) guest.flipX = delta.x < 0f;
        if (guestWalk != null && guestWalk.Length > 0 && delta.sqrMagnitude > 0f)
            guest.sprite = guestWalk[(int)(Time.time * 8f) % guestWalk.Length];
    }

    /// <summary>Walks the guest to 'toFrac' on the path in 'seconds'; the helper (if any) leads by 'helperLead' of the path.</summary>
    private IEnumerator WalkGuest(float toFrac, float seconds, float helperLead, System.Action onMid = null, GameObject carried = null)
    {
        float f0 = _frac, t = 0f;
        bool fired = false;
        seconds = Mathf.Max(0.05f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetFrac(Mathf.Lerp(f0, toFrac, t / seconds));
            if (!fired && onMid != null && t >= seconds * 0.5f) { fired = true; onMid(); }
            if (carried != null) carried.transform.position = Snap(guest.transform.position + new Vector3(guest.flipX ? 0.5f : -0.5f, -0.5f, 0f));
            MoveHelperAlong(helperLead);
            Tick(Time.deltaTime);
            yield return null;
        }
        if (guestWalk != null && guestWalk.Length > 0) guest.sprite = guestWalk[0];
    }

    private void MoveHelperAlong(float lead)
    {
        if (lead <= 0f) return;
        Transform h = transform.Find("Helper");
        if (h == null) return;
        h.position = Snap(PathAt(_frac + lead) + new Vector3(0.1f, 0f, 0f));
        var sr = h.GetComponent<SpriteRenderer>();
        if (sr != null && helperWalk != null && helperWalk.Length > 0) sr.sprite = helperWalk[(int)(Time.time * 8f) % helperWalk.Length];
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            Tick(Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator CardToGuest(float seconds)
    {
        GameObject card = Spawn("Keycard", keycard, 27);
        Vector3 a = desk.position + new Vector3(0.2f, 0.9f, 0f), b = guest.transform.position + new Vector3(0f, 0.6f, 0f);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            Tick(Time.deltaTime);
            float k = Mathf.Clamp01(t / seconds);
            if (card != null) card.transform.position = Snap(Vector3.Lerp(a, b, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.5f, 0f));
            yield return null;
        }
        if (card != null) Destroy(card);
        Fx(sparkFrames, b, 10f, 0.2f, 27);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
    }

    private IEnumerator EnterRoom()
    {
        SunodGame.Core.vc_Sfx.Play("door_open", 0.7f);
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            if (guest != null) guest.color = new Color(1f, 1f, 1f, 1f - t / 0.3f);
            yield return null;
        }
        if (guest != null) guest.gameObject.SetActive(false);
    }

    private void Ding()
    {
        Vector3 p = (desk != null ? desk.position : transform.position) + new Vector3(0.75f, 1.05f, 0f);
        Fx(bellFrames, p, 6f, 0.1f, 27);
        SunodGame.Core.vc_Sfx.Play("bell", 0.7f);
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
}
