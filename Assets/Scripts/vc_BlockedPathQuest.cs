using UnityEngine;

[DisallowMultipleComponent]
public class vc_BlockedPathQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private Transform blockingObject;
    [SerializeField] private Rigidbody2D blockingRb;
    [SerializeField] private BoxCollider2D blockingCollider;
    [SerializeField] private Transform pathGoalZone;
    [SerializeField] private GameObject hiddenWall;
    [SerializeField] private BoxCollider2D hiddenWallCollider;
    [SerializeField] private float strengthRange = 1.5f;
    [SerializeField] private Vector3 crateDropPosition = new(5.15f, 6.522f, 0f);

    private Transform _playerTransform;
    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool strengthDone = false;
    private bool pathDone = false;
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
        strengthDone = false;
        pathDone = false;
        questDone = false;
        ResolvePathGoalZone();

        if (hiddenWall != null) hiddenWall.SetActive(true);
        if (hiddenWallCollider != null) hiddenWallCollider.enabled = true;

        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (!questStarted || questDone) return;

        if (!strengthDone && blockingObject != null && _playerTransform != null && IsHoldingSkill<vc_StrengthSkill>())
        {
            if (Vector3.Distance(_playerTransform.position, blockingObject.position) < strengthRange)
            {
                vc_FloatingMessage.Instance?.Show("Pushing...");
                blockingObject.localPosition = crateDropPosition;

                if (blockingRb != null)
                {
                    blockingRb.position = blockingObject.position;
                    blockingRb.linearVelocity = Vector2.zero;
                    blockingRb.angularVelocity = 0f;
                }

                strengthDone = true;
                vc_FloatingMessage.Instance?.Show("Path cleared!");
            }
        }

        TryCompleteAtGoal();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill is vc_PathSkill && !pathDone)
        {
            if (hiddenWall != null) hiddenWall.SetActive(false);
            if (hiddenWallCollider != null) hiddenWallCollider.enabled = false;
            vc_FloatingMessage.Instance?.Show("Alternative route found.");
            pathDone = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        if (pathDone || strengthDone)
        {
            vc_FloatingMessage.Instance?.Show("Made it through!");
            CompleteQuest();
        }
    }

    private void CompleteQuest()
    {
        if (questDone) return;

        questDone = true;
        questStarted = false;
        UnsubscribeFromSkillManager();
        if (blockingCollider != null) blockingCollider.enabled = false;
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

    private void TryCompleteAtGoal()
    {
        if ((!pathDone && !strengthDone) || _playerTransform == null) return;

        ResolvePathGoalZone();
        if (pathGoalZone == null) return;

        Collider2D goalCollider = pathGoalZone.GetComponent<Collider2D>()
            ?? pathGoalZone.GetComponentInChildren<Collider2D>();
        if (goalCollider == null) return;

        Collider2D playerCollider = _playerTransform.GetComponent<Collider2D>()
            ?? _playerTransform.GetComponentInChildren<Collider2D>();

        bool reachedGoal = playerCollider != null
            ? goalCollider.bounds.Intersects(playerCollider.bounds) || goalCollider.IsTouching(playerCollider)
            : goalCollider.OverlapPoint(_playerTransform.position) || goalCollider.bounds.Contains(_playerTransform.position);

        if (!reachedGoal) return;

        vc_FloatingMessage.Instance?.Show("Made it through!");
        CompleteQuest();
    }

    private void ResolvePathGoalZone()
    {
        if (pathGoalZone != null) return;
        GameObject goalObject = GameObject.Find("PathGoalZone");
        if (goalObject != null) pathGoalZone = goalObject.transform;
    }
}
