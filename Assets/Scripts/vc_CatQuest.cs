using System;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_CatQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private GameObject bridge;
    [SerializeField] private GameObject bridgeBlocker;
    [SerializeField] private vc_CatAISystem catAI;

    public event Action TutorialBuildCompleted;
    public event Action TutorialCharmStarted;
    public event Action TutorialQuestCompleted;

    private vc_QuestRoom activeQuestRoom;
    private bool questStarted = false;
    private bool buildDone = false;
    private bool charmDone = false;
    private bool charmActive = false;
    private bool tutorialMode = false;

    private void Awake()
    {
        SetBridgeBuiltState(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        if (questStarted) return;
        StartQuest(questRoom, false);
    }

    public void BeginTutorialQuest()
    {
        if (questStarted) return;
        StartQuest(null, true);
    }

    private void Update()
    {
        if (!questStarted || !charmActive || charmDone || catAI == null || !catAI.HasReachedPlayer()) return;

        charmDone = true;
        charmActive = false;
        questStarted = false;
        UnsubscribeFromSkillManager();
        vc_FloatingMessage.Instance?.Show("Cat rescued!");
        activeQuestRoom?.OnQuestComplete();

        if (tutorialMode) TutorialQuestCompleted?.Invoke();

        activeQuestRoom = null;
        tutorialMode = false;

        if (catAI != null) Destroy(catAI.gameObject);
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill usedSkill)
    {
        if (!questStarted || usedSkill == null) return;

        if (usedSkill is vc_BuildSkill) { UseBuildSkill(); return; }
        if (usedSkill is vc_CharmSkill) { UseCharmSkill(); }
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

    private void StartQuest(vc_QuestRoom questRoom, bool isTutorialQuest)
    {
        activeQuestRoom = questRoom;
        tutorialMode = isTutorialQuest;
        questStarted = true;
        buildDone = false;
        charmDone = false;
        charmActive = false;
        SetBridgeBuiltState(false);
        SubscribeToSkillManager();
    }

    private void UseBuildSkill()
    {
        if (buildDone) return;
        buildDone = true;
        SetBridgeBuiltState(true);
        vc_FloatingMessage.Instance?.Show("Bridge built!");
        if (tutorialMode) TutorialBuildCompleted?.Invoke();
    }

    private void UseCharmSkill()
    {
        if (!buildDone || charmDone || charmActive) return;
        catAI?.StartMovingToPlayer();
        charmActive = true;
        if (tutorialMode) TutorialCharmStarted?.Invoke();
    }

    private void SetBridgeBuiltState(bool built)
    {
        if (bridge != null) bridge.SetActive(built);
        if (bridgeBlocker != null) bridgeBlocker.SetActive(!built);
    }
}
