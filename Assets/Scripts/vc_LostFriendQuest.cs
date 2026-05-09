using UnityEngine;

[DisallowMultipleComponent]
public class vc_LostFriendQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_NPCController friendNPC;
    [SerializeField] private Transform friendTransform;
    [SerializeField] private Transform classroomTransform;
    [SerializeField] private float charmRange = 1.5f;

    private Transform _playerTransform;
    private vc_QuestRoom activeQuestRoom;
    private bool questStarted = false;
    private bool trackDone = false;
    private bool charmActive = false;
    private bool questDone = false;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        activeQuestRoom = questRoom;
        questStarted = true;
        trackDone = false;
        charmActive = false;
        questDone = false;

        vc_DirectionalArrow.Instance?.ClearTarget();
        vc_DirectionalArrow.Instance?.HideArrow();

        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (!questStarted || questDone) return;

        if (!charmActive && IsHoldingSkill<vc_CharmSkill>() && _playerTransform != null && friendTransform != null)
        {
            float dist = Vector3.Distance(_playerTransform.position, friendTransform.position);
            if (dist < charmRange && friendNPC != null)
            {
                friendNPC.FollowTarget(_playerTransform);
                charmActive = true;
                vc_FloatingMessage.Instance?.Show("Your friend now follows you.");
            }
        }

        if (!charmActive || friendTransform == null || classroomTransform == null) return;

        if (Vector3.Distance(friendTransform.position, classroomTransform.position) < 1.5f)
            CompleteQuest();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill is vc_TrackSkill && !trackDone)
        {
            if (classroomTransform != null)
            {
                vc_DirectionalArrow.Instance?.SetTarget(classroomTransform);
                vc_DirectionalArrow.Instance?.ShowArrow();
            }

            vc_FloatingMessage.Instance?.Show("Path found!");
            trackDone = true;
        }
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        vc_FloatingMessage.Instance?.Show("Friend guided to class!");

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        activeQuestRoom?.OnQuestComplete();

        if (friendNPC != null) Destroy(friendNPC.gameObject);
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

    private bool IsHoldingSkill<TSkill>() where TSkill : vc_PlayerSkill
    {
        if (vc_SkillManager.Instance == null) return false;
        for (int i = 0; i < 4; i++)
        {
            if (vc_SkillManager.Instance.GetSlotSkill(i) is TSkill && vc_SkillManager.Instance.IsSlotHeld(i))
                return true;
        }
        return false;
    }
}
