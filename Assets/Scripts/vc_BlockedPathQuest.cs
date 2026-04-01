using UnityEngine;

[DisallowMultipleComponent]
public class vc_BlockedPathQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform blockingObject;
    [SerializeField] private Rigidbody2D blockingRb;
    [SerializeField] private BoxCollider2D blockingCollider;
    [SerializeField] private Transform pathGoalZone;
    [SerializeField] private GameObject hiddenWall;
    [SerializeField] private BoxCollider2D hiddenWallCollider;
    [SerializeField] private vc_QuestRoom questRoom;
    [SerializeField] private float strengthRange = 1.5f;
    [SerializeField] private Vector3 crateDropPosition = new(5.15f, 6.522f, 0f);
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private bool questStarted = false;
    private bool strengthDone = false;
    private bool pathDone = false;
    private bool questDone = false;

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        questRoom = activeQuestRoom != null ? activeQuestRoom : questRoom;
        questStarted = true;
        strengthDone = false;
        pathDone = false;
        questDone = false;
        ResolvePathGoalZone();

        if (hiddenWall != null)
        {
            hiddenWall.SetActive(true);
        }

        if (hiddenWallCollider != null)
        {
            hiddenWallCollider.enabled = true;
        }

        SubscribeToSkillManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    private void Update()
    {
        if (!questStarted || questDone)
        {
            return;
        }

        if (!strengthDone && blockingObject != null && playerTransform != null && IsHoldingSkill<vc_StrengthSkill>())
        {
            float playerDistance = Vector3.Distance(playerTransform.position, blockingObject.position);
            if (playerDistance < strengthRange)
            {
                ShowFloatingMessage("Pushing...");
                blockingObject.localPosition = crateDropPosition;

                if (blockingRb != null)
                {
                    blockingRb.position = blockingObject.position;
                    blockingRb.linearVelocity = Vector2.zero;
                    blockingRb.angularVelocity = 0f;
                }

                strengthDone = true;
                ShowFloatingMessage("Path cleared!");
            }
        }

        TryCompleteAtGoal();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null)
        {
            return;
        }

        if (skill is vc_PathSkill && !pathDone)
        {
            if (hiddenWall != null)
            {
                hiddenWall.SetActive(false);
            }

            if (hiddenWallCollider != null)
            {
                hiddenWallCollider.enabled = false;
            }

            ShowFloatingMessage("Alternative route found.");
            pathDone = true;
            Debug.Log("Path skill - hidden wall removed");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        if (pathDone || strengthDone)
        {
            ShowFloatingMessage("Made it through!");
            CompleteQuest();
        }
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

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        Debug.Log("Blocked Path quest complete");
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

    private void TryCompleteAtGoal()
    {
        if ((!pathDone && !strengthDone) || playerTransform == null)
        {
            return;
        }

        ResolvePathGoalZone();
        if (pathGoalZone == null)
        {
            return;
        }

        Collider2D goalCollider = pathGoalZone.GetComponent<Collider2D>();
        if (goalCollider == null)
        {
            goalCollider = pathGoalZone.GetComponentInChildren<Collider2D>();
        }

        if (goalCollider == null)
        {
            return;
        }

        Collider2D playerCollider = playerTransform.GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            playerCollider = playerTransform.GetComponentInChildren<Collider2D>();
        }

        bool reachedGoal = playerCollider != null
            ? goalCollider.bounds.Intersects(playerCollider.bounds) || goalCollider.IsTouching(playerCollider)
            : goalCollider.OverlapPoint(playerTransform.position) || goalCollider.bounds.Contains(playerTransform.position);

        if (!reachedGoal)
        {
            return;
        }

        ShowFloatingMessage("Made it through!");
        CompleteQuest();
    }

    private void ShowFloatingMessage(string message)
    {
        if (floatingMessage != null)
        {
            floatingMessage.Show(message);
        }
    }

    private void ResolvePathGoalZone()
    {
        if (pathGoalZone != null)
        {
            return;
        }

        GameObject goalObject = GameObject.Find("PathGoalZone");
        if (goalObject != null)
        {
            pathGoalZone = goalObject.transform;
        }
    }
}
