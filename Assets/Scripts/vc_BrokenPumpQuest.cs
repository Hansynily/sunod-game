using System.Collections;
using UnityEngine;

/// <summary>
/// R quest. Fix the water pump. Solvable by repair / build / push.
/// On a fitting skill: the broken pump fades out, the fixed pump fades in, quest completes.
/// Skill-zone gates WHERE the skill can be pressed, so no proximity check here.
/// </summary>
[DisallowMultipleComponent]
public class vc_BrokenPumpQuest : MonoBehaviour, vc_IQuestLogic
{
    [Header("Visuals (assign in Inspector)")]
    [SerializeField] private GameObject brokenPump;      // faded out on success
    [SerializeField] private GameObject fixedPump;        // optional - faded in on success
    [SerializeField] private vc_FloatingMarker mainMarker;
    [SerializeField] private float animDuration = 0.5f;

    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool questDone = false;

    private void OnDestroy() => UnsubscribeFromSkillManager();

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        questStarted = true;
        questDone = false;

        if (fixedPump != null) fixedPump.SetActive(false);

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Broken Pump",
            "The yard pump is broken. Get it working again.",
            new[] { "Fix the water pump" }
        );
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill.SkillData.HasTag("repair") || skill.SkillData.HasTag("build")
            || skill.SkillData.HasTag("push"))
        {
            questDone = true;
            UnsubscribeFromSkillManager();
            vc_FloatingMessage.Instance?.Show("You got the pump working!");
            StartCoroutine(ResolveAndComplete());
        }
        else
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
        }
    }

    private IEnumerator ResolveAndComplete()
    {
        if (fixedPump != null) fixedPump.SetActive(true);

        float elapsed = 0f;
        SpriteRenderer brokenSr = brokenPump != null ? brokenPump.GetComponentInChildren<SpriteRenderer>() : null;
        SpriteRenderer fixedSr = fixedPump != null ? fixedPump.GetComponentInChildren<SpriteRenderer>() : null;
        Color brokenBase = brokenSr != null ? brokenSr.color : Color.white;
        Color fixedBase = fixedSr != null ? fixedSr.color : Color.white;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / animDuration);
            if (brokenSr != null) { Color c = brokenBase; c.a = 1f - k; brokenSr.color = c; }
            if (fixedSr != null) { Color c = fixedBase; c.a = k; fixedSr.color = c; }
            yield return null;
        }

        if (brokenPump != null) brokenPump.SetActive(false);
        CompleteQuest();
    }

    private void CompleteQuest()
    {
        questStarted = false;
        vc_QuestHUD.Instance?.CheckObjective(0);
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
