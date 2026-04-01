using UnityEngine;

[DisallowMultipleComponent]
public class vc_CatQuest : MonoBehaviour, vc_IQuestLogic
{
    [SerializeField] private GameObject bridge;
    [SerializeField] private GameObject bridgeBlocker;
    [SerializeField] private vc_SkillManager skillManager;
    [SerializeField] private vc_CatAISystem catAI;
    [SerializeField] private vc_FloatingMessage floatingMessage;

    private vc_QuestRoom activeQuestRoom;
    private bool questStarted = false;
    private bool buildDone = false;
    private bool charmDone = false;
    private bool charmActive = false;

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
        if (questStarted)
        {
            return;
        }

        activeQuestRoom = questRoom;
        questStarted = true;
        buildDone = false;
        charmDone = false;
        charmActive = false;
        SetBridgeBuiltState(false);
        SubscribeToSkillManager();
    }

    private void Update()
    {
        if (!questStarted || !charmActive || charmDone || catAI == null || !catAI.HasReachedPlayer())
        {
            return;
        }

        charmDone = true;
        charmActive = false;
        questStarted = false;
        UnsubscribeFromSkillManager();
        ShowFloatingMessage("Cat rescued!");
        Debug.Log("Charm complete");
        activeQuestRoom?.OnQuestComplete();

        if (catAI != null)
        {
            Destroy(catAI.gameObject);
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill usedSkill)
    {
        if (!questStarted || usedSkill == null)
        {
            return;
        }

        if (usedSkill is vc_BuildSkill)
        {
            UseBuildSkill();
            return;
        }

        if (usedSkill is vc_CharmSkill)
        {
            UseCharmSkill();
        }
    }

    private void SubscribeToSkillManager()
    {
        if (skillManager != null)
        {
            skillManager.SkillUsed -= HandleSkillUsed;
            skillManager.SkillUsed += HandleSkillUsed;
        }
    }

    private void UnsubscribeFromSkillManager()
    {
        if (skillManager != null)
        {
            skillManager.SkillUsed -= HandleSkillUsed;
        }
    }

    private void UseBuildSkill()
    {
        if (buildDone)
        {
            return;
        }

        buildDone = true;
        SetBridgeBuiltState(true);
        ShowFloatingMessage("Bridge built!");
        Debug.Log("Build complete");
    }

    private void UseCharmSkill()
    {
        if (!buildDone || charmDone || charmActive)
        {
            return;
        }

        if (catAI != null)
        {
            catAI.StartMovingToPlayer();
        }

        charmActive = true;
    }

    private void SetBridgeBuiltState(bool built)
    {
        if (bridge != null)
        {
            bridge.SetActive(built);
        }

        if (bridgeBlocker != null)
        {
            bridgeBlocker.SetActive(!built);
        }
    }

    private void ShowFloatingMessage(string message)
    {
        if (floatingMessage != null)
        {
            floatingMessage.Show(message);
        }
    }
}
