using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_FallenSparrowQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_CatAISystem birdAI;
    [SerializeField] private Transform birdTransform;
    [SerializeField] private GameObject vetNPCObject;
    [SerializeField] private vc_NPCController vetNPC;
    [SerializeField] private vc_FloatingMarker mainMarker_Bird;
    [SerializeField] private vc_FloatingMarker poiMarker_VetSpot;

    private Transform _playerTransform;
    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool sosUsed = false;
    private bool questDone = false;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        questStarted = true;
        sosUsed = false;
        questDone = false;
        vc_QuestHUD.Instance?.ForceHideFeedback();

        if (vetNPCObject != null) vetNPCObject.SetActive(false);

        // Fall back to the bird AI's transform so the vet (summon path) has a walk target.
        if (birdTransform == null && birdAI != null) birdTransform = birdAI.transform;

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Fallen Sparrow",
            "A small bird is hurt and needs your help. Treat its injuries.",
            new[] { "Calm the bird, or call for help", "Tend to its injuries" }
        );
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        if ((skill.SkillData.HasTag("summon") || skill.SkillData.HasTag("command")) && !sosUsed)
        {
            sosUsed = true;
            vc_QuestHUD.Instance?.CheckObjective(0);
            poiMarker_VetSpot?.Hide();
            vc_FloatingMessage.Instance?.Show("Vet is on the way!");
            if (vetNPCObject != null) vetNPCObject.SetActive(true);
            if (vetNPC != null && birdTransform != null) vetNPC.WalkToPoint(birdTransform.position);
            StartCoroutine(WaitVetThenComplete());
            handled = true;
        }
        // Direct resolve: any one soothing skill treats the bird on press.
        if (skill.SkillData.HasTag("attract") || skill.SkillData.HasTag("charm") || skill.SkillData.HasTag("heal"))
        {
            vc_QuestHUD.Instance?.CheckObjective(0);
            vc_FloatingMessage.Instance?.Show("You tend to the bird's injuries.");
            CompleteQuest();
            handled = true;
        }
        if (!handled) vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private IEnumerator WaitVetThenComplete()
    {
        if (vetNPC == null) yield break;
        yield return new WaitUntil(vetNPC.HasReachedDestination);
        yield return new WaitForSeconds(2f);
        CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        vc_QuestHUD.Instance?.ForceHideFeedback();
        vc_FloatingMessage.Instance?.Show("The bird is safe!");
        vc_QuestHUD.Instance?.CheckObjective(1);
        mainMarker_Bird?.Hide();
        poiMarker_VetSpot?.Hide();
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
