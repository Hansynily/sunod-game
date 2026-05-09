using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SlipperyWayQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private GameObject wetFloor;
    [SerializeField] private GameObject safeRouteHighlight;
    [SerializeField] private GameObject janitorNPCObject;
    [SerializeField] private vc_NPCController janitorNPC;
    [SerializeField] private Transform janitorWalkTarget;

    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool arrowDone = false;
    private bool sosUsed = false;
    private bool questDone = false;

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        questStarted = true;
        arrowDone = false;
        sosUsed = false;
        questDone = false;

        if (safeRouteHighlight != null) safeRouteHighlight.SetActive(false);
        if (wetFloor != null) wetFloor.SetActive(false);

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Slippery Way",
            "The hallway floor is slippery and dangerous. Find a safe way through.",
            new[] { "Find a safe path through" }
        );
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        if (skill.SkillData.HasTag("navigate") && !arrowDone)
        {
            if (safeRouteHighlight != null) safeRouteHighlight.SetActive(true);
            vc_FloatingMessage.Instance?.Show("Safe path found!");
            arrowDone = true;
            StartCoroutine(WaitThenComplete());
            handled = true;
        }
        if (skill.SkillData.HasTag("summon") && !sosUsed)
        {
            sosUsed = true;
            vc_FloatingMessage.Instance?.Show("Janitor is on the way!");
            if (wetFloor != null) wetFloor.SetActive(true);
            if (janitorNPCObject != null) janitorNPCObject.SetActive(true);
            StartCoroutine(WaitJanitorThenComplete());
            handled = true;
        }
        if (!handled) vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private IEnumerator WaitThenComplete()
    {
        yield return new WaitForSeconds(1.5f);
        CompleteQuest();
    }

    private IEnumerator WaitJanitorThenComplete()
    {
        if (janitorNPC == null || janitorWalkTarget == null) yield break;
        janitorNPC.WalkToPoint(janitorWalkTarget.position);
        yield return new WaitUntil(janitorNPC.HasReachedDestination);
        vc_FloatingMessage.Instance?.Show("Floor is being cleaned...");
        yield return new WaitForSeconds(2f);
        CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        vc_FloatingMessage.Instance?.Show("Path is clear!");
        vc_QuestHUD.Instance?.CheckObjective(0);
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
