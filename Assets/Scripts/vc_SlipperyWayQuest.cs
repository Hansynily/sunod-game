using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SlipperyWayQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private GameObject wetFloor;
    [SerializeField] private GameObject safeRouteHighlight;
    [SerializeField] private GameObject janitorNPCObject;
    [SerializeField] private vc_NPCController janitorNPC;
    [SerializeField] private Transform janitorWalkTarget;
    [SerializeField] private vc_QuestRoom questRoom;
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private bool questStarted = false;
    private bool arrowDone = false;
    private bool sosUsed = false;
    private bool questDone = false;

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        questRoom = activeQuestRoom != null ? activeQuestRoom : questRoom;
        questStarted = true;
        arrowDone = false;
        sosUsed = false;
        questDone = false;

        if (safeRouteHighlight != null)
        {
            safeRouteHighlight.SetActive(false);
        }

        SubscribeToSkillManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_ArrowSkill && !arrowDone)
        {
            if (safeRouteHighlight != null)
            {
                safeRouteHighlight.SetActive(true);
            }

            ShowFloatingMessage("Safe path found!");
            arrowDone = true;
            Debug.Log("Safe path found");
            StartCoroutine(WaitThenComplete());
            return;
        }

        if (skill is vc_SOSSkill && !sosUsed)
        {
            sosUsed = true;
            ShowFloatingMessage("Janitor is on the way!");

            if (janitorNPCObject != null)
            {
                janitorNPCObject.SetActive(true);
            }

            StartCoroutine(WaitJanitorThenComplete());
        }
    }

    private IEnumerator WaitThenComplete()
    {
        yield return new WaitForSeconds(1.5f);
        CompleteQuest();
    }

    private IEnumerator WaitJanitorThenComplete()
    {
        if (janitorNPC == null || janitorWalkTarget == null)
        {
            yield break;
        }

        janitorNPC.WalkToPoint(janitorWalkTarget.position);
        yield return new WaitUntil(janitorNPC.HasReachedDestination);
        ShowFloatingMessage("Floor is being cleaned...");
        yield return new WaitForSeconds(2f);

        if (wetFloor != null)
        {
            wetFloor.SetActive(false);
        }

        CompleteQuest();
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
        ShowFloatingMessage("Path is clear!");
        Debug.Log("Slippery Way quest complete");
        questRoom?.OnQuestComplete();
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

    private void ShowFloatingMessage(string message)
    {
        if (floatingMessage != null)
        {
            floatingMessage.Show(message);
        }
    }
}
