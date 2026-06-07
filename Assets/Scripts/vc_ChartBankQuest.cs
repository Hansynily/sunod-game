using System.Collections;
using UnityEngine;

/// <summary>
/// I quest. Survey and chart the riverbank. Solvable by map / survey / plan / guide / inspect.
/// On a fitting skill: the chart overlay fades in over the area, quest completes.
/// Skill-zone gates WHERE the skill can be pressed, so no proximity check here.
/// </summary>
[DisallowMultipleComponent]
public class vc_ChartBankQuest : MonoBehaviour, vc_IQuestLogic
{
    [Header("Visuals (assign in Inspector)")]
    [SerializeField] private GameObject chartReveal;     // the charted map overlay — starts hidden, fades in
    [SerializeField] private vc_FloatingMarker mainMarker;
    [SerializeField] private float animDuration = 0.6f;

    private vc_QuestRoom _questRoom;
    private bool questStarted = false;
    private bool questDone = false;

    private void OnDestroy() => UnsubscribeFromSkillManager();

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        questStarted = true;
        questDone = false;

        if (chartReveal != null) chartReveal.SetActive(false);

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Chart the Bank",
            "This stretch of riverbank needs to be charted. Survey it.",
            new[] { "Map the riverbank" }
        );
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill.SkillData.HasTag("map") || skill.SkillData.HasTag("survey")
            || skill.SkillData.HasTag("plan") || skill.SkillData.HasTag("guide")
            || skill.SkillData.HasTag("inspect"))
        {
            questDone = true;
            UnsubscribeFromSkillManager();
            vc_FloatingMessage.Instance?.Show("You charted the whole bank!");
            StartCoroutine(ResolveAndComplete());
        }
        else
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
        }
    }

    private IEnumerator ResolveAndComplete()
    {
        if (chartReveal != null)
        {
            chartReveal.SetActive(true);
            SpriteRenderer sr = chartReveal.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Color baseColor = sr.color;
                float elapsed = 0f;
                while (elapsed < animDuration)
                {
                    elapsed += Time.deltaTime;
                    Color c = baseColor; c.a = Mathf.Clamp01(elapsed / animDuration); sr.color = c;
                    yield return null;
                }
            }
        }

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
