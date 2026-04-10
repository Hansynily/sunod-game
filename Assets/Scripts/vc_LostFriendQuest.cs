using UnityEngine;

[DisallowMultipleComponent]
public class vc_LostFriendQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private vc_NPCController friendNPC;
    [SerializeField] private Transform friendTransform;
    [SerializeField] private vc_DirectionalArrow directionArrow;
    [SerializeField] private Transform classroomTransform;
    [SerializeField] private float charmRange = 1.5f;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private vc_QuestRoom activeQuestRoom;
    private bool questStarted = false;
    private bool trackDone = false;
    private bool charmActive = false;
    private bool questDone = false;

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

        if (directionArrow != null)
        {
            directionArrow.HideArrow();
        }

        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (!questStarted || questDone)
        {
            return;
        }

        if (!charmActive && IsHoldingSkill<vc_CharmSkill>() && playerTransform != null && friendTransform != null)
        {
            float playerDistance = Vector3.Distance(playerTransform.position, friendTransform.position);
            if (playerDistance < charmRange && friendNPC != null)
            {
                friendNPC.FollowTarget(playerTransform);
                charmActive = true;
                ShowFloatingMessage("Your friend now follows you.");
            }
        }

        if (!charmActive || friendTransform == null || classroomTransform == null)
        {
            return;
        }

        if (Vector3.Distance(friendTransform.position, classroomTransform.position) < 1.5f)
        {
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_TrackSkill && !trackDone)
        {
            if (directionArrow != null)
            {
                directionArrow.ShowArrow();
            }

            ShowFloatingMessage("Path found!");
            trackDone = true;
            Debug.Log("Track complete");
        }
    }

    private void CompleteQuest()
    {
        if (questDone)
        {
            return;
        }

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        ShowFloatingMessage("Friend guided to class!");
        Debug.Log("Lost Friend quest complete");
        activeQuestRoom?.OnQuestComplete();

        if (friendNPC != null)
        {
            Destroy(friendNPC.gameObject);
        }
    }

    private void SubscribeToSkillManager()
    {
        if (skillManager == null)
        {
            return;
        }

        skillManager.SkillUsed -= HandleSkillUsed;
        skillManager.SkillUsed += HandleSkillUsed;
    }

    private void UnsubscribeFromSkillManager()
    {
        if (skillManager != null)
        {
            skillManager.SkillUsed -= HandleSkillUsed;
        }
    }

    private bool IsHoldingSkill<TSkill>() where TSkill : vc_PlayerSkill
    {
        if (skillManager == null)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            if (skillManager.GetSlotSkill(i) is TSkill && skillManager.IsSlotHeld(i))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowFloatingMessage(string message)
    {
        if (floatingMessage != null)
        {
            floatingMessage.Show(message);
        }
    }
}
