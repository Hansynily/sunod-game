using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One life-stage town (Elementary / High School / College). Owns the town's NPCs and
/// runs their quests: the quest room is spawned in a hidden area below the town, the
/// player is moved into it (the room's trigger starts the quest and its timer), and
/// when the timer ends - pass or fail - the player comes back next to the NPC.
/// One attempt per quest, same as the corridor. When every built quest has been
/// attempted, vc_TownFlow moves on to the next stage.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownStage : MonoBehaviour
{
    public static vc_TownStage Current { get; private set; }

    public string stageId = "elementary";
    public string stageTitle = "Elementary";
    [TextArea(2, 3)] public string introLine;
    [TextArea(2, 3)] public string completeLine;
    public Transform playerSpawn;
    [Tooltip("Hidden spot (far from the town) where quest rooms are spawned.")]
    public Transform questArea;
    [Tooltip("Where the player stands inside a 16x16 quest room, relative to the room's center.")]
    public Vector2 roomEntryOffset = new Vector2(0f, -6f);

    private vc_TownNPC[] _npcs = new vc_TownNPC[0];
    private vc_TownNPC _activeNpc;
    private GameObject _activeRoom;
    private bool _inQuest;
    private bool _questDoneAcknowledged;
    private bool _stageCompleted;
    private float _promptCooldownUntil;

    public bool InQuest => _inQuest;
    public int BuiltQuestCount => _npcs.Count(n => n.HasQuest);
    public int AttemptedCount => _npcs.Count(n => n.HasQuest && n.Attempted);

    private void Awake()
    {
        Current = this;
        _npcs = GetComponentsInChildren<vc_TownNPC>(true);
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
            npc.SetAttempted(vc_TownFlow.IsAttempted(npc.npcId));

        MovePlayer(playerSpawn != null ? playerSpawn.position : transform.position);
        RefreshHud();

        if (!string.IsNullOrEmpty(introLine))
            vc_DialogPanel.Instance?.ShowMessage(stageTitle, introLine, null);

        CheckStageComplete();
    }

    // ── NPC interaction ───────────────────────────────────────────────────

    public void OnNpcApproached(vc_TownNPC npc)
    {
        if (_inQuest || _stageCompleted || npc == null || Time.time < _promptCooldownUntil) return;
        if (vc_DialogPanel.Instance == null) return;

        if (!npc.HasQuest)
        {
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, "I might need your help another time!", null);
            return;
        }

        if (npc.Attempted)
        {
            string thanks = string.IsNullOrEmpty(npc.thanksLine) ? "Thanks again for your help!" : npc.thanksLine;
            vc_DialogPanel.Instance.ShowMessage(npc.npcName, thanks, null);
            return;
        }

        vc_DialogPanel.Instance.ShowChoice(
            npc.npcName,
            npc.askLine,
            null,
            "Help",
            "Later",
            onConfirm: () => StartQuest(npc),
            onCancel: () => _promptCooldownUntil = Time.time + 1.5f);
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
        }
        vc_QuestRoom.OnAnyQuestComplete -= OnQuestDoneAcknowledged;
        vc_QuestRoom.OnAnyQuestComplete += OnQuestDoneAcknowledged;

        // Standing inside the room's trigger starts the quest + timer.
        MovePlayer(roomPos + (Vector3)roomEntryOffset);
    }

    private void OnQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (!_inQuest) return;
        if (vc_QuestTimer.Instance != null) vc_QuestTimer.Instance.QuestEnded -= OnQuestEnded;
        StartCoroutine(ReturnAfterQuest(result != null && result.DidPassQuest));
    }

    private void OnQuestDoneAcknowledged() => _questDoneAcknowledged = true;

    private IEnumerator ReturnAfterQuest(bool passed)
    {
        if (passed)
        {
            // Wait for the player to close the "Quest done" popup (or give up after a while).
            float until = Time.time + 8f;
            while (!_questDoneAcknowledged && Time.time < until) yield return null;
        }
        else
        {
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
            vc_TownFlow.MarkAttempted(npc.npcId);
            npc.SetAttempted(true);
            MovePlayer(npc.transform.position + new Vector3(0f, -1f, 0f));
        }

        _promptCooldownUntil = Time.time + 1f;
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        RefreshHud();
        CheckStageComplete();
    }

    // ── Stage progress ────────────────────────────────────────────────────

    private void RefreshHud()
    {
        if (vc_QuestHUD.Instance == null) return;
        int built = BuiltQuestCount;
        vc_QuestHUD.Instance.ShowQuestInfo(
            $"Helped {AttemptedCount} / {built}",
            stageTitle,
            "Find people with a ! above their head and help them.",
            new[] { "Talk to people with a !" });
    }

    private void CheckStageComplete()
    {
        if (_stageCompleted || BuiltQuestCount == 0 || AttemptedCount < BuiltQuestCount) return;
        _stageCompleted = true;

        string line = string.IsNullOrEmpty(completeLine) ? "You've helped everyone here. Time to grow up!" : completeLine;
        vc_DialogPanel.Instance?.ShowMessage($"{stageTitle} done!", line, () => vc_TownFlow.Instance?.LoadNextTown());
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
