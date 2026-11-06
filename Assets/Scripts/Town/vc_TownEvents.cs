using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The town's news (TASKS §3.1): rare town events, morning letters and the plaza bulletin
/// board. Added to the town root by vc_TownStage, needs no scene setup. Content is in
/// vc_TownEventData; art in Resources/TownEvents (Art/Town/Tools/make_events.py).
///
///   Town events: about one stage in two, on day 2, 3 or 4. Someone has a gold-and-violet
///   sparkle over their head: in front of a house (a Visit) or on a small stage at the plaza
///   (a Gathering). Nothing points the way; the bulletin board only hints at the part of town.
///   Walk up and Join: a short letterboxed scene with a little crowd, then +2 XP to every
///   skill you own of that kind and +3 bonus stars. Doesn't cost energy. Missed = gone.
///   Fiesta: at most once a loop (about one loop in three): banderitas go up over the plaza
///   and the town square fills with confetti. Instant: +1 XP to every skill you own, +5 stars.
///   Morning letters: about one morning in three (from day 2), an envelope waits by your door:
///   a small gift (+2 stars), a tip in someone's voice, or just a message.
///   Bulletin board: today's notices, a vague rumor when an event is on, a heads-up for fiesta.
///
/// Everything is rolled from a seed (player, loop, threat, stage, day), so Continue shows the
/// same day, and what was done is saved as progress markers (ev:, lt:, bs: for stars).
/// Nothing here touches quests, the timer or scoring.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownEvents : MonoBehaviour
{
    public const float EventChancePerStage = 0.5f;
    public const float FiestaChancePerLoop = 0.35f;
    public const float LetterChancePerMorning = 0.3f;

    /// <summary>Debug menu / tests: force an event id (or "fiesta") onto the current day.</summary>
    public static string DebugForce;

    private const float Ppu = 16f;
    private const float WalkUpRange = 1.25f;

    public static vc_TownEvents Current { get; private set; }

    private vc_TownStage _stage;
    private vc_TownGrid _grid;
    private Rect _rect;
    private Transform _root;
    private string _layer = "Default";
    private int _order = 40;
    private Vector3 _plazaCenter;
    private bool _hasPlaza;
    private int _day = -1;
    private bool _ready;

    private Sprite[] _markFrames, _buntingFrames, _confetti;
    private Sprite _stageSprite, _poleSprite, _boardSprite, _boardNewSprite, _letterSprite;

    // today
    private vc_TownEventData.Event _event;
    private GameObject _host, _stageProp;
    private SpriteRenderer _hostBody, _mark;
    private Sprite[] _hostIdle;
    private string _hostName;
    private Vector3 _hostPos;
    private bool _nearHostPrompted, _running;
    private readonly List<GameObject> _crowd = new List<GameObject>();
    private readonly List<(SpriteRenderer sr, Sprite[] frames, int offset)> _crowdAnim = new List<(SpriteRenderer, Sprite[], int)>();
    private readonly List<GameObject> _decor = new List<GameObject>();
    private readonly List<SpriteRenderer> _bunting = new List<SpriteRenderer>();
    private bool _fiesta;
    private float _nextConfetti;

    private GameObject _board;
    private SpriteRenderer _boardNew;
    private bool _boardReadToday;

    private GameObject _letterGo;
    private vc_TownEventData.Letter _letter;

    public bool Running => _running;

    // ── setup ─────────────────────────────────────────────────────────────

    private void Awake() => Current = this;

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Setup()
    {
        if (_ready) return;
        _ready = true;
        _stage = GetComponent<vc_TownStage>();
        _grid = vc_TownGrid.For(transform);
        _rect = vc_TownGrid.TownRect(transform);
        foreach (vc_TownNPC npc in GetComponentsInChildren<vc_TownNPC>(true))
            if (npc.body != null) { _layer = npc.body.sortingLayerName; _order = npc.body.sortingOrder; break; }

        _root = new GameObject("TownEvents").transform;
        _root.SetParent(transform, false);

        _markFrames = new[] { Load("event_mark_0", 0.5f), Load("event_mark_1", 0.5f) };
        _buntingFrames = new[] { Load("bunting_0", 1f), Load("bunting_1", 1f) };
        _confetti = new[] { Load("confetti_0", 0.5f), Load("confetti_1", 0.5f), Load("confetti_2", 0.5f), Load("confetti_3", 0.5f) };
        _stageSprite = Load("stage", 0f);
        _poleSprite = Load("pole", 0f);
        _boardSprite = Load("board", 0f);
        _boardNewSprite = Load("board_new", 0.5f);
        _letterSprite = Load("letter", 0f);

        Tilemap plaza = transform.Find("Grid")?.Find("plaza")?.GetComponent<Tilemap>();
        if (plaza != null)
        {
            plaza.CompressBounds();
            _plazaCenter = plaza.CellToWorld(Vector3Int.RoundToInt(plaza.cellBounds.center)) + plaza.cellSize * 0.5f;
            _plazaCenter.z = 0f;
            _hasPlaza = plaza.GetUsedTilesCount() > 0;
        }
        if (!_hasPlaza) _plazaCenter = new Vector3(_rect.center.x, _rect.center.y, 0f);

        PlaceBoard();
    }

    private static Sprite Load(string name, float pivotY)
    {
        var tex = Resources.Load<Texture2D>("TownEvents/" + name);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, pivotY), Ppu);
    }

    private SpriteRenderer MakeSprite(string name, Sprite s, Vector3 pos, int orderOffset, Transform parent = null)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent != null ? parent : _root, false);
        sr.transform.position = Snap(pos);
        sr.sprite = s;
        sr.sortingLayerName = _layer;
        sr.sortingOrder = _order + orderOffset;
        return sr;
    }

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);

    // ── the schedule (seeded: Continue shows the same day) ────────────────

    private struct Plan
    {
        public vc_TownEventData.Event ev;
        public int evDay;
        public bool fiesta;
        public int fiestaDay;
    }

    private static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return h;
    }

    private static string Salt
    {
        get
        {
            var s = SunodGame.Core.SessionState.Instance;
            string user = s != null && !string.IsNullOrWhiteSpace(s.Username) ? s.Username : "guest";
            return $"{user}|{vc_LoopMeta.Loop}|{vc_LoopMeta.Threat}";
        }
    }

    private static System.Random Rng(string key) => new System.Random((int)(Hash(Salt + "|" + key) & 0x7fffffff));

    private Plan PlanFor(int stageIndex)
    {
        var plan = new Plan();
        // one loop: the six events in a shuffled order (no repeats in a run), maybe one fiesta
        System.Random loop = Rng("loop");
        int[] order = { 0, 1, 2, 3, 4, 5 };
        for (int i = order.Length - 1; i > 0; i--) { int j = loop.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        bool fiestaLoop = loop.NextDouble() < FiestaChancePerLoop;
        int fiestaStage = loop.Next(3);
        plan.fiestaDay = 2 + loop.Next(2);
        plan.fiesta = fiestaLoop && fiestaStage == stageIndex;

        System.Random stage = Rng("stage" + stageIndex);
        bool has = stage.NextDouble() < EventChancePerStage;
        plan.evDay = 2 + stage.Next(3);
        if (plan.fiesta && plan.evDay == plan.fiestaDay) plan.evDay = plan.fiestaDay + 1;
        plan.ev = has ? vc_TownEventData.Events[order[Mathf.Clamp(stageIndex, 0, 5)]] : null;

        // debug: today gets the forced event / fiesta
        if (!string.IsNullOrEmpty(DebugForce))
        {
            if (DebugForce == "fiesta") { plan.fiesta = true; plan.fiestaDay = _day; }
            else { var ev = vc_TownEventData.ById(DebugForce); if (ev != null) { plan.ev = ev; plan.evDay = _day; } }
        }
        return plan;
    }

    private string StageId => _stage != null ? _stage.stageId : "town";
    private string EventMarker(vc_TownEventData.Event ev) => $"ev:{StageId}:{ev.id}";
    private string FiestaMarker => $"ev:{StageId}:fiesta";
    private string LetterMarker(int day) => $"lt:{StageId}:{day}";
    private string SceneName => gameObject.scene.name;

    // ── a new morning ─────────────────────────────────────────────────────

    /// <summary>Called by vc_TownStage at the start of every day (and on Continue).</summary>
    public void OnDayStart(int day)
    {
        Setup();
        _day = day;
        ClearToday();
        Plan plan = PlanFor(vc_SkillLevels.StageIndex);

        if (plan.fiesta && day == plan.fiestaDay)
        {
            Decorate();
            if (!vc_TownFlow.IsQuestDone(FiestaMarker)) StartCoroutine(FiestaMorning());
        }

        if (plan.ev != null && day == plan.evDay && !vc_TownFlow.IsQuestDone(EventMarker(plan.ev)))
            SpawnEvent(plan.ev);

        RollLetter(day);

        _boardReadToday = false;
        bool news = (_event != null) || (plan.fiesta && day <= plan.fiestaDay);
        if (_boardNew != null) _boardNew.enabled = news;
    }

    private void ClearToday()
    {
        foreach (GameObject g in _crowd) if (g != null) Destroy(g);
        _crowd.Clear();
        _crowdAnim.Clear();
        foreach (GameObject g in _decor) if (g != null) Destroy(g);
        _decor.Clear();
        _bunting.Clear();
        if (_host != null) Destroy(_host);
        if (_stageProp != null) Destroy(_stageProp);
        if (_letterGo != null) Destroy(_letterGo);
        _host = _stageProp = _letterGo = null;
        _event = null;
        _letter = null;
        _fiesta = false;
        _nearHostPrompted = false;
    }

    // ── town events ───────────────────────────────────────────────────────

    private void SpawnEvent(vc_TownEventData.Event ev)
    {
        int st = Mathf.Clamp(vc_SkillLevels.StageIndex, 0, 2);
        _event = ev;
        _hostName = ev.hosts[st];
        string look = ev.looks[st];

        if (ev.kind == vc_TownEventData.Kind.Gathering)
        {
            Vector3 stagePos = StageSpot();
            _stageProp = MakeSprite("EventStage", _stageSprite, stagePos, -5).gameObject;
            _hostPos = stagePos + new Vector3(0f, 0.55f, 0f);
        }
        else
        {
            _hostPos = PickDoorSpot();
        }

        _host = new GameObject("EventHost");
        _host.transform.SetParent(_root, false);
        _host.transform.position = Snap(_hostPos);
        _hostIdle = vc_NpcSprites.Idle(look, vc_NpcSprites.Dir.Down);
        _hostBody = MakeSprite("Body", _hostIdle != null && _hostIdle.Length > 0 ? _hostIdle[0] : null, _hostPos, ev.kind == vc_TownEventData.Kind.Gathering ? 2 : 0, _host.transform);
        // drawn over everything in town (signs, roofs), so it can always be spotted
        _mark = MakeSprite("EventMark", _markFrames[0], _hostPos + new Vector3(0f, 1.6f, 0f), 400, _host.transform);
    }

    /// <summary>A clear patch of the plaza for the stage (no bench, lamp or building under it).</summary>
    private Vector3 StageSpot()
    {
        Vector3? planned = PlannedSpot("stage");
        if (planned.HasValue) return planned.Value;
        Vector2[] tries =
        {
            new Vector2(0f, 1.6f), new Vector2(-1.2f, 1.6f), new Vector2(1.2f, 1.6f), new Vector2(0f, 0.6f),
            new Vector2(-1.8f, 0.6f), new Vector2(1.8f, 0.6f), new Vector2(0f, -0.6f), new Vector2(0f, 2.4f),
        };
        var hits = new Collider2D[4];
        var filter = new ContactFilter2D { useTriggers = false };
        foreach (Vector2 t in tries)
        {
            Vector3 p = _plazaCenter + (Vector3)t;
            // the stage is 3 x 1.25 units; leave room in front for the crowd
            if (Physics2D.OverlapBox((Vector2)p + new Vector2(0f, 0.6f), new Vector2(3.4f, 1.4f), 0f, filter, hits) > 0) continue;
            if (_grid != null && (!_grid.IsWalkable(p + new Vector3(0f, -1.6f, 0f)) || !_grid.IsWalkable(p + new Vector3(-1.2f, 0.3f, 0f)) || !_grid.IsWalkable(p + new Vector3(1.2f, 0.3f, 0f)))) continue;
            return p;
        }
        return SafeSpot(_plazaCenter + new Vector3(0f, 1.6f, 0f));
    }

    /// <summary>In front of a building's door, away from home, requesters and the board.</summary>
    private Vector3 PickDoorSpot()
    {
        var spots = new List<Vector3>();
        Transform objects = transform.Find("Objects");
        var avoid = new List<Vector3>();
        foreach (vc_TownNPC n in GetComponentsInChildren<vc_TownNPC>(true)) avoid.Add(n.HomePosition);
        foreach (vc_TownHome h in GetComponentsInChildren<vc_TownHome>(true)) avoid.Add(h.DoorPosition);
        if (_board != null) avoid.Add(_board.transform.position);

        if (objects != null)
            foreach (Transform o in objects)
            {
                Transform baseT = o.Find("Base");
                SpriteRenderer b = baseT != null ? baseT.GetComponent<SpriteRenderer>() : o.GetComponent<SpriteRenderer>();
                if (b == null || b.sprite == null || b.bounds.size.x <= 2f) continue;
                string n = o.name.ToLowerInvariant();
                if (n.Contains("sign") || n.Contains("bench") || n.Contains("lamp") || n.Contains("fence") || n.Contains("garden")) continue;   // a house or shop, not a prop
                Vector3 spot = new Vector3(b.bounds.center.x, b.bounds.min.y - 0.7f, 0f);
                Vector2? ok = _grid != null ? _grid.NearestWalkable(spot, 2) : (Vector2?)spot;
                if (!ok.HasValue) continue;
                Vector3 p = ok.Value;
                bool clear = true;
                foreach (Vector3 a in avoid) if (Vector2.Distance(a, p) < 2.2f) { clear = false; break; }
                if (clear) spots.Add(p);
            }

        if (spots.Count == 0) return SafeSpot(_plazaCenter + new Vector3(2.5f, -1f, 0f));
        System.Random rng = Rng($"spot{StageId}:{_day}");
        return spots[rng.Next(spots.Count)];
    }

    private Vector3 SafeSpot(Vector3 want)
    {
        if (_grid == null || _grid.IsWalkable(want)) return want;
        Vector2? s = _grid.NearestWalkable(want, 12);
        return s.HasValue ? new Vector3(s.Value.x, s.Value.y, 0f) : want;
    }

    private string SideOfTown(Vector3 p)
    {
        Vector2 d = (Vector2)p - _rect.center;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0 ? "east" : "west";
        return d.y > 0 ? "north" : "south";
    }

    private void InviteToEvent()
    {
        if (_event == null || _running || vc_DialogPanel.Instance == null || vc_DialogPanel.Instance.IsOpen) return;
        SunodGame.Core.vc_Sfx.Play("bubble", 0.45f, 0.08f);
        vc_DialogPanel.Instance.ShowChoice(
            _hostName,
            _event.invite + "\n\n(Town event. Doesn't use energy.)",
            null, "Join", "Later",
            () => StartCoroutine(RunEvent()),
            () => vc_QuestHUD.Instance?.ShowFeedbackTimed("It's only today. Don't miss it!", 2.5f));
    }

    private IEnumerator RunEvent()
    {
        vc_TownEventData.Event ev = _event;
        if (ev == null || _running) yield break;
        _running = true;
        bool gathering = ev.kind == vc_TownEventData.Kind.Gathering;
        _stage?.SetBusy(true);
        PlayerController player = FindFirstObjectByType<PlayerController>();
        player?.MoveAction.Disable();
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        ui?.SetCinematic(true);
        if (ui != null) yield return ui.FadeScreen(1f, 0.35f);

        // set the scene: you in front, a few neighbors around
        // the dialog docks at the bottom: you, the crowd and the host stay in view above it
        vc_DialogPanel.Instance?.SetDockedBottom(true);
        Vector3 front = SafeSpot(_hostPos + new Vector3(0f, gathering ? -2.4f : -1.5f, 0f));
        MovePlayer(player, front);
        if (player != null) player.ScriptedMove = new Vector2(0f, 0.001f);   // face the host
        SpawnCrowd(gathering ? 7 : 3, front, gathering);
        if (_mark != null) _mark.enabled = false;
        if (ui != null) yield return ui.FadeScreen(0f, 0.45f);
        if (player != null) { player.ScriptedMove = null; player.MoveAction.Disable(); }

        foreach (string raw in ev.lines)
        {
            bool narration = raw.StartsWith("* ");
            string body = narration ? raw.Substring(2) : raw;
            if (!narration) HopHost();
            CrowdEmote(narration ? "dots" : (UnityEngine.Random.value < 0.5f ? "note" : "sun"));
            bool closed = false;
            vc_DialogPanel.Instance?.ShowMessage(narration ? ev.title : _hostName, body, () => closed = true);
            while (!closed && vc_DialogPanel.Instance != null) yield return null;
            player?.MoveAction.Disable();   // closing a dialog gives movement back; not yet
            yield return new WaitForSeconds(0.15f);
        }

        // applause, and what you learned
        CrowdEmote("heart");
        SunodGame.Core.vc_Sfx.Play("cheer", 0.7f);
        Burst(_hostPos + new Vector3(0f, 1.2f, 0f), 14);
        string reward = GrantEvent(ev);
        ui?.ShowStamp("Learned something!", new Color32(255, 196, 40, 255));
        bool done = false;
        vc_DialogPanel.Instance?.ShowMessage($"{ev.title} - done!", $"{ev.lesson}\n\n{reward}", () => done = true);
        while (!done && vc_DialogPanel.Instance != null) yield return null;
        player?.MoveAction.Disable();
        vc_DialogPanel.Instance?.SetDockedBottom(false);

        // the crowd goes home
        if (ui != null) yield return ui.FadeScreen(1f, 0.3f);
        foreach (GameObject g in _crowd) if (g != null) Destroy(g);
        _crowd.Clear();
        _crowdAnim.Clear();
        if (_host != null) Destroy(_host);
        if (_stageProp != null) Destroy(_stageProp);
        _host = _stageProp = null;
        _event = null;
        Plan after = PlanFor(vc_SkillLevels.StageIndex);
        if (_boardNew != null) _boardNew.enabled = !_boardReadToday && after.fiesta && _day <= after.fiestaDay;
        ui?.SetCinematic(false);
        if (ui != null) yield return ui.FadeScreen(0f, 0.4f);
        player?.MoveAction.Enable();
        _stage?.SetBusy(false);
        _running = false;
    }

    /// <summary>+2 XP to every owned skill of the event's kind, +3 bonus stars, saved.</summary>
    private string GrantEvent(vc_TownEventData.Event ev)
    {
        var names = new List<string>();
        if (vc_PlayerInventory.Instance != null)
            foreach (vc_SkillData s in vc_PlayerInventory.Instance.GatheredSkills)
                if (s != null && string.Equals(s.riaSecLetter?.Trim(), ev.letter, StringComparison.OrdinalIgnoreCase))
                    names.Add(s.DisplayName);
        vc_SkillLevels.AddCategoryXp(ev.letter, vc_TownEventData.EventXp);

        string marker = EventMarker(ev);
        vc_TownFlow.MarkQuestDone(marker);
        vc_QuestRoom.SaveProgressIds(new[] { marker }, SceneName);
        vc_BonusStars.Grant(marker, vc_TownEventData.EventStars, SceneName);

        string kind = vc_SkillManager.CategoryNameForSlot(vc_SkillManager.SlotIndexForLetter(ev.letter));
        string list = names.Count > 0 ? string.Join(", ", names) : kind + " skills";
        return $"{list}: +{vc_TownEventData.EventXp} XP\n+{vc_TownEventData.EventStars} stars";
    }

    private void SpawnCrowd(int count, Vector3 around, bool gathering)
    {
        foreach (GameObject g in _crowd) if (g != null) Destroy(g);
        _crowd.Clear();
        _crowdAnim.Clear();
        System.Random rng = Rng($"crowd{StageId}:{_day}");
        var looks = new List<string>(vc_NpcSprites.TownsfolkLooks);
        for (int i = 0; i < count; i++)
        {
            Vector3 p;
            if (gathering)
            {
                // a loose arc in front of the stage, the player in the middle
                // a loose arc between the stage and the player (who stands at the back, centered)
                float x = (i - (count - 1) * 0.5f) * 0.85f;
                p = _hostPos + new Vector3(x, -1.25f - Mathf.Abs(x) * 0.12f + (i % 2 == 0 ? 0f : -0.3f) + (float)rng.NextDouble() * 0.1f, 0f);
            }
            else
            {
                float[] xs = { -1.1f, 1.1f, 0.6f };
                p = _hostPos + new Vector3(xs[i % xs.Length], i == 2 ? -0.6f : -0.2f, 0f);
            }
            p = SafeSpot(p);
            string look = looks[rng.Next(looks.Count)];
            Vector2 face = gathering ? Vector2.up : ((Vector2)(_hostPos - p)).normalized;
            Sprite[] idle = vc_NpcSprites.Idle(look, vc_NpcSprites.DirFor(face));
            var go = new GameObject("Crowd");
            go.transform.SetParent(_root, false);
            go.transform.position = Snap(p);
            SpriteRenderer body = MakeSprite("Body", idle != null && idle.Length > 0 ? idle[0] : null, p, 0, go.transform);
            _crowdAnim.Add((body, idle, rng.Next(6)));
            _crowd.Add(go);
        }
    }

    private void CrowdEmote(string emote)
    {
        Sprite s = Resources.Load<Sprite>("TownLife/emote_" + emote);
        if (s == null) return;
        int i = 0;
        foreach (GameObject g in _crowd)
        {
            if (g == null || (i++ % 2 == 1 && emote != "heart")) continue;
            StartCoroutine(Bubble(s, g.transform.position + new Vector3(0f, 1.45f, 0f), 1.2f + UnityEngine.Random.value * 0.4f));
        }
    }

    private IEnumerator Bubble(Sprite s, Vector3 at, float seconds)
    {
        SpriteRenderer sr = MakeSprite("Emote", s, at, 35);
        float t = 0f;
        while (t < seconds && sr != null)
        {
            t += Time.deltaTime;
            sr.transform.position = Snap(at + new Vector3(0f, Mathf.Min(t, 0.3f) * 0.5f, 0f));
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    private void HopHost()
    {
        if (_hostBody != null) StartCoroutine(Hop(_hostBody.transform));
    }

    private static IEnumerator Hop(Transform t)
    {
        Vector3 rest = t.localPosition;
        for (int i = 0; i < 4 && t != null; i++)
        {
            t.localPosition = rest + new Vector3(0f, (i % 2 == 0 ? 2f : 1f) / Ppu, 0f);
            yield return new WaitForSeconds(0.06f);
        }
        if (t != null) t.localPosition = rest;
    }

    // ── fiesta ────────────────────────────────────────────────────────────

    /// <summary>Banderitas across the plaza, bamboo poles at the ends.</summary>
    private void Decorate()
    {
        _fiesta = true;
        _nextConfetti = Time.time + 1f;
        float halfW = 5f;
        float[] rows = { 2.6f, 0.2f, -2.2f };
        foreach (float dy in rows)
        {
            Vector3 left = _plazaCenter + new Vector3(-halfW, dy, 0f), right = _plazaCenter + new Vector3(halfW, dy, 0f);
            _decor.Add(MakeSprite("Pole", _poleSprite, left + new Vector3(-0.1f, -0.2f, 0f), -2).gameObject);
            _decor.Add(MakeSprite("Pole", _poleSprite, right + new Vector3(0.1f, -0.2f, 0f), -2).gameObject);
            int segments = Mathf.CeilToInt(halfW * 2f / 2f);
            for (int i = 0; i < segments; i++)
            {
                // 2 units per segment, high up between the poles (drawn over the town)
                Vector3 p = left + new Vector3(1f + i * 2f, 2.2f, 0f);
                SpriteRenderer b = MakeSprite("Bunting", _buntingFrames[i % 2], p, 60);
                _bunting.Add(b);
                _decor.Add(b.gameObject);
            }
        }
    }

    private IEnumerator FiestaMorning()
    {
        // after the wake-up fade and any morning message
        yield return new WaitForSeconds(1.6f);
        while ((_stage != null && (_stage.Busy || _stage.InQuest)) || (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen)
               || (vc_LoopMoments.Existing != null && vc_LoopMoments.Existing.IsShowing))
            yield return null;
        if (vc_TownFlow.IsQuestDone(FiestaMarker)) yield break;

        // apply first (saved), then tell
        if (vc_PlayerInventory.Instance != null)
            foreach (vc_SkillData s in new List<vc_SkillData>(vc_PlayerInventory.Instance.GatheredSkills))
                vc_SkillLevels.AddXp(s, vc_TownEventData.FiestaXp);
        vc_TownFlow.MarkQuestDone(FiestaMarker);
        vc_QuestRoom.SaveProgressIds(new[] { FiestaMarker }, SceneName);
        vc_BonusStars.Grant(FiestaMarker, vc_TownEventData.FiestaStars, SceneName);

        SunodGame.Core.vc_Sfx.Play("cheer", 0.8f);
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) Burst(player.transform.position + new Vector3(0f, 1.5f, 0f), 20);
        vc_RunProgressUI.Instance?.ShowStamp("Fiesta!", new Color32(240, 110, 150, 255));
        string line = vc_TownEventData.FiestaLines[Rng($"fiesta{StageId}").Next(vc_TownEventData.FiestaLines.Length)];
        vc_DialogPanel.Instance?.ShowMessage("Fiesta!",
            line + $"\n\nEveryone's in a good mood: all your skills +{vc_TownEventData.FiestaXp} XP, and +{vc_TownEventData.FiestaStars} stars.", null);
    }

    /// <summary>A handful of confetti bits that pop up and flutter down.</summary>
    private void Burst(Vector3 at, int count)
    {
        for (int i = 0; i < count; i++) StartCoroutine(ConfettiBit(at, true));
    }

    private IEnumerator ConfettiBit(Vector3 from, bool pop)
    {
        Sprite s = _confetti[UnityEngine.Random.Range(0, _confetti.Length)];
        if (s == null) yield break;
        SpriteRenderer sr = MakeSprite("Confetti", s, from, 70);
        Vector2 v = pop ? new Vector2(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(1.5f, 3.2f))
                        : new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), -0.6f);
        float life = UnityEngine.Random.Range(1.6f, 2.6f), t = 0f, sway = UnityEngine.Random.value * 6f;
        Vector3 p = from;
        while (t < life && sr != null)
        {
            t += Time.deltaTime;
            v.y = Mathf.Max(v.y - 4f * Time.deltaTime, -0.8f);
            p += new Vector3(v.x + Mathf.Sin(t * 5f + sway) * 0.4f, v.y, 0f) * Time.deltaTime;
            v.x *= 0.98f;
            sr.transform.position = Snap(p);
            if (t > life - 0.4f) sr.enabled = Mathf.Repeat(t, 0.16f) < 0.08f;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ── bulletin board ────────────────────────────────────────────────────

    /// <summary>A spot the town builder planned (Spots/&lt;name&gt;), if there is one.</summary>
    private Vector3? PlannedSpot(string name)
    {
        Transform spots = transform.Find("Spots");
        Transform s = spots != null ? spots.Find(name) : null;
        return s != null ? s.position : (Vector3?)null;
    }

    private void PlaceBoard()
    {
        Vector3 at = PlannedSpot("board") ?? SafeSpot(_plazaCenter + new Vector3(-4.2f, 2.4f, 0f));
        _board = MakeSprite("BulletinBoard", _boardSprite, at, 0).gameObject;
        _boardNew = MakeSprite("BoardNews", _boardNewSprite, at + new Vector3(0.55f, 1.75f, 0f), 2, _board.transform);
        _boardNew.enabled = false;
    }

    private void ReadBoard()
    {
        if (vc_DialogPanel.Instance == null || vc_DialogPanel.Instance.IsOpen) return;
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);
        Plan plan = PlanFor(vc_SkillLevels.StageIndex);
        var notes = new List<string>();

        if (_event != null)
            notes.Add(_event.rumor.Replace("{side}", SideOfTown(_hostPos)) + " (Today only.)");
        if (plan.fiesta && _day == plan.fiestaDay) notes.Add("FIESTA TODAY! Food at every house, music at the plaza. Enjoy!");
        else if (plan.fiesta && _day < plan.fiestaDay) notes.Add($"Fiesta is coming on Day {plan.fiestaDay}! Banderitas go up that morning.");

        int st = Mathf.Clamp(vc_SkillLevels.StageIndex, 0, 2);
        string[] pool = vc_TownEventData.Notices[st];
        System.Random rng = Rng($"board{StageId}:{_day}");
        int a = rng.Next(pool.Length), b = (a + 1 + rng.Next(pool.Length - 1)) % pool.Length;
        notes.Add(pool[a]);
        if (notes.Count < 3) notes.Add(pool[b]);
        if (vc_LoopMeta.IsRepeatLoop) notes.Add(vc_TownEventData.PrepNotices[rng.Next(vc_TownEventData.PrepNotices.Length)]);

        var sb = new System.Text.StringBuilder();
        foreach (string n in notes) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append("- ").Append(n); }
        // the board: today's request posters (accept them into the journal) + these notices
        vc_QuestBoard board = GetComponent<vc_QuestBoard>();
        if (board != null) board.OpenBoard(sb.ToString());
        else vc_DialogPanel.Instance.ShowMessage("Town Board", sb.ToString(), null);
        _boardReadToday = true;
        if (_boardNew != null) _boardNew.enabled = false;
    }

    // ── morning letters ───────────────────────────────────────────────────

    private void RollLetter(int day)
    {
        bool forced = DebugForce == "letter";
        if ((day < 2 && !forced) || vc_TownFlow.IsQuestDone(LetterMarker(day))) return;
        System.Random rng = Rng($"letter{StageId}:{day}");
        if (rng.NextDouble() >= LetterChancePerMorning && !forced) return;

        int st = Mathf.Clamp(vc_SkillLevels.StageIndex, 0, 2);
        var pool = new List<vc_TownEventData.Letter>();
        foreach (var l in vc_TownEventData.Letters)
            if (st >= l.minStage && st <= l.maxStage && (!l.repeatLoopsOnly || vc_LoopMeta.IsRepeatLoop)) pool.Add(l);
        if (pool.Count == 0) return;
        _letter = pool[rng.Next(pool.Count)];

        vc_TownHome home = GetComponentsInChildren<vc_TownHome>(true).Length > 0 ? GetComponentsInChildren<vc_TownHome>(true)[0] : null;
        Vector3 at = home != null ? home.DoorPosition + new Vector3(0.75f, -0.55f, 0f) : _plazaCenter;
        _letterGo = MakeSprite("Letter", _letterSprite, SafeSpot(at), -1).gameObject;
        StartCoroutine(LetterToast());
    }

    private IEnumerator LetterToast()
    {
        yield return new WaitForSeconds(2.2f);
        while (_stage != null && _stage.Busy) yield return null;
        if (_letterGo != null) vc_QuestHUD.Instance?.ShowFeedbackTimed("A letter is waiting by your door.", 3f);
    }

    private void OpenLetter()
    {
        if (_letter == null || vc_DialogPanel.Instance == null || vc_DialogPanel.Instance.IsOpen) return;
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);
        string body = _letter.body.Replace("{threat}", vc_LoopMeta.ThreatName(vc_LoopMeta.Threat));
        string marker = LetterMarker(_day);
        if (_letter.type == vc_TownEventData.LetterType.Gift
            && vc_BonusStars.Grant(marker, vc_TownEventData.LetterGiftStars, SceneName))
            body += $"\n\n(Something is tucked inside: +{vc_TownEventData.LetterGiftStars} stars.)";
        vc_TownFlow.MarkQuestDone(marker);
        vc_QuestRoom.SaveProgressIds(new[] { marker }, SceneName);
        string from = _letter.from == "???" ? "A letter with no name" : $"A letter from {_letter.from}";
        vc_DialogPanel.Instance.ShowMessage(from, body, null);
        if (_letterGo != null) Destroy(_letterGo);
        _letterGo = null;
        _letter = null;
    }

    // ── interaction: walk up, tap, or the Talk button ─────────────────────

    private Vector3 BoardPos => _board != null ? _board.transform.position : Vector3.positiveInfinity;

    /// <summary>The nearest thing here you can use (host / board / letter), for the Talk button.</summary>
    public bool Nearest(Vector2 playerPos, float range, out float dist, out string label, out Action act)
    {
        dist = float.MaxValue; label = null; act = null;
        if (_running) return false;
        var options = new List<(Vector3 p, string l, Action a)>();
        if (_host != null) options.Add((_hostPos, "Talk!", InviteToEvent));
        if (_board != null) options.Add((BoardPos, _boardNew != null && _boardNew.enabled ? "Read!" : "Read", ReadBoard));
        if (_letterGo != null) options.Add((_letterGo.transform.position, "Open!", OpenLetter));
        foreach (var (p, l, a) in options)
        {
            float d = Vector2.Distance(playerPos, p);
            if (d <= range && d < dist) { dist = d; label = l; act = a; }
        }
        return act != null;
    }

    /// <summary>A tap near the host, the board or the letter (player standing close).</summary>
    public bool TryTap(Vector2 world, Vector2 playerPos)
    {
        if (_running) return false;
        bool Hit(Vector3 p, float h) => Vector2.Distance(world, (Vector2)p + new Vector2(0f, h)) < 1f && Vector2.Distance(playerPos, p) < 2.4f;
        if (_host != null && Hit(_hostPos, 0.6f)) { InviteToEvent(); return true; }
        if (_board != null && Hit(BoardPos, 1f)) { ReadBoard(); return true; }
        if (_letterGo != null && Hit(_letterGo.transform.position, 0.2f)) { OpenLetter(); return true; }
        return false;
    }

    private float _frameT;
    private int _frame;

    private void Update()
    {
        if (!_ready) return;
        _frameT += Time.deltaTime;
        if (_frameT >= 0.11f)
        {
            _frameT = 0f;
            _frame++;
            if (_hostBody != null && _hostIdle != null && _hostIdle.Length > 0) _hostBody.sprite = _hostIdle[_frame % _hostIdle.Length];
            foreach (var (sr, frames, offset) in _crowdAnim) if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[(_frame + offset) % frames.Length];
            if (_mark != null && _frame % 3 == 0) _mark.sprite = _markFrames[(_frame / 3) % 2];
            if (_frame % 4 == 0) foreach (SpriteRenderer b in _bunting) if (b != null) b.sprite = _buntingFrames[(_frame / 4 + b.GetInstanceID()) & 1];
        }
        // the sparkle and the board's news note bob a pixel
        if (_mark != null) _mark.transform.position = Snap(_hostPos + new Vector3(0f, 1.6f + (Mathf.Repeat(Time.time, 1f) < 0.5f ? 0f : 1f / Ppu), 0f));
        if (_boardNew != null && _boardNew.enabled)
            _boardNew.transform.localPosition = new Vector3(0.55f, 1.75f + (Mathf.Repeat(Time.time, 0.8f) < 0.4f ? 0f : 1f / Ppu), 0f);

        // the board's "!": news today, or requests nobody has accepted yet
        if (_boardNew != null && Time.time > _nextNewsCheck)
        {
            _nextNewsCheck = Time.time + 0.5f;
            _boardNew.enabled = BoardHasNews();
        }

        if (_running || _stage == null || _stage.InQuest || _stage.Busy) return;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        Vector2 pp = player.transform.position;

        // fiesta: confetti drifts down around you now and then
        if (_fiesta && Time.time > _nextConfetti)
        {
            _nextConfetti = Time.time + UnityEngine.Random.Range(0.25f, 0.6f);
            StartCoroutine(ConfettiBit(player.transform.position + new Vector3(UnityEngine.Random.Range(-6f, 6f), 5f, 0f), false));
        }

        bool dialogOpen = vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen;
        // walking up to the host invites you once (walk away and back to be asked again)
        if (_host != null)
        {
            float d = Vector2.Distance(pp, _hostPos);
            if (d < WalkUpRange && !_nearHostPrompted && !dialogOpen) { _nearHostPrompted = true; InviteToEvent(); }
            else if (d > 2.4f) _nearHostPrompted = false;
        }
        // walking over the letter picks it up
        if (_letterGo != null && !dialogOpen && Vector2.Distance(pp, _letterGo.transform.position) < 0.6f) OpenLetter();
    }

    private float _nextNewsCheck;

    private bool BoardHasNews()
    {
        if (!_boardReadToday && (_event != null || _fiesta)) return true;
        vc_QuestBoard board = GetComponent<vc_QuestBoard>();
        if (board == null || _stage == null) return false;
        foreach (vc_TownNPC n in _stage.TodayRequests)
            if (n.HasQuest && !n.Attempted && !board.IsAccepted(n)) return true;
        return false;
    }

    private static void MovePlayer(PlayerController player, Vector3 pos)
    {
        if (player == null) return;
        pos.z = player.transform.position.z;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
        player.transform.position = pos;
        Physics2D.SyncTransforms();
    }

    // ── debug / tests ─────────────────────────────────────────────────────

    /// <summary>Debug menu: replay today with a forced event id, "fiesta" or "letter".</summary>
    public void DebugForceToday(string what)
    {
        DebugForce = what;
        if (what == "fiesta") vc_TownFlow.ForgetQuestIds(id => id == FiestaMarker);
        if (what == "letter") vc_TownFlow.ForgetQuestIds(id => id == LetterMarker(_day));
        OnDayStart(_day < 1 ? 1 : _day);
        DebugForce = null;
    }
}
