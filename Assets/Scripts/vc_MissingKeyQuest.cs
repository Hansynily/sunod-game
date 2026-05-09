using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_MissingKeyQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private GameObject frontDoor;
    [SerializeField] private BoxCollider2D frontDoorCollider;
    [SerializeField] private GameObject key;
    [SerializeField] private GameObject keyObject;
    [SerializeField] private GameObject[] xrayTargets;
    [SerializeField] private vc_NPCController friendNPC;
    [SerializeField] private GameObject friendNPCObject;
    [SerializeField] private float lockpickRange = 1.5f;
    [SerializeField] private float lockpickHoldTime = 5f;
    [SerializeField] private vc_XrayEffect xrayEffect;

    private Transform _playerTransform;
    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool questDone = false;
    private bool sosUsed = false;
    private bool xrayDone = false;
    private bool keyPickedUp = false;
    private float keyPickupGraceTimer = 0f;
    private float lockpickTimer = 0f;

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
        questDone = false;
        sosUsed = false;
        xrayDone = false;
        keyPickedUp = false;
        keyPickupGraceTimer = 0f;
        lockpickTimer = 0f;
        vc_QuestHUD.Instance?.HideFeedback();

        if (friendNPCObject != null) friendNPCObject.SetActive(false);

        ResetXrayState();
        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (keyPickupGraceTimer > 0f) keyPickupGraceTimer -= Time.deltaTime;

        UpdateKeyPickup();
        UpdateDoorUnlockWithKey();
        UpdateLockpickProgress();
    }

    private void UpdateLockpickProgress()
    {
        if (!questStarted || questDone || keyPickedUp || !IsHoldingSkill<vc_LockpickSkill>())
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (_playerTransform == null || frontDoor == null)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (Vector3.Distance(_playerTransform.position, frontDoor.transform.position) >= lockpickRange)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        lockpickTimer += Time.deltaTime;
        vc_QuestHUD.Instance?.ShowFeedback($"Lockpicking... {Mathf.Floor(lockpickTimer)}s");

        if (lockpickTimer >= lockpickHoldTime)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill is vc_XraySkill && !xrayDone)
        {
            ActivateXray();
            xrayDone = true;
            return;
        }

        if (skill is vc_SOSSkill && !sosUsed)
        {
            sosUsed = true;
            if (friendNPCObject != null) friendNPCObject.SetActive(true);
            if (friendNPC != null && frontDoor != null) friendNPC.WalkToPoint(frontDoor.transform.position);
            StartCoroutine(WaitThenUnlockDoor());
        }
    }

    private IEnumerator WaitThenUnlockDoor()
    {
        if (friendNPC == null) yield break;

        yield return new WaitUntil(friendNPC.HasReachedDestination);
        if (!keyPickedUp)
        {
            vc_FloatingMessage.Instance?.Show("Key picked up!");
            if (keyObject != null) keyObject.SetActive(false);
            keyPickedUp = true;
        }

        CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();

        if (frontDoor != null) frontDoor.SetActive(false);
        if (frontDoorCollider != null) frontDoorCollider.enabled = false;

        _questRoom?.OnQuestComplete();
    }

    private void ActivateXray()
    {
        if (xrayEffect != null) { xrayEffect.ActivateXray(); return; }
        SetXrayAlpha(0.25f);
        if (key != null) key.SetActive(true);
    }

    private void ResetXrayState()
    {
        if (xrayEffect != null) { xrayEffect.DeactivateXray(); return; }
        SetXrayAlpha(1f);
        if (key != null) key.SetActive(false);
    }

    private void UpdateKeyPickup()
    {
        if (!questStarted || questDone || !xrayDone || keyPickedUp || _playerTransform == null) return;

        Transform keyTransform = keyObject != null ? keyObject.transform : key != null ? key.transform : null;
        if (keyTransform == null) return;

        Collider2D keyCollider = keyTransform.GetComponent<Collider2D>();
        float pickupRange = 0.75f;
        if (keyCollider != null && !keyCollider.isTrigger)
            pickupRange = Mathf.Max(pickupRange, keyCollider.bounds.extents.magnitude);

        if (Vector2.Distance(_playerTransform.position, keyTransform.position) > pickupRange) return;

        vc_QuestHUD.Instance?.HideFeedback();
        vc_FloatingMessage.Instance?.Show("Key picked up!");

        if (keyObject != null) keyObject.SetActive(false);
        else if (key != null) key.SetActive(false);

        keyPickedUp = true;
        keyPickupGraceTimer = 3f;
    }

    private void UpdateDoorUnlockWithKey()
    {
        if (!questStarted || questDone || !keyPickedUp || _playerTransform == null || frontDoor == null) return;
        if (keyPickupGraceTimer > 0f) return;
        if (Vector2.Distance(_playerTransform.position, frontDoor.transform.position) >= lockpickRange) return;

        vc_FloatingMessage.Instance?.Show("Door unlocked!");
        CompleteQuest();
    }

    private void SetXrayAlpha(float alpha)
    {
        if (xrayTargets == null) return;
        for (int i = 0; i < xrayTargets.Length; i++)
        {
            if (xrayTargets[i] == null) continue;
            SpriteRenderer sr = xrayTargets[i].GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
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
