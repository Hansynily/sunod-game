using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_GenericQuestEngine : MonoBehaviour, vc_IQuestLogic
{
    public enum InteractionType { ProximityTrigger, Checkpoint, NPCFollow, NPCGoTo, ItemReveal }

    [Serializable]
    public class SkillPath
    {
        public string skillTag;
        public string successMessage;
        public InteractionType interactionType;
        public Transform targetTransform;
        public float proximityRange = 2f;
        public vc_NPC_Follow npcFollow;
        public vc_NPC_GoTo npcGoTo;
        public Transform destination;
        public GameObject propToReveal;
        public GameObject[] propsToHide;
        public GameObject[] objectsToDestroy;
        public vc_FloatingMarker[] markersToHide;
        public string animTrigger;
        [Tooltip("-1 = no objective checked. Otherwise calls CheckObjective on this index when the path fires.")]
        public int objectiveIndex = -1;
        [Tooltip("If set, shows the directional arrow pointing here when this path fires.")]
        public Transform arrowTarget;
        [Tooltip("This path only activates if the path at this index has already fired. -1 = no requirement.")]
        public int requiresPathIndex = -1;
        [Tooltip("Seconds to wait before completing the quest. Only applies to ProximityTrigger and ItemReveal.")]
        public float completionDelay = 0f;
        [Tooltip("How close the NPC must be to the destination to trigger completion. Only used by NPCFollow.")]
        public float npcArrivalRange = 3f;
    }

    [Header("HUD")]
    [SerializeField] private string hudCounter = "Quest";
    [SerializeField] private string hudTitle;
    [SerializeField] private string[] hudObjectives;

    [Header("Skill Paths")]
    [SerializeField] private SkillPath[] skillPaths;
    [SerializeField] private vc_FloatingMarker[] globalMarkersToHide;
    [SerializeField] private GameObject[] globalObjectsToDestroy;

    private vc_QuestRoom _questRoom;
    private Transform _playerTransform;
    private bool _questDone;
    private SkillPath _activeFollowPath;
    private bool[] _pathFired;

    private void Start()
    {
        _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        _questRoom = questRoom;
        _questDone = false;
        _activeFollowPath = null;
        _pathFired = skillPaths != null ? new bool[skillPaths.Length] : new bool[0];
        vc_QuestHUD.Instance?.ShowQuestInfo(hudCounter, hudTitle, "", hudObjectives);
        Subscribe();
    }

    private void Update()
    {
        if (_questDone || _activeFollowPath == null) return;
        if (_activeFollowPath.npcFollow == null || _activeFollowPath.destination == null) return;

        if (Vector2.Distance(_activeFollowPath.npcFollow.transform.position,
                             _activeFollowPath.destination.position) <= _activeFollowPath.npcArrivalRange)
        {
            _activeFollowPath.npcFollow.Deactivate();
            CompleteQuest(_activeFollowPath);
            _activeFollowPath = null;
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (_questDone || skill == null) return;

        bool handled = false;

        for (int i = 0; i < skillPaths.Length; i++)
        {
            SkillPath path = skillPaths[i];

            if (!skill.SkillData.HasTag(path.skillTag)) continue;
            if (_pathFired[i]) continue;
            if (path.requiresPathIndex >= 0 && !_pathFired[path.requiresPathIndex]) continue;

            if (path.targetTransform != null && _playerTransform != null)
            {
                if (Vector2.Distance(_playerTransform.position, path.targetTransform.position)
                    > path.proximityRange) continue;
            }

            handled = true;
            _pathFired[i] = true;

            if (path.objectiveIndex >= 0)
                vc_QuestHUD.Instance?.CheckObjective(path.objectiveIndex);

            if (path.arrowTarget != null)
            {
                vc_DirectionalArrow.Instance?.SetTarget(path.arrowTarget);
                vc_DirectionalArrow.Instance?.ShowArrow();
            }

            if (!string.IsNullOrEmpty(path.animTrigger))
                path.targetTransform?.GetComponent<Animator>()?.SetTrigger(path.animTrigger);

            switch (path.interactionType)
            {
                case InteractionType.ProximityTrigger:
                    StartCoroutine(CompleteAfterDelay(path));
                    break;

                case InteractionType.Checkpoint:
                    vc_FloatingMessage.Instance?.Show(path.successMessage);
                    break;

                case InteractionType.NPCFollow:
                    if (_activeFollowPath != null) break;
                    _activeFollowPath = path;
                    path.npcFollow?.Activate();
                    vc_FloatingMessage.Instance?.Show(path.successMessage);
                    break;

                case InteractionType.NPCGoTo:
                    if (path.npcGoTo != null && path.destination != null)
                    {
                        SkillPath captured = path;
                        path.npcGoTo.OnArrived += () => StartCoroutine(CompleteAfterDelay(captured));
                        path.npcGoTo.GoTo(path.destination);
                    }
                    vc_FloatingMessage.Instance?.Show(path.successMessage);
                    break;

                case InteractionType.ItemReveal:
                    if (path.propToReveal != null) path.propToReveal.SetActive(true);
                    if (path.propsToHide != null)
                        foreach (GameObject p in path.propsToHide) p?.SetActive(false);
                    StartCoroutine(CompleteAfterDelay(path));
                    break;
            }

            break;
        }

        if (!handled)
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
    }

    private IEnumerator CompleteAfterDelay(SkillPath path)
    {
        if (path.completionDelay > 0f)
            yield return new WaitForSeconds(path.completionDelay);
        CompleteQuest(path);
    }

    private void CompleteQuest(SkillPath path)
    {
        if (_questDone) return;
        _questDone = true;

        Unsubscribe();

        if (_activeFollowPath != null)
        {
            _activeFollowPath.npcFollow?.Deactivate();
            _activeFollowPath = null;
        }

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        if (path.markersToHide != null)
            foreach (vc_FloatingMarker m in path.markersToHide) m?.Hide();

        foreach (vc_FloatingMarker m in globalMarkersToHide) m?.Hide();

        if (path.objectsToDestroy != null)
            foreach (GameObject o in path.objectsToDestroy) if (o != null) Destroy(o);

        if (globalObjectsToDestroy != null)
            foreach (GameObject o in globalObjectsToDestroy) if (o != null) Destroy(o);

        if (hudObjectives != null)
            for (int i = 0; i < hudObjectives.Length; i++)
                vc_QuestHUD.Instance?.CheckObjective(i);

        vc_FloatingMessage.Instance?.Show(path.successMessage);
        _questRoom?.OnQuestComplete();
    }

    private void Subscribe()
    {
        if (vc_SkillManager.Instance == null) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
    }

    private void Unsubscribe()
    {
        if (vc_SkillManager.Instance != null)
            vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
    }
}
