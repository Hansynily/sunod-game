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
/// and when the timer ends - pass or fail - the player comes back next to the NPC.
/// One attempt per quest, same as the corridor.
///
/// Days (the roguelite loop):
///  - each morning a few random NPCs from the open pool make a request: they gather at
///    the request spot near the plaza with a "!". The rest of the pool stays hidden;
///  - the player has <see cref="questsPerDay"/> energy; each accepted quest costs 1. At 0
///    the day ends, and the player can sleep early with the Sleep button;
///  - "Not for me" (at the prompt, or cancelled from inside the quest) scores the quest's
///    item 1 and the NPC leaves;
///  - requests not answered by sleep time expire: the NPC leaves for good, unscored;
///  - sleeping shows the day summary, a free pick of 1 of 3 new skills, then the dream.
/// When the pool is empty the "Go to next stage" button appears; the player moves on
/// when they're ready (no auto-skip).
///
/// Town progress is saved as ids in the run-state's completed list: the quest id for
/// every attempt, plus markers "no:&lt;id&gt;" (refused), "left:&lt;id&gt;" (expired) and
/// "day:&lt;stage&gt;:&lt;n&gt;" (day n slept), so Continue can rebuild the town.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownStage : MonoBehaviour
{
    public static vc_TownStage Current { get; private set; }

    public const string RefusedPrefix = "no:";
    public const string LeftPrefix = "left:";
    public const string DayPrefix = "day:";

    public string stageId = "elementary";
    public string stageTitle = "Elementary";
    [TextArea(2, 3)] public string introLine;
    [TextArea(2, 3)] public string completeLine;
    [Tooltip("Short dream lines shown when you sleep at the end of a day (one per day, cycled).")]
    [TextArea(2, 3)] public string[] nightDreams;
    [Tooltip("Energy per day: each accepted quest costs 1. At 0 the day ends.")]
    public int questsPerDay = 4;
    [Tooltip("Days the stage's NPC pool is spread over.")]
    public int daysPerStage = 4;
    [Tooltip("Requests offered each day on top of an even share of the pool (more requests than energy = real choices).")]
    public int extraRequestsPerDay = 1;
    public Transform playerSpawn;
    [Tooltip("Where today's requesting NPCs gather (near the plaza / spawn).")]
    public Transform requestSpot;
    public float requestSpacing = 1.6f;
    [Tooltip("Hidden spot (far from the town) where quest rooms are spawned.")]
    public Transform questArea;
    [Tooltip("Where the player stands inside a 16x16 quest room, relative to the room's center. Rooms are designed to be entered from the left.")]
    public Vector2 roomEntryOffset = new Vector2(-6.5f, 0f);

    private vc_TownNPC[] _npcs = new vc_TownNPC[0];
    private vc_TownHome[] _homes = new vc_TownHome[0];
    private readonly List<vc_TownNPC> _today = new List<vc_TownNPC>();
    private vc_TownNPC _activeNpc;
    private GameObject _activeRoom;
    private bool _inQuest;
    private bool _cancelActive;
    private bool _questDoneAcknowledged;
    private bool _stageCompleted;
    private bool _stageDoneAnnounced;
    private bool _busy;               // day summary / dream / stage end on screen
    private float _promptCooldownUntil;
    private int _day = 1;

    // today's tally for the end-of-day summary
    private int _starsToday;
    private int _questsToday;          // energy spent (accepted quests)
    private int _passedToday;
    private int _refusedToday;         // "Not for me" (prompt or cancelled)
    private int _cancelledToday;       // ...of which cancelled inside a quest (they used energy)
    private readonly Dictionary<string, int> _usageToday = new Dictionary<string, int>();

    public bool InQuest => _inQuest;
    public bool Busy => _busy;
    public int Day => _day;
    public int Energy => Mathf.Max(0, questsPerDay - _questsToday);
    public int BuiltQuestCount => _npcs.Count(n => n.HasQuest);
    public int AttemptedCount => _npcs.Count(n => n.HasQuest && n.Attempted);
    public int HelpedCount => _npcs.Count(n => n.HasQuest && n.Attempted && !n.Refused);
    public int OpenCount => _npcs.Count(n => n.IsOpen);
    public int RequestsLeftToday => _today.Count(n => n.IsOpen);
    public bool StageDone => BuiltQuestCount > 0 && OpenCount == 0;

    private void Awake()
    {
        Current = this;
        _npcs = GetComponentsInChildren<vc_TownNPC>(true);
        _homes = GetComponentsInChildren<vc_TownHome>(true);
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
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.HasQuest) continue;
            string id = npc.QuestId;
            npc.SetAttempted(vc_TownFlow.IsQuestDone(id));
            if (vc_TownFlow.IsQuestDone(RefusedPrefix + id)) npc.SetRefused(true);
            if (vc_TownFlow.IsQuestDone(LeftPrefix + id)) npc.SetGone(true);
        }

        // Resumed mid-stage: pick up on the right day (one marker per day slept here).
        _day = 1;
        while (vc_TownFlow.IsQuestDone(DayMarker(_day))) _day++;
        ResetDayTally();

        MovePlayer(playerSpawn != null ? playerSpawn.position : transform.position);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        StartDay();

        if (!string.IsNullOrEmpty(introLine) && AttemptedCount == 0 && _day == 1)
            vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} - Day {_day}", introLine, null);
        else
            vc_QuestHUD.Instance?.ShowFeedbackTimed($"{stageTitle} - Day {_day}", 2f);

        CheckStageComplete();
    }

    private string DayMarker(int day) => $"{DayPrefix}{stageId}:{day}";

    // ── Days: who asks for help today ─────────────────────────────────────

    /// <summary>
    /// Morning: yesterday's NPCs go home, and a fresh random set from the open pool make a
    /// request. Offers per day = an even share of the pool over the days left, plus
    /// <see cref="extraRequestsPerDay"/>, so the 16 NPCs last all 4 days while there are
    /// still more requests than energy.
    /// </summary>
    private void StartDay()
    {
        _today.Clear();
        var open = _npcs.Where(n => n.IsOpen).OrderBy(_ => Random.value).ToList();
        int daysLeft = Mathf.Max(1, daysPerStage - _day + 1);
        int count = Mathf.Min(open.Count, Mathf.CeilToInt(open.Count / (float)daysLeft) + Mathf.Max(0, extraRequestsPerDay));
        _today.AddRange(open.Take(count));

        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.HasQuest) { npc.SetVisible(true); continue; }   // "..." townsfolk stay put
            if (npc.Gone || npc.Refused) { npc.SetVisible(false); continue; }
            if (npc.Attempted) { npc.ReturnHome(); npc.SetVisible(true); continue; }
            npc.SetVisible(_today.Contains(npc));
        }

        PlaceRequests();
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
    }

    /// <summary>Lines today's requesters up at the request spot (rows of 5).</summary>
    private void PlaceRequests()
    {
        Vector3 center = requestSpot != null ? requestSpot.position
            : (playerSpawn != null ? playerSpawn.position + new Vector3(0f, 3f, 0f) : transform.position);
        const int perRow = 5;
        for (int i = 0; i < _today.Count; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Mathf.Min(perRow, _today.Count - row * perRow);
            float x = (col - (inRow - 1) / 2f) * requestSpacing;
            float y = -row * 1.8f;
            _today[i].PlaceAt(center + new Vector3(x, y, 0f));
        }
    }

    // ── NPC interaction ───────────────────────────────────────────────────

    private void Update()
    {
        // Tap / click an NPC you're standing next to, to talk again (e.g. after "Later").
        if (_inQuest || _busy || Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) return;

        Camera cam = Camera.main;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (cam == null || player == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Pointer.current.position.ReadValue());
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.gameObject.activeInHierarchy) continue;
            Vector2 npcCenter = (Vector2)npc.transform.position + new Vector2(0f, 0.6f);
            if (Vector2.Distance(world, npcCenter) > 0.9f) continue;
            if (Vector2.Distance(player.transform.position, npc.transform.position) > 2.2f) continue;
            _promptCooldownUntil = 0f;
            OnNpcApproached(npc);
            return;
        }
    }

    public void OnNpcApproached(vc_TownNPC npc)
    {
        if (_inQuest || _busy || npc == null || Time.time < _promptCooldownUntil) return;
        if (vc_DialogPanel.Instance == null || vc_DialogPanel.Instance.IsOpen) return;

        if (!npc.HasQuest)
        {
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, "I might need your help another time!", null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        if (npc.Attempted)
        {
            string thanks = string.IsNullOrEmpty(npc.thanksLine) ? "Thanks again for your help!" : npc.thanksLine;
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, thanks, null);
            _promptCooldownUntil = Time.time + 1f;
            return;
        }

        if (!npc.IsOpen || _stageCompleted) return;

        vc_DialogPanel.Instance.ShowChoice3(
            npc.npcName,
            npc.askLine,
            null,
            "Help",
            "Later",
            "Not for me",
            onConfirm: () => StartQuest(npc),
            onCancel: () =>
            {
                _promptCooldownUntil = Time.time + 0.5f;
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"Tap {npc.npcName} when you're ready.", 2.5f);
            },
            onThird: () => RefuseRequest(npc));
    }

    /// <summary>"Not for me" at the prompt: the quest's item scores 1 and the NPC leaves.</summary>
    private void RefuseRequest(vc_TownNPC npc)
    {
        if (npc == null || !npc.IsOpen) return;

        vc_QuestRoom room = npc.questPrefab != null ? npc.questPrefab.GetComponentInChildren<vc_QuestRoom>(true) : null;
        string id = npc.QuestId;
        if (room != null) room.RecordRefusal(gameObject.scene.name, RefusedPrefix + id);
        else vc_QuestRoom.SaveProgressIds(new[] { id, RefusedPrefix + id }, gameObject.scene.name);

        vc_TownFlow.MarkQuestDone(id);
        vc_TownFlow.MarkQuestDone(RefusedPrefix + id);
        npc.SetRefused(true);
        npc.SetVisible(false);
        _refusedToday++;
        _promptCooldownUntil = Time.time + 0.5f;

        vc_QuestHUD.Instance?.ShowFeedbackTimed($"{npc.npcName} will ask someone else.", 2.5f);
        AfterTownChange();
    }

    // ── Quest run ─────────────────────────────────────────────────────────

    private void StartQuest(vc_TownNPC npc)
    {
        if (_inQuest || npc == null || npc.questPrefab == null || Energy <= 0) return;

        _inQuest = true;
        _cancelActive = false;
        _activeNpc = npc;
        _questDoneAcknowledged = false;

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
        vc_RunProgressUI.Instance?.ShowQuestCancel(RequestCancelQuest);
        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;
        vc_QuestRoom.OnAnyQuestComplete += OnQuestDoneAcknowledged;

        // Standing inside the room's trigger starts the quest + timer.
        MovePlayer(roomPos + (Vector3)roomEntryOffset);
    }

    /// <summary>"Not for me" from inside the quest: confirm, then end it as not completed.</summary>
    private void RequestCancelQuest()
    {
        if (!_inQuest || _cancelActive || _activeNpc == null) return;
        if (vc_DialogPanel.Instance == null) { CancelActiveQuest(); return; }

        string who = _activeNpc.npcName;
        vc_DialogPanel.Instance.ShowChoice(
            "Not for me?",
            $"Stop this quest? It counts as \"not for me\", and {who} will ask someone else.",
            null,
            "Stop",
            "Keep going",
            onConfirm: CancelActiveQuest,
            onCancel: null);
    }

    private void CancelActiveQuest()
    {
        if (!_inQuest || _cancelActive) return;
        _cancelActive = true;
        vc_RunProgressUI.Instance?.HideQuestCancel();

        vc_QuestTimer timer = vc_QuestTimer.Instance;
        if (timer != null && timer.IsRunning())
        {
            timer.AbortQuest();   // -> OnQuestEnded (not passed); the room records the 1
            return;
        }

        // The quest never started (player not in the room's trigger yet): record it here.
        vc_QuestRoom room = _activeRoom != null ? _activeRoom.GetComponentInChildren<vc_QuestRoom>(true) : null;
        if (room != null && !room.IsFinished) room.RecordRefusal(gameObject.scene.name);
        if (timer != null) timer.QuestEnded -= OnQuestEnded;
        _questsToday++;
        StartCoroutine(ReturnAfterQuest(false));
    }

    private void OnQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (!_inQuest) return;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;
        vc_RunProgressUI.Instance?.HideQuestCancel();

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

        StartCoroutine(ReturnAfterQuest(result != null && result.DidPassQuest));
    }

    private void OnQuestDoneAcknowledged() => _questDoneAcknowledged = true;

    private IEnumerator ReturnAfterQuest(bool passed)
    {
        vc_QuestRoom room = _activeRoom != null ? _activeRoom.GetComponentInChildren<vc_QuestRoom>(true) : null;
        bool cancelled = _cancelActive && !passed;
        vc_TownNPC npc = _activeNpc;

        if (passed)
        {
            // Back to town only once the player taps the "Quest done" popup. The long cap
            // is just a safety net in case the popup never shows.
            float until = Time.time + 60f;
            while (!_questDoneAcknowledged && Time.time < until) yield return null;
        }
        else if (cancelled)
        {
            // Counts as "not for me": saved like a fail, plus the refused marker.
            string id = npc != null ? npc.QuestId : null;
            vc_QuestRoom.SaveProgressIds(new[] { id, id != null ? RefusedPrefix + id : null }, gameObject.scene.name);
            _refusedToday++;
            _cancelledToday++;
            vc_QuestHUD.Instance?.ShowFeedbackTimed("Okay. Maybe this one isn't for you.", 2f);
            yield return new WaitForSeconds(1f);
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
        _cancelActive = false;

        if (npc != null)
        {
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
            if (cancelled)
            {
                vc_TownFlow.MarkQuestDone(RefusedPrefix + npc.QuestId);
                npc.SetRefused(true);
                npc.SetVisible(false);   // they leave
                MovePlayer(npc.transform.position + new Vector3(0f, -1f, 0f));
            }
            else
            {
                MovePlayer(npc.transform.position + new Vector3(0f, -1f, 0f));
            }
        }

        _promptCooldownUntil = Time.time + 1f;
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        AfterTownChange();
    }

    /// <summary>After a quest or refusal: refresh, then end the day / announce the stage as needed.</summary>
    private void AfterTownChange()
    {
        RefreshHomes();
        RefreshHud();
        RefreshTownControls();
        vc_TownFlow.RaiseProgressChanged();

        if (StageDone) { CheckStageComplete(); return; }
        if (Energy <= 0) { EndDay(); return; }
        if (RequestsLeftToday == 0)
            vc_QuestHUD.Instance?.ShowFeedbackTimed("No one else needs help today. Tap Sleep to rest.", 3f);
    }

    // ── Sleep / end of day ────────────────────────────────────────────────

    private void ResetDayTally()
    {
        _starsToday = _questsToday = _passedToday = _refusedToday = _cancelledToday = 0;
        _usageToday.Clear();
    }

    /// <summary>Sleep button. Warns first if people are still waiting today.</summary>
    public void RequestSleep()
    {
        if (_inQuest || _busy || _stageCompleted) return;
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) return;

        int waiting = RequestsLeftToday;
        if (waiting == 0 || vc_DialogPanel.Instance == null) { EndDay(); return; }

        vc_DialogPanel.Instance.ShowChoice(
            "Sleep now?",
            waiting == 1
                ? "1 person is still waiting for help. If you sleep, they'll leave town."
                : $"{waiting} people are still waiting for help. If you sleep, they'll leave town.",
            null,
            "Sleep",
            "Not yet",
            onConfirm: () => EndDay(),
            onCancel: null);
    }

    private void EndDay(System.Action afterSleep = null)
    {
        _busy = true;
        RefreshTownControls();
        string kinds = DescribeKinds(_usageToday);
        int waiting = RequestsLeftToday;
        string body =
            $"People helped today: {_questsToday - _cancelledToday}\n" +
            $"Stars earned: {_starsToday}\n" +
            (_refusedToday > 0 ? $"Not for me: {_refusedToday}\n" : "") +
            (waiting > 0 ? $"Left without help: {waiting}\n" : "") + "\n" +
            (string.IsNullOrEmpty(kinds) ? "You took it one step at a time today." : $"Today you leaned on {kinds} skills.");

        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        string dream = nightDreams != null && nightDreams.Length > 0 ? nightDreams[(_day - 1) % nightDreams.Length] : null;

        System.Action wake = () =>
        {
            ExpireRequests();
            _day++;
            ResetDayTally();
            _busy = false;
            StartDay();
            vc_TownFlow.RaiseProgressChanged();
            if (afterSleep != null) { afterSleep(); return; }
            vc_QuestHUD.Instance?.ShowFeedbackTimed($"Day {_day}", 2f);
            CheckStageComplete();
        };

        if (ui != null) ui.ShowDaySummary($"{stageTitle} - Day {_day} done", body, dream, wake, OfferSkillPick);
        else vc_DialogPanel.Instance?.ShowMessage($"Day {_day} done", body, () => OfferSkillPick(wake));
    }

    /// <summary>Requests nobody answered today: those NPCs leave for good (not scored).</summary>
    private void ExpireRequests()
    {
        var ids = new List<string> { DayMarker(_day) };
        foreach (vc_TownNPC npc in _today)
        {
            if (!npc.IsOpen) continue;
            npc.SetGone(true);
            vc_TownFlow.MarkQuestDone(LeftPrefix + npc.QuestId);
            ids.Add(LeftPrefix + npc.QuestId);
        }
        vc_TownFlow.MarkQuestDone(DayMarker(_day));
        vc_QuestRoom.SaveProgressIds(ids, gameObject.scene.name);
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
            step = waiting == 1 ? "1 neighbor is waiting at the plaza. Go see if you can help."
                                : $"{waiting} neighbors are waiting at the plaza. Pick who to help.";
        else step = "You've done what you can today. Tap Sleep to rest.";

        vc_QuestHUD.Instance.ShowQuestInfo(
            $"Day {_day}  -  Energy {Energy}/{questsPerDay}",
            stageTitle,
            "Help your neighbors at the plaza.",
            new[] { step });
    }

    private void RefreshTownControls()
    {
        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui == null) return;
        ui.SetEnergy(Energy, questsPerDay);
        ui.SetSleepVisible(!_inQuest && !_busy && !_stageCompleted && BuiltQuestCount > 0);
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
        if (_questsToday > 0 || _refusedToday > 0) EndDay(go);
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

        // Everyone's been helped, turned down or has left. No auto-skip: the player taps
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
