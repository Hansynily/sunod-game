using System.Collections;
using UnityEngine;

/// <summary>
/// A quest. Paint the mural on the blank wall. Solvable by paint / craft / build / barrier.
/// On a fitting skill: the mural sprite fades in over the blank wall, quest completes.
/// Skill-zone gates WHERE the skill can be pressed, so no proximity check here.
/// </summary>
[DisallowMultipleComponent]
public class vc_MuralQuest : MonoBehaviour, vc_IQuestLogic
{
    [Header("Visuals (assign in Inspector)")]
    [SerializeField] private GameObject muralReveal;     // the finished mural - starts hidden, fades in
    [SerializeField] private SpriteRenderer blankWall;   // the blank wall - shown at start, fades out on success
    [SerializeField] private vc_FloatingMarker mainMarker;
    [SerializeField] private float animDuration = 0.6f;

    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool questDone = false;
    private bool zoneEntered = false;

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
        vc_SkillZone.PlayerEnteredZone -= HandlePlayerEnteredZone;
    }

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        questStarted = true;
        questDone = false;
        zoneEntered = false;

        if (muralReveal != null) muralReveal.SetActive(false);
        if (blankWall != null) blankWall.gameObject.SetActive(true);

        SubscribeToSkillManager();
        vc_SkillZone.PlayerEnteredZone -= HandlePlayerEnteredZone;
        vc_SkillZone.PlayerEnteredZone += HandlePlayerEnteredZone;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "The Mural",
            "The wall is blank. Bring it to life.",
            new[] { "Stand in the glowing zone", "Use an Artistic skill to paint the wall" }
        );
    }

    private void HandlePlayerEnteredZone()
    {
        if (!questStarted || zoneEntered) return;
        zoneEntered = true;
        vc_SkillZone.PlayerEnteredZone -= HandlePlayerEnteredZone;
        vc_QuestHUD.Instance?.CheckObjective(0);
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill.SkillData.HasTag("paint") || skill.SkillData.HasTag("craft")
            || skill.SkillData.HasTag("barrier"))
        {
            questDone = true;
            UnsubscribeFromSkillManager();
            vc_FloatingMessage.Instance?.Show("You finished the mural!");
            StartCoroutine(ResolveAndComplete());
        }
        else
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That won't paint the wall. Try an Artistic skill.");
        }
    }

    private IEnumerator ResolveAndComplete()
    {
        SpriteRenderer revealSr = muralReveal != null ? muralReveal.GetComponentInChildren<SpriteRenderer>() : null;

        if (muralReveal != null) muralReveal.SetActive(true);

        Color revealBase = revealSr != null ? revealSr.color : Color.white;
        Color blankBase = blankWall != null ? blankWall.color : Color.white;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);

            if (revealSr != null)
            {
                Color c = revealBase; c.a = t; revealSr.color = c;
            }
            if (blankWall != null)
            {
                Color c = blankBase; c.a = 1f - t; blankWall.color = c;
            }

            yield return null;
        }

        if (blankWall != null) blankWall.gameObject.SetActive(false);

        CompleteQuest();
    }

    private void CompleteQuest()
    {
        questStarted = false;
        vc_QuestHUD.Instance?.CheckObjective(1);
        mainMarker?.Hide();
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
