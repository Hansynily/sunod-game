using UnityEngine.UIElements;
using UnityEngine;
using SunodGame.Core;
using SunodGame.Telemetry;

/// <summary>
/// Drives the redesigned Level0 tutorial as a guided, coached sequence with a
/// single tracked quest in the QuestPanel whose objectives tick off as the
/// player progresses:
///   intro -> teach movement -> walk onto the skill plaza (trigger) -> read the
///   6 kinds of skills & pick ONE -> talk to Tala (the "!" + Help / Later prompt, same as
///   the real towns) -> reach the glowing zone -> get her kite back (per-skill flavor) ->
///   life stages -> rule-based career reveal -> exit.
///
/// Replaces the deleted vc_Level0TutorialController. Attach to TutorialRoot.
/// </summary>
[DisallowMultipleComponent]
public class vc_TutorialFlowController : MonoBehaviour
{
    public static vc_TutorialFlowController Instance { get; private set; }

    private enum Phase { Intro, TeachMove, Explore, ChooseSkill, TalkToTala, GoToZone, QuestActive, LifeStages, Done }

    // Quest objective indices (order must match the steps array in ShowQuestInfo).
    private const int OBJ_MOVE   = 0;
    private const int OBJ_AREA   = 1;
    private const int OBJ_CHOOSE = 2;
    private const int OBJ_TALK   = 3;
    private const int OBJ_ZONE   = 4;
    private const int OBJ_CLEAR  = 5;
    private const int OBJ_STAGES = 6;

    private const int TotalSteps = 7;

    private const string IntroText =
        "Welcome to SUNOD! In the real game you grow up through 3 life stages - Elementary, High School and College - helping people along the way. First, let's learn the basics.";

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
    private bool _promptShown;          // Help/Later asked for this visit (reset when the player walks away)
    private GameObject _talaMarker;

    private int _stepsDone;
    private readonly bool[] _stepCounted = new bool[TotalSteps];

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
        if (_player == null) return;

        if (_phase == Phase.TeachMove)
        {
            if (Vector3.Distance(_player.transform.position, _moveStartPos) >= moveTeachDistance)
                OnMoveLearned();
            return;
        }

        // Walking up to Tala asks for help, the same way people do in the towns.
        if (_phase == Phase.TalkToTala && quest != null && quest.Kid != null)
        {
            float d = Vector2.Distance(_player.transform.position, quest.Kid.position);
            if (d > 2.6f) _promptShown = false;
            if (d < 1.8f && !_promptShown && (vc_DialogPanel.Instance == null || !vc_DialogPanel.Instance.IsOpen))
            {
                _promptShown = true;
                AskForHelp();
            }
        }
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
                "Look at each skill, then pick one",
                "Talk to Tala and tap Help",
                "Step into the glowing skill zone",
                "Use your skill to get Tala's kite back",
                "Learn about the 3 life stages",
            });

        vc_QuestHUD.Instance?.SetCounter($"Step 1 of {TotalSteps}");

        vc_FloatingMessage.Instance?.Show("Drag the joystick at the bottom left to walk around.");
        PointAtJoystick();
    }

    private void AdvanceStep(int objectiveIndex)
    {
        if (objectiveIndex < 0 || objectiveIndex >= _stepCounted.Length || _stepCounted[objectiveIndex]) return;
        _stepCounted[objectiveIndex] = true;

        _stepsDone++;
        vc_QuestHUD.Instance?.SetCounter(
            _stepsDone < TotalSteps ? $"Step {_stepsDone + 1} of {TotalSteps}" : "Tutorial done!");

        vc_QuestHUD.Instance?.ShowFeedbackTimed("Step done!", 1.5f);
    }

    private void OnMoveLearned()
    {
        _phase = Phase.Explore;
        vc_UIPointer.Hide();
        vc_QuestHUD.Instance?.CheckObjective(OBJ_MOVE);
        AdvanceStep(OBJ_MOVE);
        vc_FloatingMessage.Instance?.Show("Good! Follow the road to the plaza.");
        PointArrow(skillAreaArrowTarget);
    }

    // Called by vc_TutorialAreaTrigger when the player walks into the skill area.
    public void OnEnteredSkillArea()
    {
        if (_phase != Phase.Explore) return;

        vc_QuestHUD.Instance?.CheckObjective(OBJ_AREA);
        AdvanceStep(OBJ_AREA);
        HideArrow();
        _phase = Phase.ChooseSkill;

        // Skills explained in short steps: what they are, the 6 kinds, then what to do here.
        vc_DialogPanel.Instance?.ShowMessage(
            "What Are Skills?",
            "Skills are how you solve problems in SUNOD. Each problem can be solved in more than one way, with different skills.",
            () => vc_DialogPanel.Instance?.ShowList(
                "6 Kinds of Skills",
                "Every skill is one of 6 kinds:",
                new[]
                {
                    new vc_DialogPanel.ListRow("R", "Realistic", "build and fix"),
                    new vc_DialogPanel.ListRow("I", "Investigative", "think and find out"),
                    new vc_DialogPanel.ListRow("A", "Artistic", "create and design"),
                    new vc_DialogPanel.ListRow("S", "Social", "help and teach"),
                    new vc_DialogPanel.ListRow("E", "Enterprising", "lead and convince"),
                    new vc_DialogPanel.ListRow("C", "Conventional", "sort and organize"),
                },
                () =>
                {
                    PointAtSkillButtons(-1);
                    vc_DialogPanel.Instance?.ShowMessage(
                    "Your Skill Buttons",
                    "Your skill buttons are at the bottom right. Each button holds one kind and shows that kind's symbol.",
                    () => { vc_UIPointer.Hide(); vc_DialogPanel.Instance?.ShowMessage(
                        "Pick One",
                        "There is one skill of each kind here. Walk up to each one to read what it does. You can only take one, so pick the one you like most.",
                        () => PointArrowAtFirstMarker()); });
                }));
    }

    // ── Talking to Tala: the "!" and the Help / Later prompt ──────────────

    private void EnterTalkToTala()
    {
        _phase = Phase.TalkToTala;
        _promptShown = false;
        Transform kid = quest != null ? quest.Kid : null;
        _talaMarker = kid != null && kid.Find("QuestMarker") != null ? kid.Find("QuestMarker").gameObject : null;
        if (_talaMarker != null) _talaMarker.SetActive(true);

        string who = quest != null ? quest.KidName : "Tala";
        vc_DialogPanel.Instance?.ShowMessage(
            "Someone Needs Help",
            $"See the \"!\" over {who} in the park? That means someone needs your help.\n\nWalk up to {who} to talk.",
            () => { if (kid != null) PointArrow(kid); });
    }

    private void AskForHelp()
    {
        string who = quest != null ? quest.KidName : "Tala";
        HideArrow();
        vc_DialogPanel.Instance?.ShowChoice(
            who,
            "My kite got stuck up in the tree and I can't reach it! Can you help me get it down?",
            null,
            "Help",
            "Later",
            onConfirm: AcceptHelp,
            onCancel: () =>
            {
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"Walk back to {who} when you're ready.", 2.5f);
                vc_FloatingMessage.Instance?.Show("In the real game, Later lets you look around first. Everyone still gets helped.");
            });
    }

    private void AcceptHelp()
    {
        if (_phase != Phase.TalkToTala) return;
        if (_talaMarker != null) _talaMarker.SetActive(false);
        vc_QuestHUD.Instance?.CheckObjective(OBJ_TALK);
        AdvanceStep(OBJ_TALK);

        if (quest != null)
        {
            quest.Completed -= OnQuestCompleted;
            quest.Completed += OnQuestCompleted;
            quest.BeginTutorialQuest();
        }
        EnterGoToZone();
    }

    private void EnterGoToZone()
    {
        _phase = Phase.GoToZone;

        vc_DialogPanel.Instance?.ShowMessage(
            "Skill Zones",
            $"{SkillLabel(_chosen)} is now in its {KindName(_chosen)} button. Skills only work in skill zones. Stand on the glowing spot by the tree.",
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
            $"{s.DisplayName} ({KindName(s)})",
            $"{s.description}\n\nKind: {KindLetter(s)} - {KindName(s)}",
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
        vc_SkillManager.Instance?.EquipToCategorySlot(_chosen);

        if (markers != null)
            for (int i = 0; i < markers.Length; i++) markers[i]?.Despawn();

        vc_QuestHUD.Instance?.CheckObjective(OBJ_CHOOSE);
        AdvanceStep(OBJ_CHOOSE);
        EnterTalkToTala();
    }

    // ── Zone coaching ─────────────────────────────────────────────────────

    private void OnZoneEntered()
    {
        if (_phase != Phase.GoToZone || _zoneCoached) return;
        _zoneCoached = true;
        _phase = Phase.QuestActive;

        vc_QuestHUD.Instance?.CheckObjective(OBJ_ZONE);
        AdvanceStep(OBJ_ZONE);
        HideArrow();
        vc_FloatingMessage.Instance?.Show($"You're in the zone! Tap the {KindLetter(_chosen)} button to use {SkillLabel(_chosen)}.");

        // Point at the exact button until the player presses it.
        PointAtSkillButtons(vc_SkillManager.SlotIndexForLetter(KindLetter(_chosen)));
        if (vc_SkillManager.Instance != null)
        {
            vc_SkillManager.Instance.SkillUsed -= OnSkillUsedOnce;
            vc_SkillManager.Instance.SkillUsed += OnSkillUsedOnce;
        }
    }

    private void OnSkillUsedOnce(int slot, vc_PlayerSkill skill)
    {
        if (vc_SkillManager.Instance != null) vc_SkillManager.Instance.SkillUsed -= OnSkillUsedOnce;
        vc_UIPointer.Hide();
    }

    // ── Completion ────────────────────────────────────────────────────────

    private void OnQuestCompleted()
    {
        if (quest != null) quest.Completed -= OnQuestCompleted;
        vc_UIPointer.Hide();

        vc_QuestHUD.Instance?.CheckObjective(OBJ_CLEAR);
        AdvanceStep(OBJ_CLEAR);

        // Short pause so the kite / "Step done!" lands before the next dialog.
        Invoke(nameof(EnterLifeStages), 1.2f);
    }

    // ── Life stages: how a real run is shaped ─────────────────────────────

    private void EnterLifeStages()
    {
        _phase = Phase.LifeStages;
        vc_FloatingMessage.Instance?.Show("One more thing: how a full game works.");

        vc_DialogPanel.Instance?.ShowList(
            "Your 3 Life Stages",
            "A full game takes you through 3 life stages, one town each. Finish one and you grow up into the next:",
            new[]
            {
                new vc_DialogPanel.ListRow("1", "Elementary", "your first town"),
                new vc_DialogPanel.ListRow("2", "High School", "bigger problems"),
                new vc_DialogPanel.ListRow("3", "College", "then your results"),
            },
            () => vc_DialogPanel.Instance?.ShowMessage(
                "Beat the Clock",
                "In a real quest, stars at the top count down while you work, and a bar under them drains.\n\n" +
                "Finish fast to keep more stars. If the bar runs out, time's up.",
                () => vc_DialogPanel.Instance?.ShowMessage(
                "Days and Energy",
                "Each stage has 16 people to help, about 4 days' worth. People with a \"!\" above their head need help.\n\n" +
                "Every quest you take uses 1 energy, and you get 4 energy a day. When it runs out, find a shed with a bed and tap Sleep.",
                () => vc_DialogPanel.Instance?.ShowMessage(
                    "There Are No Wrong Answers",
                    "When someone asks for help, tap Help to start, or Later if you want to look around first. Everyone gets helped before you grow up to the next stage.\n\n" +
                    "How you solve each quest, and which skills you reach for, shows what you really enjoy.",
                    () => vc_DialogPanel.Instance?.ShowMessage(
                        "Watch Your Progress",
                        "The ring in the top-right corner shows your life stage and the day. Tap it to see all 3 stages.\n\n" +
                        "Finish all 48 quests (16 per stage) and SUNOD shows the careers that fit you best.",
                        OnLifeStagesLearned)))));
    }

    private void OnLifeStagesLearned()
    {
        _phase = Phase.Done;
        vc_QuestHUD.Instance?.CheckObjective(OBJ_STAGES);
        AdvanceStep(OBJ_STAGES);

        string letter = _chosen != null ? _chosen.riaSecLetter : string.Empty;
        string name   = _chosen != null ? _chosen.DisplayName : "your skill";
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

    // ── Skill wording helpers ─────────────────────────────────────────────

    private static string KindLetter(vc_SkillData s) =>
        s != null && !string.IsNullOrWhiteSpace(s.riaSecLetter) ? s.riaSecLetter.Trim().ToUpperInvariant() : "?";

    private static string KindName(vc_SkillData s)
    {
        switch (KindLetter(s))
        {
            case "R": return "Realistic";
            case "I": return "Investigative";
            case "A": return "Artistic";
            case "S": return "Social";
            case "E": return "Enterprising";
            case "C": return "Conventional";
            default:  return "skill";
        }
    }

    private static string SkillLabel(vc_SkillData s) =>
        s != null && !string.IsNullOrWhiteSpace(s.DisplayName) ? s.DisplayName : "Your skill";

    // ── UI pointer helpers ────────────────────────────────────────────────

    private static void PointAtJoystick()
    {
        var zone = FindFirstObjectByType<SunodGame.UI.vc_FloatingJoystick>(FindObjectsInactive.Exclude);
        if (zone == null) return;
        Transform visible = zone.transform.parent != null ? zone.transform.parent.Find("Joystick") : null;
        vc_UIPointer.PointAtWorldUI((visible != null ? visible : zone.transform) as RectTransform);
    }

    /// <summary>Frames one skill button (slot index) or all six (-1).</summary>
    private static void PointAtSkillButtons(int slot)
    {
        foreach (UnityEngine.UIElements.UIDocument doc in FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
        {
            var root = doc.rootVisualElement;
            if (root == null || root.Q("slot-btn-0") == null) continue;
            if (slot >= 0) { vc_UIPointer.PointAt(root.Q("slot-btn-" + slot)); return; }
            var all = new UnityEngine.UIElements.VisualElement[vc_SkillManager.SlotCount];
            for (int i = 0; i < all.Length; i++) all[i] = root.Q("slot-btn-" + i);
            vc_UIPointer.PointAt(all);
            return;
        }
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
