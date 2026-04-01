using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_FallenSparrowQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private vc_CatAISystem birdAI;
    [SerializeField] private Transform birdTransform;
    [SerializeField] private GameObject vetNPCObject;
    [SerializeField] private vc_NPCController vetNPC;
    [SerializeField] private vc_QuestRoom questRoom;
    [SerializeField] private float charmRange = 1.5f;
    [SerializeField] private float healHoldTime = 5f;
    [SerializeField] private TextMeshProUGUI healFeedbackText;
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private bool questStarted = false;
    private bool charmDone = false;
    private bool sosUsed = false;
    private bool questDone = false;
    private bool healStarted = false;
    private bool healCountdownVisible = false;
    private float healTimer = 0f;

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        questRoom = activeQuestRoom != null ? activeQuestRoom : questRoom;
        questStarted = true;
        charmDone = false;
        sosUsed = false;
        questDone = false;
        healStarted = false;
        healCountdownVisible = false;
        healTimer = 0f;
        ClearHealFeedback();

        if (vetNPCObject != null)
        {
            vetNPCObject.SetActive(false);
        }

        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (!questStarted || questDone)
        {
            ClearHealFeedback();
            return;
        }

        if (!charmDone && IsHoldingSkill<vc_CharmSkill>() && playerTransform != null && birdTransform != null)
        {
            float playerDistance = Vector3.Distance(playerTransform.position, birdTransform.position);
            if (playerDistance < charmRange && birdAI != null)
            {
                birdAI.StartMovingToPlayer();
                charmDone = true;
                ShowFloatingMessage("The bird is warming up to you...");
            }
        }

        if (!charmDone)
        {
            healTimer = 0f;
            healStarted = false;
            ClearHealFeedback();
            return;
        }

        if (!IsHoldingSkill<vc_HealSkill>() || birdAI == null || !birdAI.HasReachedPlayer())
        {
            healTimer = 0f;
            healStarted = false;
            ClearHealFeedback();
            return;
        }

        if (!healStarted)
        {
            healStarted = true;
        }

        healTimer += Time.deltaTime;
        UpdateHealFeedback();
        if (healTimer >= healHoldTime)
        {
            ClearHealFeedback();
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_SOSSkill && !sosUsed)
        {
            sosUsed = true;
            ShowFloatingMessage("Vet is on the way!");

            if (vetNPCObject != null)
            {
                vetNPCObject.SetActive(true);
            }

            if (vetNPC != null && birdTransform != null)
            {
                vetNPC.WalkToPoint(birdTransform.position);
            }

            StartCoroutine(WaitVetThenComplete());
        }
    }

    private IEnumerator WaitVetThenComplete()
    {
        if (vetNPC == null)
        {
            yield break;
        }

        yield return new WaitUntil(vetNPC.HasReachedDestination);
        yield return new WaitForSeconds(2f);
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
        ClearHealFeedback();
        ShowFloatingMessage("The bird is safe!");
        Debug.Log("Fallen Sparrow quest complete");
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

    private void UpdateHealFeedback()
    {
        int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(healHoldTime - healTimer) - 1);
        string countdownMessage = $"Treating the bird... {remainingSeconds}s";

        if (HasDedicatedHealFeedbackText())
        {
            healFeedbackText.text = countdownMessage;
            return;
        }

        if (floatingMessage != null)
        {
            healCountdownVisible = true;
            floatingMessage.Show(countdownMessage);
        }
    }

    private void ClearHealFeedback()
    {
        if (HasDedicatedHealFeedbackText())
        {
            healFeedbackText.text = string.Empty;
        }

        if (healCountdownVisible && floatingMessage != null)
        {
            floatingMessage.HideNow();
        }

        healCountdownVisible = false;
    }

    private bool HasDedicatedHealFeedbackText()
    {
        return healFeedbackText != null && (floatingMessage == null || healFeedbackText != floatingMessage.MessageText);
    }

    private void ShowFloatingMessage(string message)
    {
        healCountdownVisible = false;

        if (floatingMessage != null)
        {
            floatingMessage.Show(message);
        }
    }
}
