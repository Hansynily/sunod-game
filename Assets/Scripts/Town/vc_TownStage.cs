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
/// The stage is split into days of <see cref="questsPerDay"/> quests. Each day ends with a
/// summary (stars, which kinds of skills you leaned on) and a short dream; the home
/// rebuilds as you help people. When every built quest has been attempted, vc_TownFlow
/// moves on to the next stage.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownStage : MonoBehaviour
{
    public static vc_TownStage Current { get; private set; }

    public string stageId = "elementary";
    public string stageTitle = "Elementary";
    [TextArea(2, 3)] public string introLine;
    [TextArea(2, 3)] public string completeLine;
    [Tooltip("Short dream lines shown when you sleep at the end of a day (one per day, cycled).")]
    [TextArea(2, 3)] public string[] nightDreams;
    public int questsPerDay = 4;
    public Transform playerSpawn;
    [Tooltip("Hidden spot (far from the town) where quest rooms are spawned.")]
    public Transform questArea;
    [Tooltip("Where the player stands inside a 16x16 quest room, relative to the room's center. Rooms are designed to be entered from the left.")]
    public Vector2 roomEntryOffset = new Vector2(-6.5f, 0f);

    private vc_TownNPC[] _npcs = new vc_TownNPC[0];
    private vc_TownHome[] _homes = new vc_TownHome[0];
    private vc_TownNPC _activeNpc;
    private GameObject _activeRoom;
    private bool _inQuest;
    private bool _questDoneAcknowledged;
    private bool _stageCompleted;
    private bool _busy;               // day summary / dream / stage end on screen
    private float _promptCooldownUntil;
    private int _day = 1;

    // today's tally for the end-of-day summary
    private int _starsToday;
    private int _questsToday;
    private int _passedToday;
    private readonly Dictionary<string, int> _usageToday = new Dictionary<string, int>();

    public bool InQuest => _inQuest;
    public bool Busy => _busy;
    public int Day => _day;
    public int BuiltQuestCount => _npcs.Count(n => n.HasQuest);
    public int AttemptedCount => _npcs.Count(n => n.HasQuest && n.Attempted);

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
            npc.SetAttempted(npc.HasQuest && vc_TownFlow.IsQuestDone(npc.QuestId));

        // Resumed mid-stage: pick up on the right day.
        _day = 1 + AttemptedCount / Mathf.Max(1, questsPerDay);
        ResetDayTally();

        MovePlayer(playerSpawn != null ? playerSpawn.position : transform.position);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        RefreshHomes();
        RefreshHud();

        if (!string.IsNullOrEmpty(introLine) && AttemptedCount == 0)
            vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} - Day {_day}", introLine, null);
        else
            vc_QuestHUD.Instance?.ShowFeedbackTimed($"{stageTitle} - Day {_day}", 2f);

        CheckStageComplete();
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
        if (_inQuest || _busy || _stageCompleted || npc == null || Time.time < _promptCooldownUntil) return;
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
        if (_inQuest || npc == null || npc.questPrefab == null) return;

        _inQuest = true;
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
        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;
        vc_QuestRoom.OnAnyQuestComplete += OnQuestDoneAcknowledged;

        // Standing inside the room's trigger starts the quest + timer.
        MovePlayer(roomPos + (Vector3)roomEntryOffset);
    }

    private void OnQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (!_inQuest) return;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;

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

        if (passed)
        {
            // Back to town only once the player taps the "Quest done" popup. The long cap
            // is just a safety net in case the popup never shows.
            float until = Time.time + 60f;
            while (!_questDoneAcknowledged && Time.time < until) yield return null;
        }
        else
        {
            // A failed quest still counts as this NPC's one attempt - save it for Continue.
            room?.WriteAttemptCheckpoint();
            vc_QuestHUD.Instance?.ShowFeedbackTimed("Time's up!", 2f);
            yield return new WaitForSeconds(2f);
        }

        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;

        vc_TownNPC npc = _activeNpc;
        if (_activeRoom != null) Destroy(_activeRoom);
        _activeRoom = null;
        _activeNpc = null;
        _inQuest = false;

        if (npc != null)
        {
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
            MovePlayer(npc.transform.position + new Vector3(0f, -1f, 0f));
        }

        _promptCooldownUntil = Time.time + 1f;
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        vc_QuestTimer.Instance?.SetStarsVisible(false);
        vc_RunProgressUI.Instance?.SetBannerVisible(true);
        RefreshHomes();
        RefreshHud();
        vc_TownFlow.RaiseProgressChanged();

        if (AttemptedCount >= BuiltQuestCount) CheckStageComplete();
        else if (_questsToday >= questsPerDay) EndDay();
    }

    // ── Days ──────────────────────────────────────────────────────────────

    private void ResetDayTally()
    {
        _starsToday = _questsToday = _passedToday = 0;
        _usageToday.Clear();
    }

    private void EndDay(System.Action afterSleep = null)
    {
        _busy = true;
        string kinds = DescribeKinds(_usageToday);
        string body =
            $"People helped today: {_questsToday}\n" +
            $"Stars earned: {_starsToday}\n\n" +
            (string.IsNullOrEmpty(kinds) ? "You took it one step at a time today." : $"Today you leaned on {kinds} skills.");

        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        string dream = nightDreams != null && nightDreams.Length > 0 ? nightDreams[(_day - 1) % nightDreams.Length] : null;

        System.Action wake = () =>
        {
            _day++;
            ResetDayTally();
            _busy = false;
            RefreshHud();
            vc_TownFlow.RaiseProgressChanged();
            if (afterSleep != null) afterSleep();
            else vc_QuestHUD.Instance?.ShowFeedbackTimed($"Day {_day}", 2f);
        };

        if (ui != null) ui.ShowDaySummary($"{stageTitle} - Day {_day} done", body, dream, wake);
        else vc_DialogPanel.Instance?.ShowMessage($"Day {_day} done", body, () => wake());
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
        vc_QuestHUD.Instance.ShowQuestInfo(
            $"Day {_day}  -  Helped {AttemptedCount} / {BuiltQuestCount}",
            stageTitle,
            "Find people with a ! above their head and help them.",
            new[] { "Talk to people with a !", "Visit the skill shrine to swap skills" });
    }

    private void RefreshHomes()
    {
        float progress = BuiltQuestCount > 0 ? (float)AttemptedCount / BuiltQuestCount : 0f;
        foreach (vc_TownHome home in _homes) home.SetProgress(progress);
    }

    private void CheckStageComplete()
    {
        if (_stageCompleted || BuiltQuestCount == 0 || AttemptedCount < BuiltQuestCount) return;
        _stageCompleted = true;

        string line = string.IsNullOrEmpty(completeLine) ? "You've helped everyone here. Time to grow up!" : completeLine;
        System.Action finish = () =>
        {
            _busy = true;
            vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} done!", line, () => vc_TownFlow.Instance?.LoadNextTown());
        };

        // Close out the last day first if anything happened on it.
        if (_questsToday > 0) EndDay(finish);
        else finish();
    }

    // ── Debug menu hooks ──────────────────────────────────────────────────

    /// <summary>Marks every built quest in this town as attempted and finishes the stage.</summary>
    public void DebugCompleteStage()
    {
        foreach (vc_TownNPC npc in _npcs)
        {
            if (!npc.HasQuest) continue;
            vc_TownFlow.MarkQuestDone(npc.QuestId);
            npc.SetAttempted(true);
        }
        RefreshHomes();
        RefreshHud();
        vc_TownFlow.RaiseProgressChanged();
        CheckStageComplete();
    }

    /// <summary>Ends the current day right away (summary + dream).</summary>
    public void DebugEndDay()
    {
        if (_inQuest || _busy) return;
        EndDay();
    }

    /// <summary>First NPC whose quest is built and not yet attempted, or null.</summary>
    public vc_TownNPC DebugNextOpenNpc()
    {
        foreach (vc_TownNPC npc in _npcs)
            if (npc.HasQuest && !npc.Attempted) return npc;
        return null;
    }

    public void DebugTeleportTo(vc_TownNPC npc)
    {
        if (npc == null || _inQuest) return;
        _promptCooldownUntil = Time.time + 0.5f;
        MovePlayer(npc.transform.position + new Vector3(0f, -1.2f, 0f));
    }

    /// <summary>Clears this town's progress so every quest can be played again.</summary>
    public void DebugResetProgress()
    {
        foreach (vc_TownNPC npc in _npcs) npc.SetAttempted(false);
        vc_TownFlow.ResetRun();
        _stageCompleted = false;
        _day = 1;
        ResetDayTally();
        RefreshHomes();
        RefreshHud();
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
    }
}
