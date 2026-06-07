using System.Collections;
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
    [SerializeField] private Vector3 crateDropPosition = new(5.15f, 6.522f, 0f);
    [SerializeField] private float animDuration = 0.5f;
    [SerializeField] private vc_FloatingMarker mainMarker_Blockage;
    [SerializeField] private vc_FloatingMarker poiMarker_AltRoute;
    [SerializeField] private vc_FloatingMarker mainMarker_Goal;

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

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Blocked Path",
            "Something is blocking the way. Clear it or find another route.",
            new[] { "Find a way past the blockage", "Get to the other side" }
        );
    }

    private void Update()
    {
        if (!questStarted || questDone) return;

        TryCompleteAtGoal();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        bool handled = false;
        bool fitsSkill = skill.SkillData.HasTag("navigate") || skill.SkillData.HasTag("guide")
            || skill.SkillData.HasTag("map") || skill.SkillData.HasTag("direct")
            || skill.SkillData.HasTag("push") || skill.SkillData.HasTag("build")
            || skill.SkillData.HasTag("repair");

        if (fitsSkill && !strengthDone)
        {
            // Every fitting skill clears the same blocking object out of the way. No alt route needed.
            strengthDone = true;
            if (blockingCollider != null) blockingCollider.enabled = false;
            StartCoroutine(ClearBlockage());
            mainMarker_Blockage?.Hide();
            poiMarker_AltRoute?.Hide();
            vc_FloatingMessage.Instance?.Show("Path cleared!");
            vc_QuestHUD.Instance?.CheckObjective(0);
            handled = true;
        }
        if (!handled) vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private IEnumerator ClearBlockage()
    {
        if (blockingObject == null) yield break;

        Vector3 from = blockingObject.localPosition;
        Vector3 to = crateDropPosition;
        SpriteRenderer sr = blockingObject.GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.white;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / animDuration);
            blockingObject.localPosition = Vector3.Lerp(from, to, k);
            if (blockingRb != null)
            {
                blockingRb.position = blockingObject.position;
                blockingRb.linearVelocity = Vector2.zero;
                blockingRb.angularVelocity = 0f;
            }
            if (sr != null) { Color c = baseColor; c.a = 1f - k; sr.color = c; }
            yield return null;
        }

        if (sr != null) { Color c = baseColor; c.a = 0f; sr.color = c; }
        blockingObject.gameObject.SetActive(false);
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
        vc_QuestHUD.Instance?.CheckObjective(1);
        mainMarker_Goal?.Hide();
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
