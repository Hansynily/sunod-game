using UnityEngine;
using SunodGame.Core;
using SunodGame.Telemetry;

/// <summary>
/// Drives the redesigned Level0 tutorial as a guided, coached sequence with a
/// single tracked quest in the QuestPanel whose objectives tick off as the
/// player progresses:
///   intro -> teach movement -> walk into the skill area (trigger) -> read the
///   6 skills & pick ONE -> reach the glowing zone -> clear the obstacle
///   (per-skill flavor) -> rule-based career reveal -> exit.
///
/// Replaces the deleted vc_Level0TutorialController. Attach to TutorialRoot.
/// </summary>
[DisallowMultipleComponent]
public class vc_TutorialFlowController : MonoBehaviour
{
    public static vc_TutorialFlowController Instance { get; private set; }

    private enum Phase { Intro, TeachMove, Explore, ChooseSkill, GoToZone, QuestActive, Done }

    // Quest objective indices (order must match the steps array in ShowQuestInfo).
    private const int OBJ_MOVE   = 0;
    private const int OBJ_AREA   = 1;
    private const int OBJ_CHOOSE = 2;
    private const int OBJ_ZONE   = 3;
    private const int OBJ_CLEAR  = 4;

    private const string IntroText =
        "Welcome to SUNOD. Solve things your own way. The skills you pick and the choices you make quietly tell a story about where your strengths could take you.";

    [Header("Scene Refs")]
    [SerializeField] private vc_TutorialSkillMarker[] markers;
    [SerializeField] private vc_TutorialQuest quest;

    [Header("Optional Guidance (null-safe)")]
    [SerializeField] private Transform skillAreaArrowTarget;   // arrow points here during Explore
    [SerializeField] private Transform zoneArrowTarget;        // arrow points here after a pick

    [SerializeField] private float moveTeachDistance = 0.75f;

    private Phase _phase = Phase.Intro;
    private vc_SkillData _chosen;
    private bool _pickMade;
    private bool _zoneCoached;
    private vc_TutorialSkillMarker _activeMarker;
    private PlayerController _player;
    private Vector3 _moveStartPos;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Fresh slate on every tutorial entry. The inventory is DontDestroyOnLoad and the
        // skill-zone gate counter is static, so they survive scene loads. The tutorial is
        // reached via LoadByName / editor force-play, both of which skip
        // SceneLoader.ResetForNewRun - so without this a replayed tutorial inherits the
        // previous run's gathered skills and a desynced zone gate (skills usable anywhere).
        vc_PlayerInventory.Instance?.ClearInventory();
        vc_SkillZone.ResetCounter();

        _player = FindFirstObjectByType<PlayerController>();
    }

    private void Start()
    {
        vc_SkillManager.Instance?.SetSkillsInteractable(false);
        vc_SkillZone.PlayerEnteredZone += OnZoneEntered;

        _phase = Phase.Intro;
        vc_DialogPanel.Instance?.ShowMessage(string.Empty, IntroText, EnterTeachMove);
    }

    private void OnDestroy()
    {
        vc_SkillZone.PlayerEnteredZone -= OnZoneEntered;
        if (quest != null) quest.Completed -= OnQuestCompleted;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_phase != Phase.TeachMove || _player == null) return;

        if (Vector3.Distance(_player.transform.position, _moveStartPos) >= moveTeachDistance)
            OnMoveLearned();
    }

    // ── Phase transitions ─────────────────────────────────────────────────

    private void EnterTeachMove()
    {
        _phase = Phase.TeachMove;
        _moveStartPos = _player != null ? _player.transform.position : Vector3.zero;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Tutorial",
            "Getting Started",
            "Learn the basics of SUNOD.",
            new[]
            {
                "Use the joystick to move",
                "Find the skill area",
                "Inspect the skills and choose one",
                "Step into the glowing skill zone",
                "Use your skill to clear the path",
            });

        vc_FloatingMessage.Instance?.Show("Drag the joystick at the bottom left to walk around.");
    }

    private void OnMoveLearned()
    {
        _phase = Phase.Explore;
        vc_QuestHUD.Instance?.CheckObjective(OBJ_MOVE);
        vc_FloatingMessage.Instance?.Show("Good. Now head into the skill area ahead.");
        PointArrow(skillAreaArrowTarget);
    }

    // Called by vc_TutorialAreaTrigger when the player walks into the skill area.
    public void OnEnteredSkillArea()
    {
        if (_phase != Phase.Explore) return;

        vc_QuestHUD.Instance?.CheckObjective(OBJ_AREA);
        HideArrow();
        _phase = Phase.ChooseSkill;

        vc_DialogPanel.Instance?.ShowMessage(
            "Six Skills",
            "Six skills are scattered around this area. Walk up to each one to see what it does. Read as many as you like, but you can only take one.",
            () => PointArrowAtFirstMarker());
    }

    private void EnterGoToZone()
    {
        _phase = Phase.GoToZone;

        vc_DialogPanel.Instance?.ShowMessage(
            "Skill Zones",
            "Skills only work inside skill zones. Look for the glowing patch on the ground ahead, then stand on it.",
            () => PointArrow(zoneArrowTarget));
    }

    // ── Marker interaction ────────────────────────────────────────────────

    public void PresentSkill(vc_TutorialSkillMarker marker)
    {
        if (_phase != Phase.ChooseSkill || _pickMade || marker == null || marker.Skill == null) return;
        if (_activeMarker == marker) return;

        _activeMarker = marker;
        vc_SkillData s = marker.Skill;

        vc_DialogPanel.Instance?.ShowChoice(
            s.skillName,
            s.description,
            s.icon,
            "Pick this up",
            "Close",
            onConfirm: () => ConfirmPick(marker),
            onCancel:  () => { _activeMarker = null; });
    }

    public void MarkerExited(vc_TutorialSkillMarker marker)
    {
        if (_activeMarker == marker) _activeMarker = null;
    }

    private void ConfirmPick(vc_TutorialSkillMarker marker)
    {
        if (_pickMade || marker == null || marker.Skill == null) return;

        _pickMade = true;
        _activeMarker = null;
        _chosen = marker.Skill;

        vc_PlayerInventory.Instance?.AddSkill(_chosen);
        vc_SkillManager.Instance?.AssignSkillToSlot(0, _chosen);

        if (markers != null)
            for (int i = 0; i < markers.Length; i++) markers[i]?.Despawn();

        if (quest != null)
        {
            quest.Completed -= OnQuestCompleted;
            quest.Completed += OnQuestCompleted;
            quest.BeginTutorialQuest();
        }

        vc_QuestHUD.Instance?.CheckObjective(OBJ_CHOOSE);
        EnterGoToZone();
    }

    // ── Zone coaching ─────────────────────────────────────────────────────

    private void OnZoneEntered()
    {
        if (_phase != Phase.GoToZone || _zoneCoached) return;
        _zoneCoached = true;
        _phase = Phase.QuestActive;

        vc_QuestHUD.Instance?.CheckObjective(OBJ_ZONE);
        HideArrow();
        vc_FloatingMessage.Instance?.Show("You are in the zone. Your skill button lit up at the bottom right. Tap it to use your skill.");
    }

    // ── Completion ────────────────────────────────────────────────────────

    private void OnQuestCompleted()
    {
        _phase = Phase.Done;
        if (quest != null) quest.Completed -= OnQuestCompleted;

        vc_QuestHUD.Instance?.CheckObjective(OBJ_CLEAR);

        string letter = _chosen != null ? _chosen.riaSecLetter : string.Empty;
        string name   = _chosen != null ? _chosen.skillName   : "your skill";
        vc_TutorialEndScreen.Show(letter, name, FinishTutorial);
    }

    private void FinishTutorial()
    {
        bool hasSession =
            AuthManager.Instance != null &&
            SessionState.Instance != null &&
            !string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken);

        if (!hasSession)
        {
            SessionState.Instance?.SetTutorialCompletionState(true);
            SceneLoader.GoToMainMenu();
            return;
        }

        AuthManager.Instance.MarkTutorialCompleted(
            onResolved: result =>
            {
                SessionState.Instance?.SetTutorialCompletionState(result != null && result.tutorial_completed);
                SceneLoader.GoToMainMenu();
            },
            onError: _ =>
            {
                SessionState.Instance?.SetTutorialCompletionState(true);
                SceneLoader.GoToMainMenu();
            });
    }

    // ── Arrow helpers (all null-safe) ─────────────────────────────────────

    private void PointArrow(Transform target)
    {
        if (target == null || vc_DirectionalArrow.Instance == null) return;
        vc_DirectionalArrow.Instance.SetTarget(target);
        vc_DirectionalArrow.Instance.ShowArrow();
    }

    private void HideArrow()
    {
        vc_DirectionalArrow.Instance?.HideArrow();
    }

    private void PointArrowAtFirstMarker()
    {
        if (vc_DirectionalArrow.Instance == null || markers == null) return;
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && markers[i].gameObject.activeSelf)
            {
                PointArrow(markers[i].transform);
                return;
            }
        }
    }
}
