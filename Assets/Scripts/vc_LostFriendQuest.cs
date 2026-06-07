using UnityEngine;

[DisallowMultipleComponent]
public class vc_LostFriendQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_NPCController friendNPC;
    [SerializeField] private Transform friendTransform;
    [SerializeField] private Transform classroomTransform;
    [SerializeField] private float charmRange = 1.5f;
    [SerializeField] private vc_FloatingMarker mainMarker_Friend;
    [SerializeField] private vc_FloatingMarker mainMarker_Classroom;

    private Transform _playerTransform;
    private vc_QuestRoom activeQuestRoom;
    private bool questStarted = false;
    private bool trackDone = false;
    private bool charmActive = false;
    private bool guidingActive = false;
    private bool questDone = false;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        activeQuestRoom = questRoom;
        questStarted = true;
        trackDone = false;
        charmActive = false;
        guidingActive = false;
        questDone = false;

        vc_DirectionalArrow.Instance?.ClearTarget();
        vc_DirectionalArrow.Instance?.HideArrow();

        // Auto-wire the NPC refs so a missing Inspector assignment doesn't silently break movement.
        if (friendNPC == null && friendTransform != null) friendNPC = friendTransform.GetComponent<vc_NPCController>();
        if (friendNPC == null) friendNPC = GetComponentInChildren<vc_NPCController>(true);
        if (friendTransform == null && friendNPC != null) friendTransform = friendNPC.transform;

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Lost Friend",
            "Your classmate got lost on the way to class. Help them find their way.",
            new[] { "Find the route to class", "Convince your friend to follow you" }
        );
    }

    private void Update()
    {
        if (!questStarted || questDone) return;

        if ((!charmActive && !guidingActive) || friendTransform == null || classroomTransform == null) return;

        if (Vector3.Distance(friendTransform.position, classroomTransform.position) < 1.5f)
            CompleteQuest();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        if ((skill.SkillData.HasTag("navigate") || skill.SkillData.HasTag("guide")
             || skill.SkillData.HasTag("map") || skill.SkillData.HasTag("direct")) && !trackDone)
        {
            if (classroomTransform != null)
            {
                vc_DirectionalArrow.Instance?.SetTarget(classroomTransform);
                vc_DirectionalArrow.Instance?.ShowArrow();
            }
            // The friend follows the revealed route to class on their own — navigate solves it alone.
            if (friendNPC != null && classroomTransform != null)
            {
                friendNPC.WalkToPoint(classroomTransform.position);
                guidingActive = true;
                vc_QuestHUD.Instance?.CheckObjective(1);
            }
            vc_FloatingMessage.Instance?.Show("Path found! Your friend heads for class.");
            trackDone = true;
            vc_QuestHUD.Instance?.CheckObjective(0);
            handled = true;
        }
        if (skill.SkillData.HasTag("attract") || skill.SkillData.HasTag("charm")
            || skill.SkillData.HasTag("persuade") || skill.SkillData.HasTag("summon"))
        {
            TryCharmFriend();
            handled = true;
        }
        if (!handled) vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private void TryCharmFriend()
    {
        if (charmActive) return;
        if (_playerTransform == null || friendNPC == null) return;

        // Skill zone already gates WHERE this can be used — no proximity check needed.
        friendNPC.FollowTarget(_playerTransform);
        charmActive = true;
        vc_FloatingMessage.Instance?.Show("Your friend now follows you.");
        vc_QuestHUD.Instance?.CheckObjective(1);
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        vc_FloatingMessage.Instance?.Show("Friend guided to class!");
        vc_QuestHUD.Instance?.CheckObjective(1);

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        mainMarker_Friend?.Hide();
        mainMarker_Classroom?.Hide();
        activeQuestRoom?.OnQuestComplete();

        if (friendNPC != null) Destroy(friendNPC.gameObject);
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
