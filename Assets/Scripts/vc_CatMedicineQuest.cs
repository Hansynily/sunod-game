using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_CatMedicineQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private GameObject safe;
    [SerializeField] private Transform catTransform;
    [SerializeField] private BoxCollider2D safeCollider;
    [SerializeField] private float lockpickRange = 1.5f;
    [SerializeField] private float lockpickHoldTime = 5f;
    [SerializeField] private float medicineDeliveryRange = 1.5f;
    [SerializeField] private vc_FloatingMarker mainMarker_Safe;
    [SerializeField] private vc_FloatingMarker mainMarker_Cat;

    private Transform _playerTransform;
    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool questDone = false;
    private bool moldingDone = false;
    private bool safeOpen = false;
    private bool medicinePickedUp = false;
    private float medicinePickupGraceTimer = 0f;
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
        moldingDone = false;
        safeOpen = false;
        medicinePickedUp = false;
        medicinePickupGraceTimer = 0f;
        lockpickTimer = 0f;
        vc_QuestHUD.Instance?.ForceHideFeedback();

        if (catTransform == null)
        {
            GameObject catObject = GameObject.Find("Cat");
            if (catObject != null) catTransform = catObject.transform;
        }

        if (safeCollider != null) safeCollider.enabled = true;

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Cat Medicine",
            "The medicine is locked away. Get it and bring it to the sick cat.",
            new[] { "Crack open the locked safe", "Bring the medicine to the sick cat" }
        );
    }

    private void Update()
    {
        if (medicinePickupGraceTimer > 0f) medicinePickupGraceTimer -= Time.deltaTime;

        UpdateSafePickup();
        UpdateMedicineDelivery();
        UpdateLockpickProgress();
    }

    private void UpdateLockpickProgress()
    {
        if (!questStarted || questDone || safeOpen || !vc_SkillManager.Instance.IsHoldingTag("unlock"))
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (_playerTransform == null || safe == null)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        if (Vector3.Distance(_playerTransform.position, safe.transform.position) >= lockpickRange)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            return;
        }

        lockpickTimer += Time.deltaTime;
        int remaining = Mathf.Max(0, Mathf.CeilToInt(lockpickHoldTime - lockpickTimer) - 1);
        vc_QuestHUD.Instance?.ShowFeedback($"Lockpicking... {remaining}s");

        if (lockpickTimer >= lockpickHoldTime)
        {
            lockpickTimer = 0f;
            vc_QuestHUD.Instance?.HideFeedback();
            OpenSafe();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        if (skill.SkillData.HasTag("craft") && !moldingDone)
        {
            moldingDone = true;
            StartCoroutine(ShowMoldPopupThenComplete());
            handled = true;
        }
        if (!handled) vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private IEnumerator ShowMoldPopupThenComplete()
    {
        vc_FloatingMessage.Instance?.Show("Key created!");
        yield return new WaitForSeconds(1.5f);
        OpenSafe();
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        if (safeCollider != null) safeCollider.enabled = false;
        vc_QuestHUD.Instance?.CheckObjective(1);
        mainMarker_Cat?.Hide();
        _questRoom?.OnQuestComplete();
    }

    private void UpdateSafePickup()
    {
        if (!questStarted || questDone || !safeOpen || medicinePickedUp || _playerTransform == null || safe == null) return;

        Collider2D pickupCollider = safe.GetComponent<Collider2D>();
        float pickupRange = 0.75f;
        if (pickupCollider != null)
            pickupRange = Mathf.Max(pickupRange, pickupCollider.bounds.extents.magnitude);

        if (Vector2.Distance(_playerTransform.position, safe.transform.position) > pickupRange) return;

        vc_FloatingMessage.Instance?.Show("You successfully got the medicine!");
        medicinePickedUp = true;
        medicinePickupGraceTimer = 3f;
        vc_QuestHUD.Instance?.CheckObjective(0);
    }

    private void UpdateMedicineDelivery()
    {
        if (!questStarted || questDone || !medicinePickedUp || _playerTransform == null || catTransform == null) return;
        if (medicinePickupGraceTimer > 0f) return;
        if (Vector2.Distance(_playerTransform.position, catTransform.position) > medicineDeliveryRange) return;

        vc_FloatingMessage.Instance?.Show("Medicine delivered!");
        CompleteQuest();
    }

    private void OpenSafe()
    {
        if (safeOpen) return;
        safeOpen = true;
        mainMarker_Safe?.Hide();
        if (safeCollider != null) safeCollider.enabled = false;
        vc_FloatingMessage.Instance?.Show("Safe opened!");
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
