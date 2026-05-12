using UnityEngine;

/// <summary>
/// Quest logic for L4 Quest 2 — Club Activity.
///
/// Combo 1: attract × 6
///   - Use attract near a student → student follows player (toggle: use again to stop)
///   - Lead all 6 students to the club room entrance to complete the quest
///
/// Combo 2: persuade × 6 (requires flyers)
///   - Pick up Prop_Flyers by walking within 1f of it
///   - Use persuade near a student → student walks directly to club room
///   - 6 students arrived → quest complete
/// </summary>
[DisallowMultipleComponent]
public class vc_ClubActivityQuest : MonoBehaviour, vc_IQuestLogic
{
    private const int StudentCount = 6;
    private const float ProximityRange = 2f;
    private const float FlyersPickupRange = 1f;

    [SerializeField] private vc_NPC_Follow[] studentFollows;
    [SerializeField] private vc_NPC_GoTo[] studentGoTos;
    [SerializeField] private Transform[] studentTransforms;
    [SerializeField] private Transform clubRoomTransform;
    [SerializeField] private Transform flyersTransform;
    [SerializeField] private vc_FloatingMarker[] studentMarkers;
    [SerializeField] private vc_FloatingMarker poiMarker_Flyers;
    [SerializeField] private float arrivalDistance = 1.5f;
    [SerializeField] private int requiredCount = 6;

    private vc_QuestRoom _questRoom;
    private Transform _playerTransform;

    private bool hasFlyers = false;
    private int studentsRecruited = 0;
    private bool[] studentDone;
    private bool[] studentFollowing;
    private bool questDone = false;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    /// <inheritdoc/>
    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        _questRoom = questRoom;

        hasFlyers = false;
        studentsRecruited = 0;
        questDone = false;

        studentDone = new bool[StudentCount];
        studentFollowing = new bool[StudentCount];

        vc_QuestHUD.Instance?.ForceHideFeedback();
        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Recruit 6 students (0/6)",
            "Club Activity",
            "The MedTeam Club needs 6 new members for their upcoming event. Help recruit.",
            new[] { "Recruit 6 students (0/6)" });
    }

    private void Update()
    {
        if (questDone) return;

        // Flyers proximity pickup
        if (!hasFlyers && _playerTransform != null && flyersTransform != null &&
            Vector2.Distance(_playerTransform.position, flyersTransform.position) <= FlyersPickupRange)
        {
            hasFlyers = true;
            poiMarker_Flyers?.Hide();
            vc_FloatingMessage.Instance?.Show("Flyers picked up.");
        }

        // Check if any following student has reached the club room
        for (int i = 0; i < StudentCount; i++)
        {
            if (studentDone[i] || !studentFollowing[i]) continue;
            if (studentTransforms == null || i >= studentTransforms.Length || studentTransforms[i] == null) continue;
            if (clubRoomTransform == null) continue;

            if (Vector2.Distance(studentTransforms[i].position, clubRoomTransform.position) <= arrivalDistance)
            {
                studentFollowing[i] = false;
                studentFollows[i]?.Deactivate();
                MarkStudentDone(i);
            }
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (questDone || skill == null) return;

        bool handled = false;

        // attract: toggle nearest eligible student within range
        if (skill.SkillData.HasTag("attract"))
        {
            int nearest = FindNearestEligibleStudent();
            if (nearest >= 0)
            {
                if (!studentFollowing[nearest])
                {
                    studentFollowing[nearest] = true;
                    studentFollows[nearest]?.Activate();
                    vc_FloatingMessage.Instance?.Show("Student is following you.");
                }
                else
                {
                    studentFollowing[nearest] = false;
                    studentFollows[nearest]?.Deactivate();
                    vc_FloatingMessage.Instance?.Show("Student stopped following.");
                }
                handled = true;
            }
        }

        // persuade: send nearest eligible student to club room (requires flyers)
        if (skill.SkillData.HasTag("persuade") && hasFlyers)
        {
            int nearest = FindNearestEligibleStudent();
            if (nearest >= 0)
            {
                // Mark done immediately to prevent double-trigger
                studentDone[nearest] = true;
                if (studentFollowing[nearest])
                {
                    studentFollowing[nearest] = false;
                    studentFollows[nearest]?.Deactivate();
                }

                int capturedIndex = nearest;
                vc_NPC_GoTo goTo = studentGoTos[capturedIndex];
                if (goTo != null && clubRoomTransform != null)
                {
                    goTo.OnArrived += OnPersuadedStudentArrived(capturedIndex);
                    goTo.GoTo(clubRoomTransform);
                }
                else
                {
                    // No GoTo component — count as arrived immediately
                    studentsRecruited++;
                    studentMarkers[capturedIndex]?.Hide();
                    UpdateCounter();
                    if (studentsRecruited >= requiredCount) CompleteQuest();
                }

                vc_FloatingMessage.Instance?.Show("Student heading to the club room.");
                handled = true;
            }
        }

        if (!handled)
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    // Returns a captured-index Action for the OnArrived event subscription
    private System.Action OnPersuadedStudentArrived(int index)
    {
        System.Action handler = null;
        handler = () =>
        {
            if (studentGoTos != null && index < studentGoTos.Length && studentGoTos[index] != null)
                studentGoTos[index].OnArrived -= handler;

            studentsRecruited++;
            studentMarkers?[index]?.Hide();
            UpdateCounter();
            if (studentsRecruited >= requiredCount) CompleteQuest();
        };
        return handler;
    }

    private void MarkStudentDone(int index)
    {
        studentDone[index] = true;
        studentsRecruited++;
        studentMarkers?[index]?.Hide();
        UpdateCounter();
        if (studentsRecruited >= requiredCount) CompleteQuest();
    }

    private int FindNearestEligibleStudent()
    {
        if (_playerTransform == null || studentTransforms == null) return -1;

        int nearest = -1;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < StudentCount; i++)
        {
            if (studentDone[i]) continue;
            if (i >= studentTransforms.Length || studentTransforms[i] == null) continue;

            float dist = Vector2.Distance(_playerTransform.position, studentTransforms[i].position);
            if (dist <= ProximityRange && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = i;
            }
        }

        return nearest;
    }

    private void UpdateCounter()
    {
        string text = $"Recruit 6 students ({studentsRecruited}/6)";
        vc_QuestHUD.Instance?.SetCounter(text);
        vc_QuestHUD.Instance?.CheckObjective(0);
    }

    private void CompleteQuest()
    {
        if (questDone) return;
        questDone = true;

        UnsubscribeFromSkillManager();
        poiMarker_Flyers?.Hide();

        if (studentMarkers != null)
        {
            foreach (vc_FloatingMarker marker in studentMarkers)
                marker?.Hide();
        }

        vc_FloatingMessage.Instance?.Show("All students recruited!");
        _questRoom?.OnQuestComplete();
    }

    private void SubscribeToSkillManager()
    {
        if (vc_SkillManager.Instance == null) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
    }

    private void UnsubscribeFromSkillManager()
    {
        if (vc_SkillManager.Instance != null)
            vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
    }
}
