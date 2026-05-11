using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_FallenSparrowQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_CatAISystem birdAI;
    [SerializeField] private Transform birdTransform;
    [SerializeField] private GameObject vetNPCObject;
    [SerializeField] private vc_NPCController vetNPC;
    [SerializeField] private float charmRange = 1.5f;
    [SerializeField] private float healHoldTime = 5f;
    [SerializeField] private vc_FloatingMarker mainMarker_Bird;
    [SerializeField] private vc_FloatingMarker poiMarker_VetSpot;

    private Transform _playerTransform;
    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool charmDone = false;
    private bool sosUsed = false;
    private bool questDone = false;
    private float healTimer = 0f;

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
        charmDone = false;
        sosUsed = false;
        questDone = false;
        healTimer = 0f;
        vc_QuestHUD.Instance?.ForceHideFeedback();

        if (vetNPCObject != null) vetNPCObject.SetActive(false);

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Fallen Sparrow",
            "A small bird is hurt and needs your help. Treat its injuries.",
            new[] { "Calm the bird, or call for help", "Tend to its injuries" }
        );
    }

    private void Update()
    {
        if (!questStarted || questDone)
        {
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (!charmDone && vc_SkillManager.Instance.IsHoldingTag("attract") && _playerTransform != null && birdTransform != null)
        {
            if (Vector3.Distance(_playerTransform.position, birdTransform.position) < charmRange && birdAI != null)
            {
                birdAI.StartMovingToPlayer();
                charmDone = true;
                vc_FloatingMessage.Instance?.Show("The bird is warming up to you...");
                vc_QuestHUD.Instance?.CheckObjective(0);
            }
        }

        if (!charmDone)
        {
            healTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (!vc_SkillManager.Instance.IsHoldingTag("heal") || birdAI == null || !birdAI.HasReachedPlayer())
        {
            healTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        healTimer += Time.deltaTime;

        int remaining = Mathf.Max(0, Mathf.CeilToInt(healHoldTime - healTimer) - 1);
        vc_QuestHUD.Instance?.ShowFeedback($"Treating the bird... {remaining}s");

        if (healTimer >= healHoldTime)
        {
            vc_QuestHUD.Instance?.HideFeedback();
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        if (skill.SkillData.HasTag("summon") && !sosUsed)
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
        // attract and heal are hold-based in Update — pressing them is valid, no immediate event effect
        if (skill.SkillData.HasTag("attract") || skill.SkillData.HasTag("heal")) handled = true;
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
