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

        if (muralReveal != null) muralReveal.SetActive(false);

        SubscribeToSkillManager();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "The Mural",
            "The wall is blank. Bring it to life.",
            new[] { "Paint the mural" }
        );
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!questStarted || questDone || skill == null) return;

        if (skill.SkillData.HasTag("paint") || skill.SkillData.HasTag("craft")
            || skill.SkillData.HasTag("build") || skill.SkillData.HasTag("barrier"))
        {
            questDone = true;
            UnsubscribeFromSkillManager();
            vc_FloatingMessage.Instance?.Show("You finished the mural!");
            StartCoroutine(ResolveAndComplete());
        }
        else
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
        }
    }

    private IEnumerator ResolveAndComplete()
    {
        if (muralReveal != null)
        {
            muralReveal.SetActive(true);
            SpriteRenderer sr = muralReveal.GetComponentInChildren<SpriteRenderer>();
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
