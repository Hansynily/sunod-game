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
///  - sleeping: the player walks to the nearest shed (letterbox, no HUD), fade to the night,
///    day summary, a free pick of 1 of 3 new skills, the dream, then morning at that shed.
/// When the pool is empty the "Go to next stage" button appears; the player moves on
/// when they're ready (no auto-skip).
///
/// Saving works like a farming game's "save on sleep": Continue always puts the player at
/// the shed they slept in, on the morning of the current day, with that morning's requests
/// and the energy already spent. A quest's outcome is locked in the moment it's played - a
/// quest that was started and never finished (quit or closed mid-quest) counts as failed,
/// so quitting can never be used to retry one.
///
/// Town progress is saved as ids in the run-state's completed list: the quest id for
/// every attempt, plus markers "day:&lt;stage&gt;:&lt;n&gt;" (day n slept), "req:&lt;stage&gt;:&lt;n&gt;:&lt;id&gt;"
/// (asked for help on day n), "e:&lt;stage&gt;:&lt;n&gt;:&lt;id&gt;" (energy spent on it on day n),
/// "s:&lt;id&gt;" (quest started) and "bed:&lt;stage&gt;:&lt;n&gt;:&lt;shed&gt;" (slept in that shed after day n).
/// Older saves may also hold "no:&lt;id&gt;" (refused) and "left:&lt;id&gt;" (expired) from before
/// "Not for me" and leaving were removed; those are still read so old runs load.
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
    public const string BedPrefix = "bed:";

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
    private vc_TownShed[] _sheds = new vc_TownShed[0];
    private int _bedShed = -1;            // shed slept in last (-1 = none yet this stage)
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
    public int Day => _day;
    public int Energy => Mathf.Max(0, questsPerDay - _questsToday);
    public int BuiltQuestCount => _npcs.Count(n => n.HasQuest);
    public int AttemptedCount => _npcs.Count(n => n.HasQuest && n.Attempted);
    public int PassedCount => _npcs.Count(n => n.HasQuest && n.Attempted && n.Passed);
    public int OpenCount => _npcs.Count(n => n.IsOpen);
    public int RequestsLeftToday => _today.Count(n => n.IsOpen);
    public bool StageDone => BuiltQuestCount > 0 && OpenCount == 0;

    /// <summary>
    /// Where the player wakes up: in front of the shed they slept in. Before the first night
    /// of a stage, the town's spawn point (falls back to the home, then the town's origin).
    /// </summary>
    public Vector3 WakePosition =>
        _bedShed >= 0 && _bedShed < _sheds.Length && _sheds[_bedShed] != null ? _sheds[_bedShed].WakePosition
        : playerSpawn != null ? playerSpawn.position
        : _homes.Length > 0 && _homes[0] != null ? _homes[0].WakePosition
        : transform.position;

    private void Awake()
    {
        Current = this;
        _npcs = GetComponentsInChildren<vc_TownNPC>(true);
        _homes = GetComponentsInChildren<vc_TownHome>(true);
        _sheds = GetComponentsInChildren<vc_TownShed>(true).OrderBy(s => s.name).ToArray();
        // roaming townsfolk + daily happenings (no scene setup needed)
        if (GetComponent<vc_TownLife>() == null) gameObject.AddComponent<vc_TownLife>();
        // morning chimney smoke over the houses
        if (GetComponent<vc_TownAtmosphere>() == null) gameObject.AddComponent<vc_TownAtmosphere>();
        // chickens, the town cat, laundry lines, morning mist, dew
        if (GetComponent<vc_TownCritters>() == null) gameObject.AddComponent<vc_TownCritters>();
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
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
        // front of the shed slept in last.
        _day = 1;
        while (vc_TownFlow.IsQuestDone(DayMarker(_day))) _day++;
        _bedShed = -1;
        for (int d = _day - 1; d >= 1 && _bedShed < 0; d--)
            for (int i = 0; i < _sheds.Length; i++)
                if (vc_TownFlow.IsQuestDone(BedMarker(d, i))) { _bedShed = i; break; }
        ResetDayTally();
        // Energy already spent today (quests started before the save point).
        _questsToday = _npcs.Count(n => n.HasQuest && vc_TownFlow.IsQuestDone(EnergyMarker(_day, n.QuestId)));

        // Checkpoint "the player is in this town" right away, so Continue (on any device)
        // resumes here even if they quit before playing a quest in it.
        vc_TownFlow.MarkQuestDone("in:" + stageId);
        vc_QuestRoom.SaveProgressIds(new[] { "in:" + stageId }, gameObject.scene.name);

        MovePlayer(WakePosition);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        StartDay();
        _lastTalkTime = Time.time;

        if (abandoned.Count > 0)
            vc_DialogPanel.Instance?.ShowMessage(
                "Quest not finished",
                abandoned.Count == 1
                    ? $"You left {abandoned[0]}'s quest before it was done, so it counts as failed.\n\nYou wake up on Day {_day}."
                    : $"You left {abandoned.Count} quests before they were done, so they count as failed.\n\nYou wake up on Day {_day}.",
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
    private string BedMarker(int day, int shed) => $"{BedPrefix}{stageId}:{day}:{shed}";

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
    private void StartDay()
    {
        _today.Clear();
        foreach (vc_TownNPC npc in _npcs)
            if (npc.HasQuest && vc_TownFlow.IsQuestDone(RequestMarker(_day, npc.QuestId)))
                _today.Add(npc);

        if (_today.Count == 0)
        {
            var open = _npcs.Where(n => n.IsOpen)
                .OrderByDescending(n => _day > 1 && vc_TownFlow.IsQuestDone(RequestMarker(_day - 1, n.QuestId)))
                .ThenBy(_ => Random.value)
                .ToList();
            int count = Mathf.Min(open.Count, questsPerDay + Mathf.Max(0, extraRequestsPerDay));
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

        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
    }

    // ── NPC interaction ───────────────────────────────────────────────────

    private float _nextHintCheck;

    private void Update()
    {
        UpdateHintArrow();

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

        // roaming townsfolk and people at today's happenings talk too
        if (GetComponent<vc_TownLife>()?.TryTalk(world, player.transform.position, _day, stageId) == true)
            _lastTalkTime = Time.time;
        else
            GetComponent<vc_TownCritters>()?.TryPet(world, player.transform.position);   // pet the town cat
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

        bool allowed = !_inQuest && !_busy && Energy > 0 && RequestsLeftToday > 0 && Time.time - _lastTalkTime > hintArrowDelay;
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
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, npc.askLine + "\n\nYou're out of energy today. Get some sleep - I'll still be here tomorrow.", null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        // Only Help / Later: every quest gets played (pass or fail), nobody is turned down.
        vc_DialogPanel.Instance.ShowChoice(
            npc.npcName,
            npc.askLine,
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
            MovePlayer(npc.transform.position + new Vector3(0f, -1f, 0f));
        }

        _promptCooldownUntil = Time.time + 1f;
        _lastTalkTime = Time.time;
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
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
                : $"{waiting} people are still waiting for help, and you have energy left.") +
            "\n\nThey'll wait until tomorrow, but sleeping early adds an extra day to this stage.",
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

        // Sleep in the shed nearest to where the player is now; they wake up there.
        int shed = NearestShed(PlayerPosition());
        System.Action wake = () =>
        {
            SaveNight(shed);
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
        System.Action<System.Action> pick = endingRun ? (System.Action<System.Action>)(then => then?.Invoke()) : OfferSkillPick;

        string title = $"{stageTitle} - Day {_day} done";
        Vector3 bedDoor = shed >= 0 ? _sheds[shed].DoorPosition
            : _homes.Length > 0 && _homes[0] != null ? _homes[0].DoorPosition : WakePosition;
        if (ui != null) StartCoroutine(SleepSequence(ui, title, body, dream, bedDoor, shed, wake, pick));
        else vc_DialogPanel.Instance?.ShowMessage($"Day {_day} done", body, () => pick(wake));
    }

    /// <summary>
    /// Going to bed: letterbox in, HUD away, and the player walks to the shed on their own,
    /// on a real path around buildings and trees (vc_TownGrid), with the camera following.
    /// At the shed the door swings open, they step inside, the door shuts, and the screen
    /// fades to the night: day summary, skill pick and dream over the night picture.
    /// A very long walk is cut short (fade early) so bedtime never drags.
    /// </summary>
    private IEnumerator SleepSequence(vc_RunProgressUI ui, string title, string body, string dream,
                                      Vector3 door, int shed, System.Action wake, System.Action<System.Action> pick)
    {
        vc_DirectionalArrow.Instance?.HideArrow();
        _hintShown = false;
        while (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) yield return null;

        ui.SetCinematic(true);
        PlayerController player = FindFirstObjectByType<PlayerController>();
        vc_TownShed bed = shed >= 0 && shed < _sheds.Length ? _sheds[shed] : null;
        bool wentInside = false;
        if (player != null)
        {
            player.MoveAction.Disable();
            yield return new WaitForSeconds(0.5f);   // letterbox settles

            Vector3 front = bed != null ? bed.WakePosition : door + new Vector3(0f, -0.9f, 0f);
            yield return WalkPath(player, front, 7f);

            if (bed != null && Vector2.Distance(player.transform.position, front) < 0.6f)
            {
                yield return EnterShed(player, bed);
                wentInside = true;
            }
            player.ScriptedMove = Vector2.zero;
        }

        yield return ui.FadeScreen(1f, wentInside ? 0.5f : 0.7f);
        if (shed >= 0) _bedShed = shed;
        if (player != null)
        {
            player.ScriptedMove = null;
            SetPlayerInside(player, false, bed);
            MovePlayer(WakePosition);
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
        while (i < path.Count && Time.time < until)
        {
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
                if (Vector2.Distance(player.transform.position, checkPos) < 0.15f && grid != null)
                {
                    path = grid.FindPath(player.transform.position, target);
                    if (path.Count == 0) break;
                    i = SkipPassedWaypoints(grid, path, player.transform.position);
                }
                checkPos = player.transform.position;
                nextCheck = Time.time + 0.6f;
            }
            yield return null;
        }
        player.ScriptedMove = Vector2.zero;
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

    /// <summary>Door opens, the player steps in and fades into the dark doorway, door closes.</summary>
    private IEnumerator EnterShed(PlayerController player, vc_TownShed bed)
    {
        player.ScriptedMove = new Vector2(0f, 0.001f);   // face the door
        yield return new WaitForSeconds(0.25f);
        bed.OpenDoor();
        yield return new WaitForSeconds(0.3f);

        SetPlayerInside(player, true, bed);
        Vector3 from = player.transform.position, inside = bed.DoorPosition + new Vector3(0f, 0.25f, 0f);
        float t = 0f;
        var renderers = player.GetComponentsInChildren<SpriteRenderer>();
        while (t < 1f)
        {
            t += Time.deltaTime / 0.55f;
            player.ScriptedMove = Vector2.up * 0.6f;     // walking animation only (collider is off)
            player.transform.position = Vector3.Lerp(from, inside, t);
            foreach (SpriteRenderer r in renderers) { Color c = r.color; c.a = 1f - Mathf.Clamp01((t - 0.35f) / 0.65f); r.color = c; }
            yield return null;
        }
        player.ScriptedMove = Vector2.zero;
        yield return new WaitForSeconds(0.2f);
        bed.CloseDoor();
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>
    /// Morning at a shed: the door opens, the player steps out to the front and the door shuts
    /// behind them. Starts hidden in the doorway; the screen is already fading in.
    /// </summary>
    private IEnumerator LeaveShed(PlayerController player, vc_TownShed bed)
    {
        Vector3 inside = bed.DoorPosition + new Vector3(0f, 0.25f, 0f), front = bed.WakePosition;
        SetPlayerInside(player, true, bed);
        var renderers = player.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer r in renderers) { Color c = r.color; c.a = 0f; r.color = c; }
        player.transform.position = inside;
        yield return new WaitForSeconds(0.35f);
        bed.OpenDoor();
        yield return new WaitForSeconds(0.25f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.55f;
            player.ScriptedMove = Vector2.down * 0.6f;
            player.transform.position = Vector3.Lerp(inside, front, t);
            foreach (SpriteRenderer r in renderers) { Color c = r.color; c.a = Mathf.Clamp01(t / 0.5f); r.color = c; }
            yield return null;
        }
        player.ScriptedMove = null;
        SetPlayerInside(player, false, bed);
        MovePlayer(front);
        yield return new WaitForSeconds(0.15f);
        bed.CloseDoor();
    }

    /// <summary>
    /// While going through a shed door the player's collider is off (the doorway is part of
    /// the shed's solid footprint) and they draw just behind the shed's roof.
    /// </summary>
    private static void SetPlayerInside(PlayerController player, bool inside, vc_TownShed bed)
    {
        if (player == null) return;
        foreach (Collider2D c in player.GetComponents<Collider2D>()) if (!c.isTrigger) c.enabled = !inside;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = !inside; }
        if (!inside)
            foreach (SpriteRenderer r in player.GetComponentsInChildren<SpriteRenderer>()) { Color c = r.color; c.a = 1f; r.color = c; }
    }

    private IEnumerator WakeUp(vc_RunProgressUI ui, System.Action wake)
    {
        yield return ui.FadeScreen(1f, 0.35f);
        ui.HideNight();
        wake?.Invoke();
        PlayerController player = FindFirstObjectByType<PlayerController>();
        vc_TownShed bed = _bedShed >= 0 && _bedShed < _sheds.Length ? _sheds[_bedShed] : null;
        bool stepOut = player != null && bed != null && bed.doorOpen != null;
        if (stepOut)
        {
            // hide in the doorway before the screen comes back
            SetPlayerInside(player, true, bed);
            foreach (SpriteRenderer r in player.GetComponentsInChildren<SpriteRenderer>()) { Color c = r.color; c.a = 0f; r.color = c; }
            player.transform.position = bed.DoorPosition + new Vector3(0f, 0.25f, 0f);
        }
        yield return new WaitForSeconds(0.25f);
        ui.SetCinematic(false);
        StartCoroutine(ui.FadeScreen(0f, 0.8f));
        if (stepOut) yield return LeaveShed(player, bed);
        else yield return new WaitForSeconds(0.8f);
        player?.MoveAction.Enable();
    }

    /// <summary>
    /// Saves the night: the day is over, and which shed the player slept in (Continue wakes
    /// them there). Unhelped requests are not touched - those people ask again tomorrow.
    /// </summary>
    private void SaveNight(int shed)
    {
        var ids = new List<string> { DayMarker(_day) };
        vc_TownFlow.MarkQuestDone(DayMarker(_day));
        if (shed >= 0)
        {
            _bedShed = shed;
            ids.Add(BedMarker(_day, shed));
            vc_TownFlow.MarkQuestDone(BedMarker(_day, shed));
        }
        vc_QuestRoom.SaveProgressIds(ids, gameObject.scene.name);
    }

    /// <summary>Index of the shed closest to a point (-1 if the town has none).</summary>
    private int NearestShed(Vector3 from)
    {
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _sheds.Length; i++)
        {
            if (_sheds[i] == null) continue;
            float d = Vector2.Distance(from, _sheds[i].DoorPosition);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    /// <summary>Before the dream: pick 1 of 3 random skills you don't own (free).</summary>
    private void OfferSkillPick(System.Action then)
    {
        vc_SkillData[] picks = DrawSkillPicks(3);
        if (picks.Length == 0 || vc_SkillAwardPopup.Instance == null) { then?.Invoke(); return; }

        vc_SkillAwardPopup.Instance.Show(picks, chosen =>
        {
            if (chosen != null)
            {
                vc_PlayerInventory.Instance?.AddSkill(chosen);
                vc_SkillManager.Instance?.OfferEquip(chosen);
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

        var pool = new List<(vc_SkillData s, int w)>();
        foreach (vc_SkillData s in all)
        {
            if (s == null) continue;
            if (vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(s)) continue;
            bool wanted = !string.IsNullOrEmpty(s.riaSecLetter) && openLetters.Contains(s.riaSecLetter.ToUpperInvariant());
            pool.Add((s, wanted ? 3 : 1));
        }

        var picks = new List<vc_SkillData>();
        while (picks.Count < count && pool.Count > 0)
        {
            int total = 0;
            foreach (var p in pool) total += p.w;
            int roll = Random.Range(0, total), acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += pool[i].w;
                if (roll < acc) { picks.Add(pool[i].s); pool.RemoveAt(i); break; }
            }
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
        if (BuiltQuestCount > 0 && StageDone) step = "Everyone's settled. Head to the next stage when you're ready.";
        else if (waiting > 0 && Energy > 0)
            step = waiting == 1 ? "1 neighbor needs help today. Look around town for the \"!\"."
                                : $"{waiting} neighbors need help today. Look around town for the \"!\".";
        else step = "You've done what you can today. Tap Sleep to rest in the nearest shed.";

        vc_QuestHUD.Instance.ShowQuestInfo(
            $"Day {_day}  -  Energy {Energy}/{questsPerDay}",
            stageTitle,
            $"Quests done here: {AttemptedCount} of {BuiltQuestCount}. Help everyone to grow up to the next stage.",
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
        MovePlayer(npc.transform.position + new Vector3(0f, -1.2f, 0f));
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
