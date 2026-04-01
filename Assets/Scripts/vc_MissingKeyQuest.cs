using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_MissingKeyQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject frontDoor;
    [SerializeField] private BoxCollider2D frontDoorCollider;
    [SerializeField] private GameObject key;
    [SerializeField] private GameObject keyObject;
    [SerializeField] private GameObject[] xrayTargets;
    [SerializeField] private vc_NPCController friendNPC;
    [SerializeField] private GameObject friendNPCObject;
    [SerializeField] private vc_QuestRoom questRoom;
    [SerializeField] private float lockpickRange = 1.5f;
    [SerializeField] private float lockpickHoldTime = 5f;
    [SerializeField] private vc_XrayEffect xrayEffect;
    [SerializeField] private vc_FloatingMessage floatingMessage;
    [SerializeField] private TextMeshProUGUI lockpickFeedbackText;

    private bool questStarted = false;
    private bool questDone = false;
    private bool sosUsed = false;
    private bool xrayDone = false;
    private bool keyPickedUp = false;
    private float keyPickupGraceTimer = 0f;
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
        sosUsed = false;
        xrayDone = false;
        keyPickedUp = false;
        keyPickupGraceTimer = 0f;
        lockpickTimer = 0f;
        ClearLockpickFeedback();

        if (friendNPCObject != null)
        {
            friendNPCObject.SetActive(false);
        }

        ResetXrayState();
        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (keyPickupGraceTimer > 0f)
        {
            keyPickupGraceTimer -= Time.deltaTime;
        }

        UpdateKeyPickup();
        UpdateDoorUnlockWithKey();
        UpdateLockpickProgress();
    }

    private void UpdateLockpickProgress()
    {
        if (!questStarted || questDone || keyPickedUp || !IsHoldingSkill<vc_LockpickSkill>())
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            return;
        }

        if (playerTransform == null || frontDoor == null)
        {
            lockpickTimer = 0f;
            ClearLockpickFeedback();
            return;
        }

        float playerDistance = Vector3.Distance(playerTransform.position, frontDoor.transform.position);
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
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_XraySkill && !xrayDone)
        {
            ActivateXray();
            xrayDone = true;
            Debug.Log("X-Ray complete");
            return;
        }

        if (skill is vc_SOSSkill && !sosUsed)
        {
            sosUsed = true;

            if (friendNPCObject != null)
            {
                friendNPCObject.SetActive(true);
            }

            if (friendNPC != null && frontDoor != null)
            {
                friendNPC.WalkToPoint(frontDoor.transform.position);
            }

            StartCoroutine(WaitThenUnlockDoor());
        }
    }

    private IEnumerator WaitThenUnlockDoor()
    {
        if (friendNPC == null)
        {
            yield break;
        }

        yield return new WaitUntil(friendNPC.HasReachedDestination);
        if (!keyPickedUp)
        {
            ShowFloatingMessage("Key picked up!");

            if (keyObject != null)
            {
                keyObject.SetActive(false);
            }

            keyPickedUp = true;
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

        if (frontDoorCollider != null)
        {
            frontDoorCollider.enabled = false;
        }

        Debug.Log("Missing Key quest complete");
        questRoom?.OnQuestComplete();
    }

    private void ActivateXray()
    {
        if (xrayEffect != null)
        {
            xrayEffect.ActivateXray();
            return;
        }

        SetXrayAlpha(0.25f);
        if (key != null)
        {
            key.SetActive(true);
        }
    }

    private void ResetXrayState()
    {
        if (xrayEffect != null)
        {
            xrayEffect.DeactivateXray();
            return;
        }

        SetXrayAlpha(1f);
        if (key != null)
        {
            key.SetActive(false);
        }
    }

    private void UpdateKeyPickup()
    {
        if (!questStarted || questDone || !xrayDone || keyPickedUp || playerTransform == null)
        {
            return;
        }

        Transform keyTransform = keyObject != null ? keyObject.transform : key != null ? key.transform : null;
        if (keyTransform == null)
        {
            return;
        }

        Collider2D keyCollider = keyTransform.GetComponent<Collider2D>();
        float pickupRange = 0.75f;

        if (keyCollider != null && !keyCollider.isTrigger)
        {
            pickupRange = Mathf.Max(pickupRange, keyCollider.bounds.extents.magnitude);
        }

        if (Vector2.Distance(playerTransform.position, keyTransform.position) > pickupRange)
        {
            return;
        }

        ClearLockpickFeedback();
        ShowFloatingMessage("Key picked up!");

        if (keyObject != null)
        {
            keyObject.SetActive(false);
        }
        else if (key != null)
        {
            key.SetActive(false);
        }

        keyPickedUp = true;
        keyPickupGraceTimer = 3f;
    }

    private void UpdateDoorUnlockWithKey()
    {
        if (!questStarted || questDone || !keyPickedUp || playerTransform == null || frontDoor == null)
        {
            return;
        }

        if (keyPickupGraceTimer > 0f)
        {
            return;
        }

        if (Vector2.Distance(playerTransform.position, frontDoor.transform.position) >= lockpickRange)
        {
            return;
        }

        ShowFloatingMessage("Door unlocked!");
        CompleteQuest();
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
            lockpickFeedbackText.text = $"Lockpicking... {Mathf.Floor(lockpickTimer)}s";
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

    private void SetXrayAlpha(float alpha)
    {
        if (xrayTargets == null)
        {
            return;
        }

        for (int i = 0; i < xrayTargets.Length; i++)
        {
            if (xrayTargets[i] == null)
            {
                continue;
            }

            SpriteRenderer renderer = xrayTargets[i].GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
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
