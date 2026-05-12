using System;
using UnityEngine;

/// <summary>
/// Quest logic for L4 Quest 1 — Medical Attention.
///
/// Combo 1: navigate → attract
///   - navigate shows directional arrow to clinic
///   - attract near injured friend makes them follow player
///   - reaching the clinic with injured friend completes the quest
///
/// Combo 2: craft → persuade
///   - craft near injured friend spawns medical materials prop
///   - persuade near helper friend sends them to the injured friend to perform a 7s action
///   - action completing triggers quest complete
/// </summary>
[DisallowMultipleComponent]
public class vc_MedicalAttentionQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_NPC_Follow injuredFriendFollow;
    [SerializeField] private vc_NPC_GoTo helperFriendGoTo;
    [SerializeField] private vc_NPC_DoAction helperFriendAction;
    [SerializeField] private Transform clinicTransform;
    [SerializeField] private Transform injuredFriendTransform;
    [SerializeField] private Transform materialSpawnPoint;
    [SerializeField] private GameObject materialsProp;
    [SerializeField] private vc_FloatingMarker mainMarker_Injured;
    [SerializeField] private vc_FloatingMarker poiMarker_Helper;
    [SerializeField] private vc_FloatingMarker poiMarker_Clinic;

    private const float ProximityRange = 2f;
    private const float ClinicArrivalRange = 1.5f;

    private vc_QuestRoom _questRoom;
    private Transform _playerTransform;

    private bool arrowShown = false;
    private bool friendFollowing = false;
    private bool materialsCreated = false;
    private bool helperPersuaded = false;
    private bool questDone = false;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    /// <inheritdoc/>
    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        _questRoom = questRoom;

        arrowShown = false;
        friendFollowing = false;
        materialsCreated = false;
        helperPersuaded = false;
        questDone = false;

        if (materialsProp != null) materialsProp.SetActive(false);

        vc_DirectionalArrow.Instance?.ClearTarget();
        vc_DirectionalArrow.Instance?.HideArrow();
        vc_QuestHUD.Instance?.ForceHideFeedback();

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Medical Attention",
            "Your friend got into an accident. Help them reach the school clinic.",
            new[] {
                "Reach the clinic with your friend",
                "OR: Create medical materials",
                "Have a friend help with first aid"
            });
    }

    private void Update()
    {
        if (questDone || !friendFollowing) return;
        if (injuredFriendTransform == null || clinicTransform == null) return;

        if (Vector2.Distance(injuredFriendTransform.position, clinicTransform.position) <= ClinicArrivalRange)
        {
            injuredFriendFollow?.Deactivate();
            CompleteQuest();
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (questDone || skill == null) return;

        bool handled = false;

        // navigate → show directional arrow to clinic
        if (skill.SkillData.HasTag("navigate") && !arrowShown)
        {
            arrowShown = true;
            if (clinicTransform != null)
            {
                vc_DirectionalArrow.Instance?.SetTarget(clinicTransform);
                vc_DirectionalArrow.Instance?.ShowArrow();
            }
            vc_FloatingMessage.Instance?.Show("Arrow pointing to clinic.");
            handled = true;
        }

        // attract → make injured friend follow (requires arrow shown, proximity check)
        if (skill.SkillData.HasTag("attract") && arrowShown && !friendFollowing)
        {
            if (_playerTransform != null && injuredFriendTransform != null &&
                Vector2.Distance(_playerTransform.position, injuredFriendTransform.position) <= ProximityRange)
            {
                friendFollowing = true;
                injuredFriendFollow?.Activate();
                poiMarker_Helper?.Hide();
                vc_FloatingMessage.Instance?.Show("Your friend is following you.");
                vc_QuestHUD.Instance?.CheckObjective(0);
                handled = true;
            }
        }

        // craft → spawn materials prop near injured friend
        if (skill.SkillData.HasTag("craft") && !materialsCreated)
        {
            if (_playerTransform != null && injuredFriendTransform != null &&
                Vector2.Distance(_playerTransform.position, injuredFriendTransform.position) <= ProximityRange)
            {
                materialsCreated = true;
                if (materialsProp != null) materialsProp.SetActive(true);
                vc_FloatingMessage.Instance?.Show("Materials created.");
                vc_QuestHUD.Instance?.CheckObjective(1);
                handled = true;
            }
        }

        // persuade → send helper to injured friend, start timed action
        if (skill.SkillData.HasTag("persuade") && materialsCreated && !helperPersuaded)
        {
            Transform helperTransform = helperFriendGoTo != null ? helperFriendGoTo.transform : null;
            if (_playerTransform != null && helperTransform != null &&
                Vector2.Distance(_playerTransform.position, helperTransform.position) <= ProximityRange)
            {
                helperPersuaded = true;

                helperFriendGoTo.OnArrived += OnHelperArrived;
                helperFriendGoTo.GoTo(injuredFriendTransform);

                poiMarker_Clinic?.Hide();
                vc_FloatingMessage.Instance?.Show("Helper is on their way.");
                vc_QuestHUD.Instance?.CheckObjective(2);
                handled = true;
            }
        }

        if (!handled)
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private void OnHelperArrived()
    {
        if (helperFriendGoTo != null) helperFriendGoTo.OnArrived -= OnHelperArrived;
        if (helperFriendAction == null) { CompleteQuest(); return; }

        helperFriendAction.OnActionComplete += OnHelperActionComplete;
        helperFriendAction.SetDuration(7f);
        helperFriendAction.StartAction();
    }

    private void OnHelperActionComplete()
    {
        if (helperFriendAction != null) helperFriendAction.OnActionComplete -= OnHelperActionComplete;
        CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (questDone) return;
        questDone = true;

        UnsubscribeFromSkillManager();

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        mainMarker_Injured?.Hide();
        poiMarker_Helper?.Hide();
        poiMarker_Clinic?.Hide();

        vc_FloatingMessage.Instance?.Show("Friend reached the clinic!");
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
