using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_CatMedicineQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject safe;
    [SerializeField] private Transform catTransform;
    [SerializeField] private BoxCollider2D safeCollider;
    [SerializeField] private vc_QuestRoom questRoom;
    [SerializeField] private float lockpickRange = 1.5f;
    [SerializeField] private float lockpickHoldTime = 5f;
    [SerializeField] private float medicineDeliveryRange = 1.5f;
    [SerializeField] private TextMeshProUGUI lockpickFeedbackText;
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private bool questStarted = false;
    private bool questDone = false;
    private bool moldingDone = false;
    private bool safeOpen = false;
    private bool medicinePickedUp = false;
    private float medicinePickupGraceTimer = 0f;
    private float lockpickTimer = 0f;

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        questRoom = activeQuestRoom != null ? activeQuestRoom : questRoom;
        questStarted = true;
        questDone = false;
        moldingDone = false;
        safeOpen = false;
        medicinePickedUp = false;
        medicinePickupGraceTimer = 0f;
        lockpickTimer = 0f;
        ClearLockpickFeedback();

        if (catTransform == null)
        {
            GameObject catObject = GameObject.Find("Cat");
            if (catObject != null)
            {
                catTransform = catObject.transform;
            }
        }

        if (safeCollider != null)
        {
            safeCollider.enabled = true;
        }

        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (medicinePickupGraceTimer > 0f)
        {
            medicinePickupGraceTimer -= Time.deltaTime;
        }

        UpdateSafePickup();
        UpdateMedicineDelivery();
        UpdateLockpickProgress();
    }

    private void UpdateLockpickProgress()
    {
        if (!questStarted || questDone || safeOpen || !IsHoldingSkill<vc_LockpickSkill>())
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            return;
        }

        if (playerTransform == null || safe == null)
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            return;
        }

        float playerDistance = Vector3.Distance(playerTransform.position, safe.transform.position);
        if (playerDistance >= lockpickRange)
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            return;
        }

        lockpickTimer += Time.deltaTime;
        UpdateLockpickFeedback();
        if (lockpickTimer >= lockpickHoldTime)
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            OpenSafe();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_MoldingSkill && !moldingDone)
        {
            moldingDone = true;
            Debug.Log("Mold complete - key created");
            StartCoroutine(ShowMoldPopupThenComplete());
        }
    }

    private IEnumerator ShowMoldPopupThenComplete()
    {
        ShowFloatingMessage("Key created!");
        yield return new WaitForSeconds(1.5f);
        OpenSafe();
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

        if (safeCollider != null)
        {
            safeCollider.enabled = false;
        }

        Debug.Log("Cat's Medicine quest complete");
        questRoom?.OnQuestComplete();
    }

    private void UpdateSafePickup()
    {
        if (!questStarted || questDone || !safeOpen || medicinePickedUp || playerTransform == null || safe == null)
        {
            return;
        }

        Collider2D pickupCollider = safe.GetComponent<Collider2D>();
        float pickupRange = 0.75f;

        if (pickupCollider != null)
        {
            pickupRange = Mathf.Max(pickupRange, pickupCollider.bounds.extents.magnitude);
        }

        if (Vector2.Distance(playerTransform.position, safe.transform.position) > pickupRange)
        {
            return;
        }

        ShowFloatingMessage("You successfully got the medicine!");
        medicinePickedUp = true;
        medicinePickupGraceTimer = 3f;
    }

    private void UpdateMedicineDelivery()
    {
        if (!questStarted || questDone || !medicinePickedUp || playerTransform == null || catTransform == null)
        {
            return;
        }

        if (medicinePickupGraceTimer > 0f)
        {
            return;
        }

        if (Vector2.Distance(playerTransform.position, catTransform.position) > medicineDeliveryRange)
        {
            return;
        }

        ShowFloatingMessage("Medicine delivered!");
        CompleteQuest();
    }

    private void OpenSafe()
    {
        if (safeOpen)
        {
            return;
        }

        safeOpen = true;

        if (safeCollider != null)
        {
            safeCollider.enabled = false;
        }

        ShowFloatingMessage("Safe opened!");
    }

    private void UpdateLockpickFeedback()
    {
        if (lockpickFeedbackText != null)
        {
            if (IsFloatingMessageUsingLockpickText())
            {
                return;
            }

            lockpickFeedbackText.gameObject.SetActive(true);
            int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(lockpickHoldTime - lockpickTimer) - 1);
            lockpickFeedbackText.text = $"Lockpicking... {remainingSeconds}s";
        }
    }

    private void ClearLockpickFeedback()
    {
        if (lockpickFeedbackText != null)
        {
            if (IsFloatingMessageUsingLockpickText())
            {
                return;
            }

            lockpickFeedbackText.text = string.Empty;
            lockpickFeedbackText.gameObject.SetActive(false);
        }
    }

    private void ShowFloatingMessage(string message)
    {
        if (floatingMessage != null)
        {
            floatingMessage.gameObject.SetActive(true);
            floatingMessage.Show(message);
        }
        else
        {
            Debug.Log(message);
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

    private bool IsFloatingMessageUsingLockpickText()
    {
        return floatingMessage != null
            && floatingMessage.IsShowing
            && floatingMessage.MessageText == lockpickFeedbackText;
    }
}
