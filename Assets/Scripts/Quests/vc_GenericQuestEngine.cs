using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_GenericQuestEngine : MonoBehaviour, vc_IQuestLogic
{
    public enum InteractionType
    {
        // Existing types - order preserved so serialized Inspector data stays valid.
        ProximityTrigger,
        Checkpoint,
        NPCFollow,
        NPCGoTo,
        ItemReveal,
        // New types (appended).
        NPCTimedAction,
        ReachLocation
    }

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
        [Tooltip("Tick to check a HUD objective when this path fires. Which one = Objective Index below. Leave OFF for single-objective quests (the quest ticks everything on completion anyway).")]
        public bool ticksObjective;
        [Tooltip("HUD objective to check when this path fires. Only used if Ticks Objective is ON. 0 = first objective.")]
        public int objectiveIndex;
        [Tooltip("If set, shows the directional arrow pointing here when this path fires.")]
        public Transform arrowTarget;
        [Tooltip("Tick only for a CHAIN step: this path is blocked until the path at Requires Path Index has fired. Leave OFF for normal paths.")]
        public bool hasPrerequisite;
        [Tooltip("Chain prerequisite: index of the path that must fire first. Only used if Has Prerequisite is ON. 0 = first path.")]
        public int requiresPathIndex;
        [Tooltip("Seconds to wait before completing the quest. Only applies to ProximityTrigger, ItemReveal, NPCGoTo, NPCTimedAction.")]
        public float completionDelay = 0f;
        [Tooltip("How close the NPC must be to the destination to trigger completion. Only used by NPCFollow.")]
        public float npcArrivalRange = 3f;

        [Header("Counter (optional)")]
        [Tooltip("Empty = no counter. Paths sharing a non-empty group count together; the path resolves only when the group reaches Counter Target. A counter path re-fires on every qualifying skill press until then.")]
        public string counterGroup;
        [Tooltip("How many qualifying presses this counter group needs before the path resolves.")]
        public int counterTarget = 1;

        [Header("Timed NPC action (NPCTimedAction only)")]
        [Tooltip("NPC that performs the timed action after walking to the destination.")]
        public vc_NPC_DoAction npcAction;
        [Tooltip("Seconds the timed action runs. <= 0 uses the vc_NPC_DoAction's own duration.")]
        public float actionDuration = 0f;

        [Header("Multi-stage")]
        [Tooltip("Tick ONLY for a non-final chain step (e.g. the 'plan' before the 'survey'). A stage step ticks objectives, cleans its props, and unlocks chained paths but does NOT finish the quest. Leave OFF for normal paths - they complete the quest when they fire.")]
        public bool isStageStep;

        [Header("No-skill path (optional)")]
        [Tooltip("Armed automatically when the quest starts - no skill press needed. ReachLocation only: the exploration fallback (player wanders onto the target). Armed silently - no arrow or message, so a hidden target isn't given away. successMessage still shows when it resolves. Leave skillTag empty on these paths.")]
        public bool startsActive;
    }

    [Header("HUD")]
    [SerializeField] private string hudCounter = "Quest";
    [SerializeField] private string hudTitle;
    [SerializeField] private string[] hudObjectives;

    [Header("Skill Paths")]
    [SerializeField] private SkillPath[] skillPaths;
    [SerializeField] private vc_FloatingMarker[] globalMarkersToHide;
    [SerializeField] private GameObject[] globalObjectsToDestroy;

    [Header("Animation")]
    [SerializeField] private float propAnimDuration = 0.4f;

    private vc_QuestRoom _questRoom;
    private vc_QuestTimer _questTimer;
    private Transform _playerTransform;
    private bool _questDone;
    private SkillPath _activeFollowPath;
    private bool[] _pathFired;
    private int _runId; // invalidates delayed coroutines from a previous run

    private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();
    private readonly List<SkillPath> _activeReachPaths = new List<SkillPath>();
    private readonly List<Action> _eventCleanup = new List<Action>();

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

        if (_questTimer != null) _questTimer.QuestEnded -= HandleQuestEnded;
        _questTimer = questTimer;
        if (_questTimer != null) _questTimer.QuestEnded += HandleQuestEnded;

        _questDone = false;
        _activeFollowPath = null;
        _pathFired = skillPaths != null ? new bool[skillPaths.Length] : new bool[0];
        _runId++;

        // Reset multi-run state so re-entering the quest starts clean.
        RunEventCleanup();
        _counters.Clear();
        _activeReachPaths.Clear();

        // The player may be spawned after this component's Start().
        if (_playerTransform == null)
            _playerTransform = FindFirstObjectByType<PlayerController>()?.transform;

        vc_QuestHUD.Instance?.ShowQuestInfo(hudCounter, hudTitle, "", hudObjectives);
        Subscribe();
        ArmStartsActivePaths();
    }

    // Paths marked startsActive complete by exploration alone (e.g. stumbling onto the
    // missing key without a reveal skill) - the quest is solvable with zero skill presses.
    // Armed silently: no arrow, no message - otherwise the hidden target is given away.
    private void ArmStartsActivePaths()
    {
        if (skillPaths == null) return;

        for (int i = 0; i < skillPaths.Length; i++)
        {
            SkillPath path = skillPaths[i];
            if (path == null || !path.startsActive) continue;

            if (path.interactionType != InteractionType.ReachLocation || path.targetTransform == null)
            {
                Debug.LogWarning($"[QuestEngine] startsActive path {i} ignored - only ReachLocation with a Target Transform is supported.");
                continue;
            }

            _pathFired[i] = true; // consumed at arm time so a skill press can't double-fire it
            _activeReachPaths.Add(path);
        }
    }

    private void Update()
    {
        if (_questDone) return;

        // NPCFollow arrival check.
        if (_activeFollowPath != null &&
            _activeFollowPath.npcFollow != null &&
            _activeFollowPath.destination != null)
        {
            if (Vector2.Distance(_activeFollowPath.npcFollow.transform.position,
                                 _activeFollowPath.destination.position) <= EffectiveRange(_activeFollowPath.npcArrivalRange, 3f))
            {
                _activeFollowPath.npcFollow.Deactivate();
                SkillPath arrived = _activeFollowPath;
                _activeFollowPath = null;
                ResolvePath(arrived);
                return;
            }
        }

        // ReachLocation polling.
        if (_activeReachPaths.Count > 0 && _playerTransform != null)
        {
            for (int i = _activeReachPaths.Count - 1; i >= 0; i--)
            {
                SkillPath p = _activeReachPaths[i];
                if (p == null || p.targetTransform == null) { _activeReachPaths.RemoveAt(i); continue; }

                if (Vector2.Distance(_playerTransform.position, p.targetTransform.position) <= EffectiveRange(p.proximityRange, 2f))
                {
                    _activeReachPaths.RemoveAt(i);
                    ResolvePath(p);
                    return;
                }
            }
        }
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (_questDone || skill == null || skillPaths == null) return;

        bool handled = false;

        for (int i = 0; i < skillPaths.Length; i++)
        {
            SkillPath path = skillPaths[i];
            if (path == null) continue;

            if (!PathAcceptsSkill(path, skill.SkillData)) continue;
            if (_pathFired[i]) continue;
            if (path.hasPrerequisite && path.requiresPathIndex >= 0 &&
                (path.requiresPathIndex >= _pathFired.Length || !_pathFired[path.requiresPathIndex])) continue;

            // An NPCFollow path must not be consumed while another follow is active -
            // RunInteraction would silently drop it and the quest could soft-lock.
            if (path.interactionType == InteractionType.NPCFollow && _activeFollowPath != null)
            {
                handled = true;
                vc_QuestHUD.Instance?.ShowFeedbackTimed("Someone is already following you.");
                break;
            }

            handled = true;

            // Per-fire feedback (arrow / animation).
            if (path.arrowTarget != null)
            {
                vc_DirectionalArrow.Instance?.SetTarget(path.arrowTarget);
                vc_DirectionalArrow.Instance?.ShowArrow();
            }

            if (!string.IsNullOrEmpty(path.animTrigger))
                path.targetTransform?.GetComponent<Animator>()?.SetTrigger(path.animTrigger);

            // Counter paths re-fire on every qualifying press until the group reaches its target.
            if (!string.IsNullOrEmpty(path.counterGroup))
            {
                int target = Mathf.Max(1, path.counterTarget);
                int count = (_counters.TryGetValue(path.counterGroup, out int existing) ? existing : 0) + 1;
                _counters[path.counterGroup] = count;

                if (count < target)
                {
                    string progress = string.IsNullOrEmpty(path.successMessage)
                        ? $"{count}/{target}"
                        : $"{path.successMessage} ({count}/{target})";
                    vc_QuestHUD.Instance?.ShowFeedbackTimed(progress);
                    break; // keep this path re-fireable - do NOT set _pathFired
                }

                // Target reached: retire every path sharing this group so none re-resolves.
                for (int j = 0; j < skillPaths.Length; j++)
                    if (skillPaths[j] != null && skillPaths[j].counterGroup == path.counterGroup)
                        _pathFired[j] = true;

                if (path.ticksObjective && path.objectiveIndex >= 0)
                    vc_QuestHUD.Instance?.CheckObjective(path.objectiveIndex);

                RunInteraction(path); // honor the configured interaction, same as non-counter paths
                break;
            }

            // Non-counter paths fire once.
            _pathFired[i] = true;

            if (path.ticksObjective && path.objectiveIndex >= 0)
                vc_QuestHUD.Instance?.CheckObjective(path.objectiveIndex);

            RunInteraction(path);
            break;
        }

        if (!handled)
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("That skill doesn't work here.");
            Debug.Log($"[QuestEngine] No skill path accepted '{skill.SkillData.skillName}' " +
                      $"(its tags: [{JoinTags(skill.SkillData.capabilityTags)}]). Configured path Skill Tags: [{DescribePathTags()}]. " +
                      $"A path fires when its Skill Tag equals one of the pressed skill's tags, its name, or its button label (all trimmed, case-insensitive).");
        }
    }

    // Matches a pressed skill to a path. Forgiving on purpose - this is Inspector-authored
    // by a non-coder, and the two mistakes that silently break a correct-looking path are a
    // stray space and typing the skill NAME ("TULAK") into Skill Tag instead of its tag
    // ("push"). Skill names and capability tags never overlap, so the name/label fallback
    // cannot cause a wrong match. Empty tag never matches a press (walk/startsActive paths).
    // Unity zero-fills new Inspector array elements instead of running C# field
    // initializers, so a proximity/arrival range left untouched serializes as 0 (meaning
    // "player must stand exactly on the point"). Treat a non-positive range as the intended
    // default so authors never have to remember to type it.
    private static float EffectiveRange(float configured, float fallback)
    {
        return configured > 0f ? configured : fallback;
    }

    private static bool PathAcceptsSkill(SkillPath path, vc_SkillData data)
    {
        if (path == null || data == null) return false;

        string tag = path.skillTag != null ? path.skillTag.Trim() : string.Empty;
        if (string.IsNullOrEmpty(tag)) return false;

        if (data.HasTag(tag)) return true;
        if (!string.IsNullOrEmpty(data.skillName) &&
            string.Equals(data.skillName.Trim(), tag, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(data.buttonLabel) &&
            string.Equals(data.buttonLabel.Trim(), tag, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static string JoinTags(string[] tags)
    {
        return tags != null ? string.Join(", ", tags) : string.Empty;
    }

    private string DescribePathTags()
    {
        if (skillPaths == null) return string.Empty;
        List<string> parts = new List<string>();
        for (int i = 0; i < skillPaths.Length; i++)
            if (skillPaths[i] != null)
                parts.Add($"{i}:'{skillPaths[i].skillTag}'");
        return string.Join(", ", parts);
    }

    private void RunInteraction(SkillPath path)
    {
        switch (path.interactionType)
        {
            case InteractionType.ProximityTrigger:
                StartCoroutine(CompleteAfterDelay(path));
                break;

            case InteractionType.Checkpoint:
                // Non-terminal: shows a message and unlocks chained paths. Never resolves on its own.
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
                    SubscribeGoToOnce(path.npcGoTo, () => StartCoroutine(CompleteAfterDelay(captured)));
                    path.npcGoTo.GoTo(path.destination);
                }
                vc_FloatingMessage.Instance?.Show(path.successMessage);
                break;

            case InteractionType.NPCTimedAction:
            {
                SkillPath captured = path;
                Action runAction = () =>
                {
                    if (captured.npcAction != null)
                    {
                        if (captured.actionDuration > 0f) captured.npcAction.SetDuration(captured.actionDuration);
                        SubscribeActionOnce(captured.npcAction, () => StartCoroutine(CompleteAfterDelay(captured)));
                        captured.npcAction.StartAction();
                    }
                    else
                    {
                        StartCoroutine(CompleteAfterDelay(captured));
                    }
                };

                if (path.npcGoTo != null && path.destination != null)
                {
                    SubscribeGoToOnce(path.npcGoTo, runAction);
                    path.npcGoTo.GoTo(path.destination);
                }
                else
                {
                    runAction();
                }
                vc_FloatingMessage.Instance?.Show(path.successMessage);
                break;
            }

            case InteractionType.ReachLocation:
                if (path.targetTransform != null)
                {
                    _activeReachPaths.Add(path);
                    if (path.arrowTarget == null)
                    {
                        vc_DirectionalArrow.Instance?.SetTarget(path.targetTransform);
                        vc_DirectionalArrow.Instance?.ShowArrow();
                    }
                }
                vc_FloatingMessage.Instance?.Show(path.successMessage);
                break;

            case InteractionType.ItemReveal:
                if (path.propToReveal != null) StartCoroutine(FadeIn(path.propToReveal, propAnimDuration));
                if (path.propsToHide != null)
                    foreach (GameObject p in path.propsToHide)
                        if (p != null) StartCoroutine(FadeAndHide(p, propAnimDuration));
                StartCoroutine(CompleteAfterDelay(path));
                break;
        }
    }

    private IEnumerator FadeAndHide(GameObject target, float duration)
    {
        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) { target.SetActive(false); yield break; }
        Color original = sr.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            sr.color = new Color(original.r, original.g, original.b, 1f - k);
            yield return null;
        }
        target.SetActive(false);
        sr.color = original;
    }

    private IEnumerator FadeIn(GameObject target, float duration)
    {
        target.SetActive(true);
        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;
        Color c = sr.color;
        sr.color = new Color(c.r, c.g, c.b, 0f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sr.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        sr.color = new Color(c.r, c.g, c.b, 1f);
    }

    private IEnumerator CompleteAfterDelay(SkillPath path)
    {
        int runId = _runId;
        if (path.completionDelay > 0f)
            yield return new WaitForSeconds(path.completionDelay);
        if (runId != _runId) yield break; // quest was restarted while we waited
        ResolvePath(path);
    }

    // Decides what a finished path does: final paths complete the quest, non-final paths
    // only apply their local effects and let chained paths take over.
    private void ResolvePath(SkillPath path)
    {
        if (_questDone || path == null) return;

        if (!path.isStageStep)
            CompleteQuest(path);
        else
            ApplyNonFinalEffects(path);
    }

    private void ApplyNonFinalEffects(SkillPath path)
    {
        if (path.markersToHide != null)
            foreach (vc_FloatingMarker m in path.markersToHide) m?.Hide();

        if (path.objectsToDestroy != null)
            foreach (GameObject o in path.objectsToDestroy) if (o != null) Destroy(o);

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        vc_FloatingMessage.Instance?.Show(path.successMessage);
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

        _activeReachPaths.Clear();

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        if (path.markersToHide != null)
            foreach (vc_FloatingMarker m in path.markersToHide) m?.Hide();

        if (globalMarkersToHide != null)
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

    // The timer ends a quest on expiry without going through CompleteQuest. Without
    // this handler a failed quest's engine stays subscribed to SkillUsed forever and
    // keeps reacting (messages, arrows, NPC moves) during every later quest in the run.
    private void HandleQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (_questTimer != null) _questTimer.QuestEnded -= HandleQuestEnded;
        if (_questDone) return; // normal completion already shut everything down

        _questDone = true;
        _runId++;
        StopAllCoroutines();

        if (_activeFollowPath != null)
        {
            _activeFollowPath.npcFollow?.Deactivate();
            _activeFollowPath = null;
        }
        _activeReachPaths.Clear();

        vc_DirectionalArrow.Instance?.HideArrow();
        vc_DirectionalArrow.Instance?.ClearTarget();

        Unsubscribe();
    }

    // ── Event subscription helpers (guard against listener stacking) ──────────
    // "Once" is literal: the wrapper removes itself on first fire, so an NPC shared
    // by two paths can't re-trigger a stale handler on its next arrival. The cleanup
    // list still holds a removal (harmless if already removed) for unfired handlers.

    private void SubscribeGoToOnce(vc_NPC_GoTo goTo, Action handler)
    {
        if (goTo == null || handler == null) return;
        Action wrapper = null;
        wrapper = () =>
        {
            goTo.OnArrived -= wrapper;
            handler();
        };
        goTo.OnArrived += wrapper;
        _eventCleanup.Add(() => goTo.OnArrived -= wrapper);
    }

    private void SubscribeActionOnce(vc_NPC_DoAction action, Action handler)
    {
        if (action == null || handler == null) return;
        Action wrapper = null;
        wrapper = () =>
        {
            action.OnActionComplete -= wrapper;
            handler();
        };
        action.OnActionComplete += wrapper;
        _eventCleanup.Add(() => action.OnActionComplete -= wrapper);
    }

    private void RunEventCleanup()
    {
        for (int i = 0; i < _eventCleanup.Count; i++) _eventCleanup[i]?.Invoke();
        _eventCleanup.Clear();
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

        if (_questTimer != null)
            _questTimer.QuestEnded -= HandleQuestEnded;

        RunEventCleanup();
    }
}
