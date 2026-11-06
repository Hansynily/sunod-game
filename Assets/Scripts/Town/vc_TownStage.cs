using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// One life-stage town (Elementary / High School / College). Owns the town's NPCs and
/// runs their quests: the quest room is spawned in a hidden area below the town, the
/// player enters it from the left (the room's trigger starts the quest and its timer),
/// and when the quest is over the player comes back next to the NPC. A finished quest
/// doesn't throw the player out: they can keep looking around the room and head back with
/// the "Back to town" button. One attempt per quest, same as the corridor.
///
/// Days (the roguelite loop):
///  - each morning a few NPCs from the open pool make a request: a "!" appears over them
///    where they live, scattered around town. Anyone who asked yesterday and wasn't helped
///    is still waiting (they ask first); the rest of the pool stays hidden;
///  - the player has <see cref="questsPerDay"/> energy; each accepted quest costs 1. At 0
///    the day ends, and the player can sleep early with the Sleep button;
///  - every quest must be played: the prompt is only Help / Later, nobody leaves town, and
///    a quest ends passed or failed (timer, or quitting mid-quest). Days keep coming until
///    all of the stage's quests are done - 4 days if the player uses all their energy;
///  - sleeping: the player heads home on their own (letterbox, no HUD) and walks out of the
///    frame, fading away; fade to the night, day summary, a free pick of 1 of 3 new skills,
///    the dream, then morning in front of their home.
/// When the pool is empty the "Go to next stage" button appears; the player moves on
/// when they're ready (no auto-skip).
///
/// Saving works like a farming game's "save on sleep": Continue always puts the player at
/// their home, on the morning of the current day (day 1 of a stage starts at the town's spawn), with that morning's requests
/// and the energy already spent. A quest's outcome is locked in the moment it's played - a
/// quest that was started and never finished (quit or closed mid-quest) counts as failed,
/// so quitting can never be used to retry one.
///
/// Town progress is saved as ids in the run-state's completed list: the quest id for
/// every attempt, plus markers "day:&lt;stage&gt;:&lt;n&gt;" (day n slept), "req:&lt;stage&gt;:&lt;n&gt;:&lt;id&gt;"
/// (asked for help on day n), "e:&lt;stage&gt;:&lt;n&gt;:&lt;id&gt;" (energy spent on it on day n),
/// and "s:&lt;id&gt;" (quest started).
/// Older saves may also hold "no:&lt;id&gt;" (refused) and "left:&lt;id&gt;" (expired) from before
/// "Not for me" and leaving were removed; those are still read so old runs load. Saves from
/// the shed version also hold "bed:..." markers, which are simply ignored now.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownStage : MonoBehaviour
{
    public static vc_TownStage Current { get; private set; }

    public const string RefusedPrefix = "no:";
    public const string LeftPrefix = "left:";   // old saves only (expired requests were removed)
    public const string DayPrefix = "day:";
    public const string RequestPrefix = "req:";
    public const string EnergyPrefix = "e:";
    public const string StartedPrefix = "s:";

    public string stageId = "elementary";
    public string stageTitle = "Elementary";
    [TextArea(2, 3)] public string introLine;
    [TextArea(2, 3)] public string completeLine;
    [Tooltip("Short dream lines shown when you sleep at the end of a day (one per day, cycled).")]
    [TextArea(2, 3)] public string[] nightDreams;
    [Tooltip("Energy per day: each accepted quest costs 1. At 0 the day ends.")]
    public int questsPerDay = 4;
    [Tooltip("Days the stage is planned for (16 quests / 4 energy). Not a hard limit: days keep coming until every quest is done.")]
    public int daysPerStage = 4;
    [Tooltip("Requests offered each day on top of the day's energy (more requests than energy = real choices).")]
    public int extraRequestsPerDay = 1;
    public Transform playerSpawn;
    [Tooltip("Unused since requesters stay where they live (kept so old scenes still load).")]
    public Transform requestSpot;
    public float requestSpacing = 1.6f;
    [Tooltip("Hidden spot (far from the town) where quest rooms are spawned.")]
    public Transform questArea;
    [Tooltip("Where the player stands inside a 16x16 quest room, relative to the room's center. Rooms are designed to be entered from the left.")]
    public Vector2 roomEntryOffset = new Vector2(-6.5f, 0f);
    [Tooltip("Seconds without talking to anyone before the arrow over the player points at the nearest request.")]
    public float hintArrowDelay = 30f;

    private vc_TownNPC[] _npcs = new vc_TownNPC[0];
    private vc_TownHome[] _homes = new vc_TownHome[0];
    private readonly List<vc_TownNPC> _today = new List<vc_TownNPC>();
    private vc_TownNPC _activeNpc;
    private GameObject _activeRoom;
    private bool _inQuest;
    private bool _questDoneAcknowledged;
    private bool _stageCompleted;
    private bool _stageDoneAnnounced;
    private bool _busy;               // sleeping / day summary / dream / stage end on screen
    private float _promptCooldownUntil;
    private float _lastTalkTime;
    private int _day = 1;

    // today's tally for the end-of-day summary
    private int _starsToday;
    private int _questsToday;          // energy spent (accepted quests)
    private int _passedToday;
    private readonly Dictionary<string, int> _usageToday = new Dictionary<string, int>();

    public bool InQuest => _inQuest;
    public bool Busy => _busy;
    /// <summary>A town event's cutscene holds the town like the sleep cutscene does (no prompts, no hint arrow).</summary>
    public void SetBusy(bool busy) { _busy = busy; RefreshTownControls(); }
    public int Day => _day;
    public int Energy => Mathf.Max(0, questsPerDay - _questsToday);
    public int BuiltQuestCount => _npcs.Count(n => n.HasQuest);
    public int AttemptedCount => _npcs.Count(n => n.HasQuest && n.Attempted);
    public int PassedCount => _npcs.Count(n => n.HasQuest && n.Attempted && n.Passed);
    public int OpenCount => _npcs.Count(n => n.IsOpen);
    public int RequestsLeftToday => _today.Count(n => n.IsOpen);
    /// <summary>Who is asking for help today (the plaza board's posters), in order.</summary>
    public IReadOnlyList<vc_TownNPC> TodayRequests => _today;
    /// <summary>Every quest giver in this town.</summary>
    public IReadOnlyList<vc_TownNPC> QuestGivers => _npcs;
    public bool StageDone => BuiltQuestCount > 0 && OpenCount == 0;

    /// <summary>
    /// Where the player wakes up: in front of their home. Before the first night of a stage,
    /// the town's spawn point (falls back to the home, then the town's origin).
    /// </summary>
    public Vector3 WakePosition =>
        _day > 1 && HomeWake.HasValue ? HomeWake.Value
        : playerSpawn != null ? playerSpawn.position
        : HomeWake ?? transform.position;

    private Vector3? HomeWake => _homes.Length > 0 && _homes[0] != null ? _homes[0].WakePosition : (Vector3?)null;

    private void Awake()
    {
        Current = this;
        _npcs = GetComponentsInChildren<vc_TownNPC>(true);
        _homes = GetComponentsInChildren<vc_TownHome>(true);
        // solid forest edge + far-forest backdrop down to the quest area (no blue void)
        Rect? questRect = questArea != null
            ? new Rect(questArea.position.x - 24f, questArea.position.y - 18f, 48f, 36f)
            : (Rect?)null;
        vc_TownBorder.Apply(transform, questRect);
        // roaming townsfolk + daily happenings (no scene setup needed)
        if (GetComponent<vc_TownLife>() == null) gameObject.AddComponent<vc_TownLife>();
        // morning chimney smoke over the houses
        if (GetComponent<vc_TownAtmosphere>() == null) gameObject.AddComponent<vc_TownAtmosphere>();
        // chickens, the town cat, laundry lines, morning mist, dew
        if (GetComponent<vc_TownCritters>() == null) gameObject.AddComponent<vc_TownCritters>();
        // rare town events, morning letters, the plaza bulletin board (TASKS §3.1)
        if (GetComponent<vc_TownEvents>() == null) gameObject.AddComponent<vc_TownEvents>();
        // the plaza quest board + the quest journal
        if (GetComponent<vc_QuestBoard>() == null) gameObject.AddComponent<vc_QuestBoard>();
        // the coast: foam washing in and out, wave crests rolling in
        if (transform.Find("ShoreFoam") != null && GetComponent<vc_SeaWaves>() == null) gameObject.AddComponent<vc_SeaWaves>();
        // warm low sun: cast shadows, warm wash + vignette, deeper sea
        if (GetComponent<vc_TownLighting>() == null) gameObject.AddComponent<vc_TownLighting>();
        // the Keepsake Shop stall, a few steps from home
        if (_homes.Length > 0 && _homes[0] != null && GetComponentInChildren<vc_KeepsakeStall>() == null)
        {
            Sprite bang = null;
            foreach (vc_TownNPC n in _npcs) if (n.markerQuest != null) { bang = n.markerQuest; break; }
            vc_KeepsakeStall.Place(this, _homes[0].DoorPosition, bang);
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        PlayerController.SpeedMultiplier = 1f;
        vc_LoopMoments.Existing?.SetPocketVisible(false);
        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;
    }

    private void Start()
    {
        // Played straight from a town scene in the editor: bring in the gameplay scene.
        if (vc_TownFlow.Instance == null && !SceneManager.GetSceneByName(vc_TownFlow.GameSceneName).isLoaded)
            SceneManager.LoadScene(vc_TownFlow.GameSceneName, LoadSceneMode.Additive);
    }

    /// <summary>Called by vc_TownFlow once the player is in this town.</summary>
    public void Enter()
    {
        var abandoned = new List<string>();
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.HasQuest) continue;
            string id = npc.QuestId;
            bool attempted = vc_TownFlow.IsQuestDone(id);

            // Started but never finished (quit / app closed mid-quest): it counts as a
            // failed attempt, same as running out of time. No retries by quitting.
            if (!attempted && vc_TownFlow.IsQuestDone(StartedPrefix + id))
            {
                RecordAbandoned(npc);
                abandoned.Add(npc.npcName);
                attempted = true;
            }

            npc.SetAttempted(attempted);
            npc.SetPassed(attempted && WasPassed(id));
            // Saves from before "Not for me" was removed: a refused quest stays answered (it
            // was scored 1). An NPC who "left" unhelped in an old save comes back: everyone
            // must be helped now, so their quest is open again.
            if (vc_TownFlow.IsQuestDone(RefusedPrefix + id)) npc.SetRefused(true);
        }

        // Resumed mid-stage: pick up on the right day (one marker per day slept here), in
        // front of the home (WakePosition).
        _day = 1;
        while (vc_TownFlow.IsQuestDone(DayMarker(_day))) _day++;
        ResetDayTally();
        // Energy already spent today (quests started before the save point).
        // Restore how they went too, or the summary counts every earlier one as failed.
        foreach (vc_TownNPC n in _npcs)
        {
            if (!n.HasQuest || !vc_TownFlow.IsQuestDone(EnergyMarker(_day, n.QuestId))) continue;
            _questsToday++;
            if (!n.Passed) continue;
            _passedToday++;
            _starsToday += StarsFor(n.QuestId);
        }

        // Checkpoint "the player is in this town" right away, so Continue (on any device)
        // resumes here even if they quit before playing a quest in it.
        vc_TownFlow.MarkQuestDone("in:" + stageId);
        vc_QuestRoom.SaveProgressIds(new[] { "in:" + stageId }, gameObject.scene.name);

        MovePlayer(WakePosition);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        StartDay();
        _lastTalkTime = Time.time;

        // Lucky Ribbon keepsake: a free skill pick on arriving in a new town (once per town per loop).
        if (vc_LoopMeta.HasMemento("ribbon") && AttemptedCount == 0 && _day == 1
            && RibbonPicksGiven.Add($"{vc_LoopMeta.Loop}:{stageId}"))
            StartCoroutine(RibbonPickWhenFree());

        // First morning of a new loop: "Loop N" - what you remember, what you still carry.
        if (vc_LoopMeta.IsRepeatLoop && WakeUpShownForLoop != vc_LoopMeta.Loop && AttemptedCount == 0 && _day == 1
            && vc_TownFlow.Instance != null && vc_TownFlow.Instance.CurrentStageIndex == 0 && vc_LoopMoments.Instance != null)
        {
            WakeUpShownForLoop = vc_LoopMeta.Loop;
            _busy = true;
            vc_LoopMoments.Instance.ShowWakeUp(vc_LoopMeta.Loop, vc_LoopMeta.Threat, RememberedSkills(), CarriedKeepsakes(), () =>
            {
                _busy = false;
                _lastTalkTime = Time.time;
                if (!string.IsNullOrEmpty(introLine))
                    vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} - Day {_day}", introLine, null);
            });
            CheckStageComplete();
            return;
        }

        if (abandoned.Count > 0)
            vc_DialogPanel.Instance?.ShowMessage(
                "Quest not finished",
                abandoned.Count == 1
                    ? $"{abandoned[0]}'s quest counts as failed."
                    : $"{abandoned.Count} unfinished quests count as failed.",
                null);
        else if (!string.IsNullOrEmpty(introLine) && AttemptedCount == 0 && _day == 1)
            vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} - Day {_day}", introLine, null);
        else
            vc_QuestHUD.Instance?.ShowFeedbackTimed($"{stageTitle} - Day {_day}", 2f);

        CheckStageComplete();
    }

    private string DayMarker(int day) => $"{DayPrefix}{stageId}:{day}";
    private string RequestMarker(int day, string questId) => $"{RequestPrefix}{stageId}:{day}:{questId}";
    private string EnergyMarker(int day, string questId) => $"{EnergyPrefix}{stageId}:{day}:{questId}";

    /// <summary>Did this quest's recorded attempt complete it? (Telemetry is seeded from the save on Continue.)</summary>
    private static bool WasPassed(string questId)
    {
        vc_SessionTelemetry t = vc_SessionTelemetry.Instance;
        if (t == null || string.IsNullOrEmpty(questId)) return true;
        bool any = false;
        foreach (var r in t.GetAllRecords())
        {
            if (r == null || r.questId != questId) continue;
            any = true;
            if (r.completed) return true;
        }
        return !any;   // no record at all (older save): keep the old check mark
    }

    private static int StarsFor(string questId)
    {
        vc_SessionTelemetry t = vc_SessionTelemetry.Instance;
        if (t == null) return 0;
        int best = 0;
        foreach (var r in t.GetAllRecords())
            if (r != null && r.questId == questId && r.completed) best = Mathf.Max(best, r.starsEarned);
        return best;
    }

    /// <summary>A quest left mid-way: record it as not completed and save, like a timer fail.</summary>
    private void RecordAbandoned(vc_TownNPC npc)
    {
        vc_QuestRoom room = npc.questPrefab != null ? npc.questPrefab.GetComponentInChildren<vc_QuestRoom>(true) : null;
        if (room != null) room.RecordNotFinished(gameObject.scene.name);
        else vc_QuestRoom.SaveProgressIds(new[] { npc.QuestId }, gameObject.scene.name);
        vc_TownFlow.MarkQuestDone(npc.QuestId);
    }

    // ── Days: who asks for help today ─────────────────────────────────────

    /// <summary>
    /// Morning: a set from the open pool make a request, where they live. Anyone who asked
    /// yesterday and wasn't helped is still waiting and asks first; the rest are a random
    /// pick. Offers per day = the day's energy + <see cref="extraRequestsPerDay"/>, so there
    /// is a real choice of who to help first, but nobody is ever skipped for good. The
    /// morning's picks are saved right away (the "save on sleep"), so Continue brings back
    /// the same people, not a fresh roll.
    /// </summary>
    /// <summary>
    /// The people asking today stand on spawn points: the spots the town builder placed the
    /// quest NPCs on, minus the ones taken by people already helped (they stay at home) and
    /// any too close to the keepsake stall. Shuffled, so who stands where changes each day.
    /// </summary>
    private void ShuffleRequestSpots()
    {
        var asking = _npcs.Where(n => n.HasQuest && !n.Attempted && !n.Refused && _today.Contains(n)).ToList();
        if (asking.Count == 0) return;

        vc_KeepsakeStall stall = GetComponentInChildren<vc_KeepsakeStall>();
        var spots = _npcs.Where(n => n.HasQuest && !n.Attempted && !n.Refused)   // spots nobody is standing on for good
            .Select(n => n.HomePosition)
            .Where(p => stall == null || Vector2.Distance(p, stall.transform.position + new Vector3(0f, 1f, 0f)) > 3f)
            .OrderBy(_ => Random.value)
            .ToList();

        // Planned towns (2026-11-07): everyone asks at their own place (shop door, pond, beach),
        // which the plaza board and the journal point to. No more shuffling.
        foreach (vc_TownNPC n in asking) n.PlaceAt(n.HomePosition);
    }

    private void StartDay()
    {
        _today.Clear();
        foreach (vc_TownNPC npc in _npcs)
            if (npc.HasQuest && vc_TownFlow.IsQuestDone(RequestMarker(_day, npc.QuestId)))
                _today.Add(npc);

        if (_today.Count == 0)
        {
            // A fresh random set every morning (nobody is skipped for good: the stage only ends
            // once everyone has been helped).
            var open = _npcs.Where(n => n.IsOpen).OrderBy(_ => Random.value).ToList();
            // Mang Erning's Whistle keepsake: one more person asks each morning (more choice
            // of who to help first; nothing about scoring changes).
            int extra = Mathf.Max(0, extraRequestsPerDay) + (vc_LoopMeta.HasMemento("whistle") ? 1 : 0);
            int count = Mathf.Min(open.Count, questsPerDay + extra);
            _today.AddRange(open.Take(count));

            var ids = new List<string>();
            foreach (vc_TownNPC npc in _today)
            {
                string m = RequestMarker(_day, npc.QuestId);
                vc_TownFlow.MarkQuestDone(m);
                ids.Add(m);
            }
            if (ids.Count > 0) vc_QuestRoom.SaveProgressIds(ids, gameObject.scene.name);
        }

        foreach (vc_TownNPC npc in _npcs)
        {
            npc.ReturnHome();
            if (!npc.HasQuest) { npc.SetVisible(true); continue; }   // "..." townsfolk stay put
            if (npc.Refused) { npc.SetVisible(false); continue; }    // old saves only
            if (npc.Attempted) { npc.SetVisible(true); continue; }
            npc.SetVisible(_today.Contains(npc));
        }
        ShuffleRequestSpots();

        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        GetComponent<vc_TownEvents>()?.OnDayStart(_day);
    }

    // ── NPC interaction ───────────────────────────────────────────────────

    private float _nextHintCheck;

    private void Update()
    {
        UpdateHintArrow();
        UpdateTalkButton();
        KeepPlayerInTown();
        // Old Sneakers keepsake: faster walking around town, never inside a quest room.
        PlayerController.SpeedMultiplier = !_inQuest && vc_LoopMeta.HasMemento("sneakers") ? 1.25f : 1f;
        // The keepsakes you carry, at the top of the screen while you're in town.
        if (vc_LoopMeta.Current != null && vc_LoopMeta.Current.owned_mementos.Count > 0)
            vc_LoopMoments.Instance?.SetPocketVisible(!_inQuest && !_busy);

        // Tap / click an NPC you're standing next to, to talk again (e.g. after "Later").
        if (_inQuest || _busy || Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) return;

        Camera cam = Camera.main;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (cam == null || player == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Pointer.current.position.ReadValue());
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.gameObject.activeInHierarchy || npc.Leaving) continue;
            Vector2 npcCenter = (Vector2)npc.transform.position + new Vector2(0f, 0.6f);
            if (Vector2.Distance(world, npcCenter) > 0.9f) continue;
            if (Vector2.Distance(player.transform.position, npc.transform.position) > 2.2f) continue;
            _promptCooldownUntil = 0f;
            OnNpcApproached(npc);
            return;
        }

        // today's event host, the bulletin board, a letter by the door
        if (GetComponent<vc_TownEvents>()?.TryTap(world, player.transform.position) == true) { _lastTalkTime = Time.time; return; }

        // roaming townsfolk and people at today's happenings talk too
        if (GetComponent<vc_TownLife>()?.TryTalk(world, player.transform.position, _day, stageId) == true)
            _lastTalkTime = Time.time;
        else
            GetComponent<vc_TownCritters>()?.TryPet(world, player.transform.position);   // pet the town cat
    }

    // ── Talk button (TASKS §3.2): players didn't know they could tap people ──
    // Whenever someone (or something) you can talk to is close, a Talk / Read / Open button
    // shows with the side controls. Tapping people still works too.

    private const float TalkRange = 1.7f;
    private float _nextTalkCheck;

    private void UpdateTalkButton()
    {
        if (Time.time < _nextTalkCheck) return;
        _nextTalkCheck = Time.time + 0.2f;
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui == null) return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        vc_QuestBoard board = GetComponent<vc_QuestBoard>();
        bool boardOpen = board != null && board.IsOpen;
        // the journal: always there in town (with how many accepted requests are waiting)
        ui.SetJournalButton(_inQuest || _busy || boardOpen ? -1 : (board != null ? board.JournalEntries().Count : 0), () => board?.OpenJournal());
        bool blocked = _inQuest || _busy || boardOpen || player == null || (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen);
        if (blocked) { ui.SetTalkButton(null, null); return; }

        Vector2 pp = player.transform.position;
        float best = TalkRange;
        string label = null;
        System.Action act = null;

        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.gameObject.activeInHierarchy || npc.Leaving) continue;
            float d = Vector2.Distance(pp, npc.transform.position);
            if (d >= best) continue;
            vc_TownNPC who = npc;
            best = d;
            label = npc.HasQuest && npc.IsOpen && !npc.Attempted ? "Talk!" : "Talk";
            act = () => { _promptCooldownUntil = 0f; OnNpcApproached(who); };
        }

        vc_TownEvents events = GetComponent<vc_TownEvents>();
        if (events != null && events.Nearest(pp, best, out float de, out string le, out System.Action ae) && de < best)
        {
            best = de; label = le; act = ae;
        }

        vc_TownLife life = GetComponent<vc_TownLife>();
        if (life != null && life.NearestTalker(pp, best, out Vector2 at) && Vector2.Distance(pp, at) < best)
        {
            label = "Talk";
            act = () => { if (life.TryTalk(at + new Vector2(0f, 0.6f), player.transform.position, _day, stageId)) _lastTalkTime = Time.time; };
        }

        ui.SetTalkButton(label, act);
    }

    private float _nextBoundsCheck;

    /// <summary>
    /// Safety net: if the player ever ends up off the map (past the edge walls) or standing
    /// in the water, put them back on the nearest walkable spot. Skipped during quests, the
    /// sleep cutscene (where the player's collider may be off on purpose).
    /// </summary>
    private void KeepPlayerInTown()
    {
        if (_inQuest || _busy || Time.time < _nextBoundsCheck) return;
        _nextBoundsCheck = Time.time + 0.25f;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null || player.ScriptedMove.HasValue) return;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null && !rb.simulated) return;

        Vector3 p = player.transform.position;
        Rect town = vc_TownGrid.TownRect(transform);
        bool outside = p.x < town.xMin || p.x > town.xMax || p.y < town.yMin || p.y > town.yMax;
        bool inWater = false;
        if (!outside)
        {
            if (_water == null) _water = transform.Find("Grid")?.Find("water")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            inWater = _water != null && _water.HasTile(_water.WorldToCell(p));
        }
        if (outside || inWater) MovePlayer(SafeTownSpot(p));
    }

    private UnityEngine.Tilemaps.Tilemap _water;

    /// <summary>The nearest spot to 'want' the player can actually stand on (not in water, a building or off the map).</summary>
    private Vector3 SafeTownSpot(Vector3 want)
    {
        vc_TownGrid grid = vc_TownGrid.For(transform);
        if (grid == null || grid.IsWalkable(want)) return want;
        Vector2? spot = grid.NearestWalkable(want, 16);
        return spot.HasValue ? new Vector3(spot.Value.x, spot.Value.y, want.z) : want;
    }

    /// <summary>
    /// Requests are scattered around town, so exploring is part of the day. If the player
    /// wanders a long time without finding anyone, the arrow over their head points at the
    /// nearest request.
    /// </summary>
    private void UpdateHintArrow()
    {
        if (Time.time < _nextHintCheck) return;
        _nextHintCheck = Time.time + 0.5f;
        vc_DirectionalArrow arrow = vc_DirectionalArrow.Instance;
        if (arrow == null) return;

        // Old Compass keepsake: the arrow shows up after a few seconds instead of hintArrowDelay.
        float delay = vc_LoopMeta.HasMemento("compass") ? Mathf.Min(hintArrowDelay, 6f) : hintArrowDelay;
        bool allowed = !_inQuest && !_busy && Energy > 0 && RequestsLeftToday > 0 && Time.time - _lastTalkTime > delay;
        vc_TownNPC nearest = null;
        if (allowed)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            float best = float.MaxValue;
            foreach (vc_TownNPC npc in _today)
            {
                if (!npc.IsOpen || !npc.gameObject.activeInHierarchy || player == null) continue;
                float d = Vector2.Distance(player.transform.position, npc.transform.position);
                if (d < best) { best = d; nearest = npc; }
            }
        }

        if (nearest != null) { arrow.SetTarget(nearest.transform); arrow.ShowArrow(); _hintShown = true; }
        else if (_hintShown) { arrow.ClearTarget(); arrow.HideArrow(); _hintShown = false; }
    }
    private bool _hintShown;

    public void OnNpcApproached(vc_TownNPC npc)
    {
        if (_inQuest || _busy || npc == null || npc.Leaving || Time.time < _promptCooldownUntil) return;
        if (vc_DialogPanel.Instance == null || vc_DialogPanel.Instance.IsOpen) return;
        _lastTalkTime = Time.time;

        if (!npc.HasQuest)
        {
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, vc_NpcDialogue.DailyLine(stageId, npc.npcName, _day), null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        if (npc.Attempted)
        {
            // The day you helped them they talk about it; after that it's their line of the day.
            string line;
            if (vc_TownFlow.IsQuestDone(EnergyMarker(_day, npc.QuestId)))
                line = npc.Passed
                    ? (string.IsNullOrEmpty(npc.thanksLine) ? "Thanks again for your help!" : npc.thanksLine)
                    : "That was a tough one, huh? Thanks for trying anyway. Really.";
            else
                line = vc_NpcDialogue.DailyLine(stageId, npc.npcName, _day);
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, line, null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        if (!npc.IsOpen || _stageCompleted) return;

        if (Energy <= 0)
        {
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, npc.askLine + "\n\nOut of energy. Come back tomorrow.", null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        // Loop 2+: the first time someone asks this loop, they half-remember you.
        string ask = npc.askLine;
        string seen = $"{vc_LoopMeta.Loop}:{npc.QuestId}";
        if (vc_LoopMeta.IsRepeatLoop && MemoryOpenersShown.Add(seen))
        {
            // Someone whose keepsake you carry notices it; everyone else half-remembers you.
            vc_LoopMeta.Keepsake theirs = vc_LoopMeta.KeepsakeByCode(npc.questCode);
            string opener = theirs != null && vc_LoopMeta.HasMemento(theirs.id)
                ? theirs.rememberLine
                : vc_LoopMeta.MemoryOpener(npc.QuestId, npc.QuestId.GetHashCode() & 0x7fffffff);
            ask = opener + "\n\n" + ask;
        }
        ask += RecommendedLine(npc.questCode);

        // Only Help / Later: every quest gets played (pass or fail), nobody is turned down.
        vc_DialogPanel.Instance.ShowChoice(
            npc.npcName,
            ask,
            null,
            "Help",
            "Later",
            onConfirm: () => StartQuest(npc),
            onCancel: () =>
            {
                _promptCooldownUntil = Time.time + 0.5f;
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"Tap {npc.npcName} when you're ready.", 2.5f);
            });
    }

    /// <summary>
    /// Skill system v2: advice only, never required. Just the level ("Rec. Lv2"), never which
    /// skill or kind: picking the skill is the player's call.
    /// </summary>
    private static string RecommendedLine(string questCode)
    {
        if (string.IsNullOrEmpty(questCode)) return "";
        return $"\n\nRec. Lv{vc_SkillLevels.RecommendedLevel(vc_SkillLevels.StageIndex, questCode)}";
    }

    private static readonly HashSet<string> MemoryOpenersShown = new HashSet<string>();
    private static int WakeUpShownForLoop;
    private static readonly HashSet<string> RibbonPicksGiven = new HashSet<string>();

    private IEnumerator RibbonPickWhenFree()
    {
        yield return null;
        while (_busy || (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen)
               || (vc_LoopMoments.Existing != null && vc_LoopMoments.Existing.IsShowing))
            yield return null;
        OfferSkillPick(null);
    }

    private static List<vc_SkillData> RememberedSkills()
    {
        var list = new List<vc_SkillData>();
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance;
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        if (all == null) return list;
        foreach (string name in vc_LoopMeta.KeptSkills)
        {
            vc_SkillData s = System.Array.Find(all, x => x != null && string.Equals(x.skillName, name, System.StringComparison.OrdinalIgnoreCase));
            if (s != null) list.Add(s);
        }
        return list;
    }

    private static List<vc_LoopMeta.Keepsake> CarriedKeepsakes()
    {
        var list = new List<vc_LoopMeta.Keepsake>();
        if (vc_LoopMeta.Current == null) return list;
        foreach (string id in vc_LoopMeta.Current.owned_mementos)
        {
            vc_LoopMeta.Keepsake k = vc_LoopMeta.KeepsakeById(id);
            if (k != null) list.Add(k);
        }
        return list;
    }

    // ── Quest run ─────────────────────────────────────────────────────────

    private void StartQuest(vc_TownNPC npc)
    {
        if (_inQuest || npc == null || npc.questPrefab == null || Energy <= 0) return;

        _inQuest = true;
        _activeNpc = npc;
        _questDoneAcknowledged = false;

        // Lock the attempt in before anything else: energy spent + quest started. If the game
        // is quit or closed before the quest ends, Continue counts it as not finished.
        string e = EnergyMarker(_day, npc.QuestId), s = StartedPrefix + npc.QuestId;
        vc_TownFlow.MarkQuestDone(e);
        vc_TownFlow.MarkQuestDone(s);
        vc_QuestRoom.SaveProgressIds(new[] { e, s }, gameObject.scene.name);

        Vector3 roomPos = questArea != null ? questArea.position : transform.position + new Vector3(0f, -100f, 0f);
        _activeRoom = Instantiate(npc.questPrefab, roomPos, Quaternion.identity, questArea);
        vc_TownBorder.WallRoom(_activeRoom);   // some rooms have open sides; keep the player in

        if (vc_QuestTimer.Instance != null)
        {
            vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;
            vc_QuestTimer.Instance.QuestEnded += OnQuestEnded;
            vc_QuestTimer.Instance.SetStarsVisible(true);
        }
        vc_RunProgressUI.Instance?.SetBannerVisible(false);
        RefreshTownControls();
        vc_RunProgressUI.Instance?.EnterQuestMode();
        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;
        vc_QuestRoom.OnAnyQuestComplete += OnQuestDoneAcknowledged;

        // Standing inside the room's trigger starts the quest + timer.
        MovePlayer(roomPos + (Vector3)roomEntryOffset);
    }

    /// <summary>
    /// Quit to the menu from the pause screen while a quest is running: the quest ends now as
    /// failed (same as running out of time) and the save is pushed before leaving.
    /// </summary>
    public void AbandonQuestForQuit()
    {
        if (!_inQuest || _activeNpc == null) return;
        vc_TownNPC npc = _activeNpc;
        vc_QuestRoom room = _activeRoom != null ? _activeRoom.GetComponentInChildren<vc_QuestRoom>(true) : null;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;

        vc_QuestTimer timer = vc_QuestTimer.Instance;
        if (timer != null && timer.IsRunning()) timer.AbortQuest();   // the room records the result
        else if (room != null && !room.IsFinished) room.RecordNotFinished(gameObject.scene.name);

        vc_TownFlow.MarkQuestDone(npc.QuestId);
        vc_QuestRoom.SaveProgressIds(new[] { npc.QuestId }, gameObject.scene.name);
        _inQuest = false;
    }

    private void OnQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (!_inQuest) return;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;
        vc_RunProgressUI.Instance?.ExitQuestMode();

        // Tally for the end-of-day summary (usage is reset at the next quest's start).
        _questsToday++;
        if (result != null && result.DidPassQuest)
        {
            _passedToday++;
            _starsToday += result.FinalStarsEarnedNormalized;
        }
        if (vc_SkillManager.Instance != null)
            foreach (var kv in vc_SkillManager.Instance.GetUsageSummary())
                _usageToday[kv.Key] = (_usageToday.TryGetValue(kv.Key, out int v) ? v : 0) + kv.Value;

        RefreshTownControls();   // energy chip shows the spent point right away
        StartCoroutine(ReturnAfterQuest(result != null && result.DidPassQuest));
    }

    private void OnQuestDoneAcknowledged() => _questDoneAcknowledged = true;

    private IEnumerator ReturnAfterQuest(bool passed)
    {
        vc_QuestRoom room = _activeRoom != null ? _activeRoom.GetComponentInChildren<vc_QuestRoom>(true) : null;
        vc_TownNPC npc = _activeNpc;

        if (passed)
        {
            // The player stays in the room as long as they like; the "Back to town" button
            // (shown with the quest-done banner) brings them back.
            while (!_questDoneAcknowledged) yield return null;
        }
        else
        {
            // A failed quest still counts as this NPC's one attempt - save it for Continue.
            room?.WriteAttemptCheckpoint();
            vc_QuestHUD.Instance?.ShowFeedbackTimed("Time's up!", 2f);
            yield return new WaitForSeconds(2f);
        }

        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;

        if (_activeRoom != null) Destroy(_activeRoom);
        _activeRoom = null;
        _activeNpc = null;
        _inQuest = false;

        if (npc != null)
        {
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
            npc.SetPassed(passed);
            MovePlayer(SafeTownSpot(npc.transform.position + new Vector3(0f, -1f, 0f)));
        }

        _promptCooldownUntil = Time.time + 1f;
        _lastTalkTime = Time.time;
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);

        // Helping some people unlocks their keepsake in the shop.
        if (passed && npc != null && vc_LoopMeta.TryUnlock(npc.questCode, out vc_LoopMeta.Keepsake unlocked))
            vc_LoopMoments.Instance?.ShowUnlocked(unlocked);

        AfterTownChange();
    }

    /// <summary>After a quest: refresh, then end the day / announce the stage as needed.</summary>
    private void AfterTownChange()
    {
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        vc_TownFlow.RaiseProgressChanged();

        if (StageDone) { CheckStageComplete(); return; }
        if (Energy <= 0) { StartCoroutine(EndDayAfterMessages()); return; }
        if (RequestsLeftToday == 0)
            vc_QuestHUD.Instance?.ShowFeedbackTimed("No one else needs help today. Tap Sleep to rest.", 3f);
    }

    /// <summary>Out of energy: let any open message close, then head home.</summary>
    private IEnumerator EndDayAfterMessages()
    {
        yield return new WaitForSeconds(1.2f);
        while (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) yield return null;
        if (!_busy && !_inQuest) EndDay();
    }

    // ── Sleep / end of day ────────────────────────────────────────────────

    private void ResetDayTally()
    {
        _starsToday = _questsToday = _passedToday = 0;
        _usageToday.Clear();
    }

    /// <summary>Sleep button. Asks first if energy is left and people are still waiting today.</summary>
    public void RequestSleep()
    {
        if (_inQuest || _busy || _stageCompleted) return;
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) return;

        int waiting = RequestsLeftToday;
        if (waiting == 0 || Energy <= 0 || vc_DialogPanel.Instance == null) { EndDay(); return; }

        vc_DialogPanel.Instance.ShowChoice(
            "Sleep now?",
            (waiting == 1
                ? "1 person is still waiting for help, and you have energy left."
                : $"{waiting} people still need help.") +
            "\n\nSleeping early adds a day.",
            null,
            "Sleep",
            "Not yet",
            onConfirm: () => EndDay(),
            onCancel: null);
    }

    private void EndDay(System.Action afterSleep = null)
    {
        if (_busy && afterSleep == null) return;
        _busy = true;
        RefreshTownControls();
        string kinds = DescribeKinds(_usageToday);
        int failed = _questsToday - _passedToday;
        int left = OpenCount;
        string body =
            $"People helped today: {_questsToday}" + (failed > 0 ? $"  ({_passedToday} done, {failed} failed)" : "") + "\n" +
            $"Stars earned: {_starsToday}\n" +
            (left > 0 ? $"Still to help in {stageTitle}: {left}\n" : $"Everyone in {stageTitle} has been helped!\n") + "\n" +
            (string.IsNullOrEmpty(kinds) ? "You took it one step at a time today." : $"Today you leaned on {kinds} skills.");

        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        string dream = nightDreams != null && nightDreams.Length > 0 ? nightDreams[(_day - 1) % nightDreams.Length] : null;

        // The player heads home for the night and wakes up in front of it.
        System.Action wake = () =>
        {
            SaveNight();
            _day++;
            ResetDayTally();
            MovePlayer(WakePosition);
            _busy = false;
            StartDay();
            _lastTalkTime = Time.time;
            vc_TownFlow.RaiseProgressChanged();
            if (afterSleep != null) { afterSleep(); return; }
            vc_QuestHUD.Instance?.ShowFeedbackTimed($"Day {_day}", 2f);
            CheckStageComplete();
        };

        // No new-skill pick when this night leads straight to the results (last stage, leaving
        // town or its final day): a skill you can never use is just noise before the ending.
        int stageIdx = vc_TownFlow.Instance != null ? vc_TownFlow.Instance.CurrentStageIndex : -1;
        bool lastStage = stageIdx < 0 || stageIdx + 1 >= vc_TownFlow.TownScenes.Length;
        bool endingRun = lastStage && (afterSleep != null || OpenCount == 0);
        // The night's skill pick is earned: no quest played today, no pick (no farming skills
        // by sleeping through days).
        bool earnedPick = _questsToday > 0;
        System.Action<System.Action> pick = endingRun || !earnedPick ? (System.Action<System.Action>)(then => then?.Invoke())
            : vc_LoopMeta.HasMemento("lantern") ? (then => OfferSkillPick(() => OfferSkillPick(then)))   // Lola's Lantern: +1 pick
            : OfferSkillPick;

        string title = $"{stageTitle} - Day {_day} done";
        Vector3 home = HomeWake ?? WakePosition;
        if (ui != null) StartCoroutine(SleepSequence(ui, title, body, dream, home, wake, pick));
        else vc_DialogPanel.Instance?.ShowMessage($"Day {_day} done", body, () => pick(wake));
    }

    /// <summary>
    /// Going to bed: letterbox in, HUD away, and the player heads home on their own, on a real
    /// path around buildings and trees (vc_TownGrid). The camera stays put, so they walk out of
    /// the frame, fading away as they reach its edge (or after a couple of seconds, or at the
    /// door if home is close). Then the screen fades to the night: day summary, skill pick and
    /// dream over the night picture.
    /// </summary>
    private IEnumerator SleepSequence(vc_RunProgressUI ui, string title, string body, string dream,
                                      Vector3 home, System.Action wake, System.Action<System.Action> pick)
    {
        vc_DirectionalArrow.Instance?.HideArrow();
        _hintShown = false;
        while (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) yield return null;

        ui.SetCinematic(true);
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.MoveAction.Disable();
            yield return new WaitForSeconds(0.5f);   // letterbox settles
            player.CameraHeld = true;

            bool walking = true;
            Coroutine walk = StartCoroutine(RunThen(WalkPath(player, home, 4f), () => walking = false));
            Camera cam = Camera.main;
            float started = Time.time, alpha = 1f;
            while (alpha > 0f)
            {
                bool leaving = !walking || Time.time - started > 2.2f || NearScreenEdge(cam, player.transform.position);
                if (leaving) alpha = Mathf.MoveTowards(alpha, 0f, Time.deltaTime / 0.6f);
                SetPlayerAlpha(player, alpha);
                yield return null;
            }
            if (walking) StopCoroutine(walk);
            player.ScriptedMove = Vector2.zero;
            yield return new WaitForSeconds(0.2f);
        }

        yield return ui.FadeScreen(1f, 0.6f);
        if (player != null)
        {
            player.ScriptedMove = null;
            SetPlayerGhost(player, false);   // collider back on, fully visible again
            player.CameraHeld = false;
            MovePlayer(home);
        }
        ui.ShowNight(2);
        yield return ui.FadeScreen(0f, 0.5f);

        ui.ShowDaySummary(title, body, dream, () => StartCoroutine(WakeUp(ui, wake)), pick);
    }

    /// <summary>
    /// Walks the player along a path to 'target' (scripted input, so their walk animation
    /// plays). Gives up after maxSeconds, or if no path exists.
    /// </summary>
    private IEnumerator WalkPath(PlayerController player, Vector3 target, float maxSeconds)
    {
        vc_TownGrid grid = vc_TownGrid.For(transform);
        List<Vector3> path = grid != null ? grid.FindPath(player.transform.position, target) : new List<Vector3> { target };
        if (path.Count == 0) path.Add(target);

        float until = Time.time + maxSeconds;
        int i = SkipPassedWaypoints(grid, path, player.transform.position);
        Vector3 checkPos = player.transform.position;
        float nextCheck = Time.time + 0.6f;
        int stuckCount = 0;
        bool ghost = false;
        while (i < path.Count && Time.time < until)
        {
            if (ghost)
            {
                // Still stuck after re-planning (a prop corner, a roaming NPC): it's a cutscene,
                // so slide through whatever is in the way instead of freezing on the spot.
                Vector3 step = Vector3.MoveTowards(player.transform.position, path[i], player.speed * Time.deltaTime);
                player.ScriptedMove = ((Vector2)(path[i] - player.transform.position)).normalized;
                player.transform.position = step;
                if (Vector2.Distance(step, path[i]) < 0.05f) i++;
                yield return null;
                continue;
            }

            Vector2 to = path[i] - player.transform.position;
            bool last = i == path.Count - 1;
            if (to.magnitude < (last ? 0.08f : 0.2f)) { i++; continue; }
            // slow down for the last bit so we stop on the spot instead of overshooting it
            float k = last ? Mathf.Clamp01(to.magnitude / 0.35f) : 1f;
            player.ScriptedMove = to.normalized * Mathf.Max(0.35f, k);

            // Really stuck (barely moved over a whole window, e.g. someone in the way):
            // re-plan from here. Physics only moves the player on fixed steps, so this has to
            // be measured over time, not frame to frame.
            if (Time.time >= nextCheck)
            {
                if (Vector2.Distance(player.transform.position, checkPos) < 0.15f)
                {
                    stuckCount++;
                    List<Vector3> replanned = grid != null ? grid.FindPath(player.transform.position, target) : new List<Vector3>();
                    if (stuckCount >= 2 || replanned.Count == 0)
                    {
                        if (replanned.Count > 0) { path = replanned; i = 0; }
                        ghost = true;
                        SetPlayerGhost(player, true);   // collider off for the rest of the walk
                        continue;
                    }
                    path = replanned;
                    i = SkipPassedWaypoints(grid, path, player.transform.position);
                }
                else stuckCount = 0;
                checkPos = player.transform.position;
                nextCheck = Time.time + 0.6f;
            }
            yield return null;
        }
        player.ScriptedMove = Vector2.zero;
        if (ghost) SetPlayerGhost(player, false);
    }

    /// <summary>
    /// A fresh path starts at the centre of the cell the player is standing in, which can be
    /// behind them. Skip ahead to the furthest early waypoint they can already walk straight to.
    /// </summary>
    private static int SkipPassedWaypoints(vc_TownGrid grid, List<Vector3> path, Vector3 from)
    {
        if (grid == null || path.Count < 2) return 0;
        int i = 0;
        while (i + 1 < path.Count && i < 2 && grid.LineClear(from, path[i + 1])) i++;
        return i;
    }

    private static IEnumerator RunThen(IEnumerator routine, System.Action done)
    {
        yield return routine;
        done?.Invoke();
    }

    /// <summary>True when a world point is at (or past) the edge of the camera's view.</summary>
    private static bool NearScreenEdge(Camera cam, Vector3 world)
    {
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(world);
        const float m = 0.06f;
        return v.x < m || v.x > 1f - m || v.y < m || v.y > 1f - m;
    }

    private static void SetPlayerAlpha(PlayerController player, float alpha)
    {
        foreach (SpriteRenderer r in player.GetComponentsInChildren<SpriteRenderer>()) { Color c = r.color; c.a = alpha; r.color = c; }
    }

    /// <summary>
    /// Cutscene walking through whatever is in the way: the player's collider is off and
    /// physics leaves them alone. Turning it back off also makes them fully visible again.
    /// </summary>
    private static void SetPlayerGhost(PlayerController player, bool ghost)
    {
        if (player == null) return;
        foreach (Collider2D c in player.GetComponents<Collider2D>()) if (!c.isTrigger) c.enabled = !ghost;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = !ghost; }
        if (!ghost) SetPlayerAlpha(player, 1f);
    }

    /// <summary>Morning: the screen fades back in on the player in front of their home.</summary>
    private IEnumerator WakeUp(vc_RunProgressUI ui, System.Action wake)
    {
        yield return ui.FadeScreen(1f, 0.35f);
        ui.HideNight();
        wake?.Invoke();
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.ScriptedMove = new Vector2(0f, -0.001f);   // face the town
        yield return new WaitForSeconds(0.25f);
        ui.SetCinematic(false);
        yield return ui.FadeScreen(0f, 0.8f);
        if (player != null)
        {
            player.ScriptedMove = null;
            player.MoveAction.Enable();
        }
    }

    /// <summary>
    /// Saves the night: the day is over (Continue wakes the player at home on the next
    /// morning). Unhelped requests are not touched - those people ask again tomorrow.
    /// </summary>
    private void SaveNight()
    {
        vc_TownFlow.MarkQuestDone(DayMarker(_day));
        vc_QuestRoom.SaveProgressIds(new[] { DayMarker(_day) }, gameObject.scene.name);
    }

    /// <summary>
    /// Before the dream: pick 1 of 3 skills (free). Mostly skills you don't own; about 1 card in
    /// 4 is a duplicate of one you own (+4 XP to it, Hades style), and about 1 night in 6 a rare
    /// skill (Plan / Survey) shows up (SKILL_SYSTEM_DESIGN.md §5).
    /// </summary>
    private void OfferSkillPick(System.Action then)
    {
        vc_SkillData[] picks = DrawSkillPicks(3);
        if (picks.Length == 0 || vc_SkillAwardPopup.Instance == null) { then?.Invoke(); return; }

        vc_SkillAwardPopup.Instance.Show(picks, chosen =>
        {
            if (chosen != null)
            {
                bool duplicate = vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(chosen);
                if (duplicate)
                {
                    vc_SkillLevels.AddXp(chosen, vc_SkillLevels.XpDuplicate);
                    vc_QuestHUD.Instance?.ShowFeedbackTimed($"{chosen.DisplayName} +{vc_SkillLevels.XpDuplicate} XP", 2.5f);
                    // not in its button right now? offer the swap, else nothing to ask
                    if (vc_SkillManager.Instance != null && vc_SkillManager.Instance.GetSkillInCategory(chosen.riaSecLetter) != chosen)
                        vc_SkillManager.Instance.OfferEquip(chosen);
                }
                else
                {
                    vc_PlayerInventory.Instance?.AddSkill(chosen);
                    vc_SkillManager.Instance?.OfferEquip(chosen);
                }
                // save the pick (new skill or XP) right away, so quitting tonight keeps it
                vc_QuestRoom.SaveProgressIds(null, gameObject.scene.name);
            }
            StartCoroutine(AfterDialogClosed(then));
        });
    }

    private IEnumerator AfterDialogClosed(System.Action then)
    {
        yield return null;
        while (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) yield return null;
        then?.Invoke();
    }

    /// <summary>Random skills the player doesn't own, preferring kinds the town's open quests use.</summary>
    private vc_SkillData[] DrawSkillPicks(int count)
    {
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
            ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        if (all == null) return new vc_SkillData[0];

        var openLetters = new HashSet<string>();
        foreach (vc_TownNPC npc in _npcs)
            if (npc.IsOpen && !string.IsNullOrEmpty(npc.questCode))
                openLetters.Add(npc.questCode.Substring(0, 1).ToUpperInvariant());

        bool Owned(vc_SkillData s) => vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(s);

        // new skills (rares only by their own roll below)
        var pool = new List<(vc_SkillData s, int w)>();
        // duplicates of owned common skills that can still level
        var dupes = new List<(vc_SkillData s, int w)>();
        var rares = new List<vc_SkillData>();
        var rareDupes = new List<vc_SkillData>();
        foreach (vc_SkillData s in all)
        {
            if (s == null) continue;
            bool owned = Owned(s);
            if (vc_SkillLevels.IsRare(s))
            {
                if (!owned) rares.Add(s);
                else if (vc_SkillLevels.Level(s) < vc_SkillLevels.MaxLevel) rareDupes.Add(s);
                continue;
            }
            bool wanted = !string.IsNullOrEmpty(s.riaSecLetter) && openLetters.Contains(s.riaSecLetter.ToUpperInvariant());
            if (!owned) pool.Add((s, wanted ? 3 : 1));
            else if (vc_SkillLevels.Level(s) < vc_SkillLevels.MaxLevel) dupes.Add((s, wanted ? 3 : 1));
        }

        var picks = new List<vc_SkillData>();

        // Rare: about 1 night in 6 (a rare you already own: about 1 night in 12).
        if (rares.Count > 0 && Random.value < 1f / 6f) picks.Add(rares[Random.Range(0, rares.Count)]);
        else if (rareDupes.Count > 0 && Random.value < 1f / 12f) picks.Add(rareDupes[Random.Range(0, rareDupes.Count)]);

        while (picks.Count < count && (pool.Count > 0 || dupes.Count > 0))
        {
            // about 1 card in 4 is a duplicate (always, once nothing new is left)
            var from = dupes.Count > 0 && (pool.Count == 0 || Random.value < 0.25f) ? dupes : pool;
            int total = 0;
            foreach (var p in from) total += p.w;
            int roll = Random.Range(0, total), acc = 0;
            for (int i = 0; i < from.Count; i++)
            {
                acc += from[i].w;
                if (roll < acc) { picks.Add(from[i].s); from.RemoveAt(i); break; }
            }
        }

        // shuffle so the rare card isn't always first
        for (int i = picks.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (picks[i], picks[j]) = (picks[j], picks[i]);
        }
        return picks.ToArray();
    }

    private static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>
    {
        { "R", "Realistic" }, { "I", "Investigative" }, { "A", "Artistic" },
        { "S", "Social" }, { "E", "Enterprising" }, { "C", "Conventional" },
    };

    private static string DescribeKinds(Dictionary<string, int> usage)
    {
        var top = usage.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(2)
            .Select(kv => KindNames.TryGetValue(kv.Key.ToUpperInvariant(), out string n) ? n : kv.Key).ToList();
        if (top.Count == 0) return "";
        return top.Count == 1 ? top[0] : $"{top[0]} and {top[1]}";
    }

    // ── Stage progress ────────────────────────────────────────────────────

    private void RefreshHud()
    {
        if (vc_QuestHUD.Instance == null) return;
        // One clear next step, based on what's actually left today.
        int waiting = RequestsLeftToday;
        string step;
        if (BuiltQuestCount > 0 && StageDone) step = "Go to the next stage.";
        else if (waiting > 0 && Energy > 0) step = waiting == 1 ? "1 person needs help." : $"{waiting} people need help.";
        else step = "Tap Sleep.";

        vc_QuestHUD.Instance.ShowQuestInfo(
            $"Day {_day}  -  Energy {Energy}/{questsPerDay}",
            stageTitle,
            $"Quests: {AttemptedCount}/{BuiltQuestCount}",
            new[] { step });
    }

    private void RefreshTownControls()
    {
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui == null) return;
        ui.SetEnergy(Energy, questsPerDay);
        ui.SetSleepVisible(!_inQuest && !_busy && !_stageCompleted && BuiltQuestCount > 0);
        bool canSleep = !_inQuest && !_busy && !_stageCompleted && BuiltQuestCount > 0;
        ui.SetSleepGlow(canSleep && !(BuiltQuestCount > 0 && StageDone) && (Energy <= 0 || RequestsLeftToday == 0));
    }

    private void RefreshHomes()
    {
        // Rebuilds as the town's requests get resolved (helped, refused or expired).
        int resolved = _npcs.Count(n => n.HasQuest && (n.Attempted || n.Gone));
        float progress = BuiltQuestCount > 0 ? (float)resolved / BuiltQuestCount : 0f;
        foreach (vc_TownHome home in _homes) home.SetProgress(progress);
    }

    /// <summary>
    /// Shows the "Next stage" button whenever nothing is left to do here - the request pool
    /// is empty, or the stage has no built quests at all yet. The player moves on when
    /// they're ready.
    /// </summary>
    private void UpdateNextStageButton()
    {
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui == null) return;
        if (_inQuest || (BuiltQuestCount > 0 && OpenCount > 0)) { ui.HideNextStageButton(); return; }

        int idx = vc_TownFlow.Instance != null ? vc_TownFlow.Instance.CurrentStageIndex : -1;
        bool last = idx < 0 || idx + 1 >= vc_TownFlow.TownScenes.Length;
        string next = !last && vc_TownFlow.Instance.Stages.Length > idx + 1 ? vc_TownFlow.Instance.Stages[idx + 1].title : "";
        ui.ShowNextStageButton(last ? "See my results  >" : $"Go to {next}  >", GoToNextStage);
    }

    /// <summary>Leave this stage now (Next stage button). Closes out the current day first.</summary>
    public void GoToNextStage()
    {
        if (_inQuest || _busy) return;
        _stageCompleted = true;
        vc_RunProgressUI.Instance?.HideNextStageButton();
        RefreshTownControls();
        System.Action go = () => { _busy = true; RefreshTownControls(); vc_TownFlow.Instance?.LoadNextTown(); };
        if (_questsToday > 0) EndDay(go);
        else go();
    }

    private void CheckStageComplete()
    {
        UpdateNextStageButton();
        if (_stageDoneAnnounced || _inQuest || _busy) return;

        if (BuiltQuestCount == 0)
        {
            // No quests built for this stage yet: let the player look around, then move on.
            _stageDoneAnnounced = true;
            vc_QuestHUD.Instance?.ShowFeedbackTimed("No one needs help here yet. Tap the button at the top to move on.", 4f);
            return;
        }
        if (!StageDone) return;

        // Every quest here has been played (passed or failed). No auto-skip: the player taps
        // the "Go to ..." button when they're ready.
        _stageDoneAnnounced = true;
        RefreshTownControls();
        string line = string.IsNullOrEmpty(completeLine) ? "You've helped everyone here. Time to grow up!" : completeLine;
        vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} done!", line + "\n\nTap the button at the top when you're ready to move on.", null);
    }

    // ── Debug menu hooks ──────────────────────────────────────────────────

    /// <summary>Marks every built quest in this town as attempted and finishes the stage.</summary>
    public void DebugCompleteStage()
    {
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.HasQuest || npc.Gone) continue;
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
            npc.SetPassed(!npc.Refused);
            npc.SetVisible(!npc.Refused);
        }
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        vc_TownFlow.RaiseProgressChanged();
        CheckStageComplete();
    }

    /// <summary>
    /// Like <see cref="DebugCompleteStage"/>, but every quest still open gets a real record:
    /// passed with a random fitting skill and a believable time, saved and checkpointed like a
    /// real pass (so the save, the server and the prediction all see it). Keepsakes from these
    /// quests are given too. Returns how many quests were recorded.
    /// </summary>
    public int DebugCompleteStageWithSkills()
    {
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
            ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;

        int recorded = 0;
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.IsOpen) continue;
            vc_QuestRoom room = npc.questPrefab.GetComponentInChildren<vc_QuestRoom>(true);
            if (room == null) continue;
            room.DebugRecordPass(gameObject.scene.name, room.DebugPickFittingSkill(all));
            vc_LoopMeta.TryUnlock(npc.questCode, out _);
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
            npc.SetPassed(true);
            npc.SetVisible(true);
            recorded++;
        }
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        vc_TownFlow.RaiseProgressChanged();
        CheckStageComplete();
        return recorded;
    }

    /// <summary>Quest NPCs in this town (for the debug menu's per-person buttons).</summary>
    public IReadOnlyList<vc_TownNPC> DebugQuestNpcs => _npcs.Where(n => n.HasQuest).ToList();

    /// <summary>Starts this person's quest right now, wherever the player is.</summary>
    public void DebugStartQuest(vc_TownNPC npc)
    {
        if (npc == null || !npc.IsOpen || _inQuest || _busy) return;
        if (!_today.Contains(npc)) _today.Add(npc);
        npc.SetVisible(true);
        vc_DialogPanel.Instance?.Hide();
        if (Energy <= 0) _questsToday = Mathf.Max(0, questsPerDay - 1);   // debug: make room for one more
        StartQuest(npc);
    }

    /// <summary>Records this person's quest as passed with a fitting skill (like the stage button, one person).</summary>
    public void DebugFinishQuest(vc_TownNPC npc)
    {
        if (npc == null || !npc.IsOpen || _inQuest) return;
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
            ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        vc_QuestRoom room = npc.questPrefab.GetComponentInChildren<vc_QuestRoom>(true);
        if (room == null) return;
        room.DebugRecordPass(gameObject.scene.name, room.DebugPickFittingSkill(all));
        if (vc_LoopMeta.TryUnlock(npc.questCode, out vc_LoopMeta.Keepsake unlocked)) vc_LoopMoments.Instance?.ShowUnlocked(unlocked);
        vc_TownFlow.MarkQuestDone(npc.QuestId);
        npc.SetAttempted(true);
        npc.SetPassed(true);
        npc.SetVisible(true);
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        vc_TownFlow.RaiseProgressChanged();
        CheckStageComplete();
    }

    /// <summary>
    /// Makes this person's quest unplayed again (forgets the attempt in the town, the quest
    /// records and the save), and puts them back asking for help today, so it can be replayed.
    /// </summary>
    public void DebugResetQuest(vc_TownNPC npc)
    {
        if (npc == null || !npc.HasQuest || _inQuest) return;
        string id = npc.QuestId;
        System.Predicate<string> mine = s => s == id || s == StartedPrefix + id || s == RefusedPrefix + id
            || (s.StartsWith(EnergyPrefix) && s.EndsWith(":" + id));
        vc_TownFlow.ForgetQuestIds(mine);
        vc_SessionTelemetry.Instance?.DebugForgetQuest(id);
        vc_QuestRoom.DebugForgetIds(mine, gameObject.scene.name);

        npc.SetAttempted(false);
        npc.SetGone(false);
        npc.ReturnHome();
        if (!_today.Contains(npc)) _today.Add(npc);
        npc.SetVisible(true);
        _stageCompleted = false;
        _stageDoneAnnounced = false;
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        UpdateNextStageButton();
        vc_TownFlow.RaiseProgressChanged();
    }

    /// <summary>Ends the current day right away (summary + skill pick + dream).</summary>
    public void DebugEndDay()
    {
        if (_inQuest || _busy) return;
        EndDay();
    }

    /// <summary>First NPC asking for help today (else any open one), or null.</summary>
    public vc_TownNPC DebugNextOpenNpc()
    {
        foreach (vc_TownNPC npc in _today)
            if (npc.IsOpen) return npc;
        foreach (vc_TownNPC npc in _npcs)
            if (npc.IsOpen) return npc;
        return null;
    }

    public void DebugTeleportTo(vc_TownNPC npc)
    {
        if (npc == null || _inQuest) return;
        if (npc.IsOpen && !_today.Contains(npc))
        {
            _today.Add(npc);
            npc.SetVisible(true);
        }
        _promptCooldownUntil = Time.time + 0.5f;
        MovePlayer(SafeTownSpot(npc.transform.position + new Vector3(0f, -1.2f, 0f)));
    }

    /// <summary>Clears this town's progress so every quest can be played again.</summary>
    public void DebugResetProgress()
    {
        foreach (vc_TownNPC npc in _npcs)
        {
            npc.SetAttempted(false);
            npc.SetGone(false);
            npc.ReturnHome();
        }
        vc_TownFlow.ResetRun();
        _stageCompleted = false;
        _stageDoneAnnounced = false;
        _day = 1;
        ResetDayTally();
        StartDay();
        UpdateNextStageButton();
        vc_TownFlow.RaiseProgressChanged();
    }

    private Vector3 PlayerPosition()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        return player != null ? player.transform.position : transform.position;
    }

    /// <summary>Quest journal: fade out, appear next to 'who', fade in.</summary>
    public void TeleportTo(vc_TownNPC who)
    {
        if (who == null || _inQuest || _busy) return;
        StartCoroutine(TeleportRoutine(who));
    }

    private IEnumerator TeleportRoutine(vc_TownNPC who)
    {
        _busy = true;
        RefreshTownControls();
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui != null) yield return ui.FadeScreen(1f, 0.3f);
        vc_Sfx_Play("quest_start");
        MovePlayer(SafeTownSpot(who.transform.position + new Vector3(0f, -1.3f, 0f)));
        _lastTalkTime = Time.time;
        _promptCooldownUntil = Time.time + 0.6f;
        yield return new WaitForSeconds(0.15f);
        if (ui != null) yield return ui.FadeScreen(0f, 0.35f);
        _busy = false;
        RefreshTownControls();
    }

    private static void vc_Sfx_Play(string clip) => SunodGame.Core.vc_Sfx.Play(clip, 0.5f);

    private void MovePlayer(Vector3 pos)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        pos.z = player.transform.position.z;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
        player.transform.position = pos;
        Physics2D.SyncTransforms();
    }
}
