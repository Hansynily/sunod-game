// Active demo flow only. Parked thesis-future systems remain in the repo but are not part of this runtime path.
using System;
using System.Collections.Generic;
using TMPro;
using SunodGame.Core;
using SunodGame.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    [Serializable]
    public struct ChallengeStarThresholds
    {
        public int three_star_max_retry_count;
        public int two_star_max_retry_count;
        public int one_star_max_retry_count;
        public int zero_star_min_retry_count;

        public int EvaluateStars(int retryCount)
        {
            retryCount = Mathf.Max(0, retryCount);

            if (retryCount <= three_star_max_retry_count) return 3;
            if (retryCount <= two_star_max_retry_count) return 2;
            if (retryCount <= one_star_max_retry_count) return 1;
            return 0;
        }

        public int GetRetryCountForStars(int stars)
        {
            switch (Mathf.Clamp(stars, 0, 3))
            {
                case 3:
                    return Mathf.Max(0, three_star_max_retry_count);
                case 2:
                    return Mathf.Max(three_star_max_retry_count + 1, two_star_max_retry_count);
                case 1:
                    return Mathf.Max(two_star_max_retry_count + 1, one_star_max_retry_count);
                default:
                    return Mathf.Max(zero_star_min_retry_count, one_star_max_retry_count + 1);
            }
        }
    }

    [Serializable]
    public class ChallengeDefinition
    {
        public string challenge_id;
        public RiasecCode primary_riasec;
        public GameObject content_root;
        public ChallengeStarThresholds star_thresholds;

        public ChallengeDefinition(string challengeId, RiasecCode primaryRiasec, GameObject contentRoot, ChallengeStarThresholds starThresholds)
        {
            challenge_id = challengeId;
            primary_riasec = primaryRiasec;
            content_root = contentRoot;
            star_thresholds = starThresholds;
        }
    }

    public enum ChallengeSessionPhase
    {
        SessionStart,
        RoundIntro,
        RoundActive,
        RoundResult,
        NextRound,
        SessionComplete
    }

    public partial class ChallengeSessionController : MonoBehaviour
    {
        public static ChallengeSessionController Instance { get; private set; }

        private const string DemoSceneName = "DemoPlayScene";
        private const string ChallengeHudName = "ChallengeHUD";
        private const string RoundCounterName = "RoundCounter";
        private const string RoundObjectiveName = "RoundObjective";
        private const string StarsDisplayName = "StarsDisplay";
        private const string NextRoundButtonName = "BtnNextRound";
        private const string DebugTextName = "DebugText";
        private const string ControlsCanvasName = "Controls UI";

        private static readonly string[] ChallengeRootNames =
        {
            "Challenge_01",
            "Challenge_02",
            "Challenge_03",
            "Challenge_04",
            "Challenge_05",
            "Challenge_06"
        };

        private static readonly string[] ChallengeIds =
        {
            "challenge_cat_quest",
            "challenge_stub_02",
            "challenge_stub_03",
            "challenge_stub_04",
            "challenge_stub_05",
            "challenge_stub_06"
        };

        private static readonly RiasecCode[] ChallengeTags =
        {
            RiasecCode.R,
            RiasecCode.I,
            RiasecCode.A,
            RiasecCode.S,
            RiasecCode.E,
            RiasecCode.C
        };

        private static readonly Color FilledStarColor = new(1f, 0.85f, 0.25f, 1f);
        private static readonly Color EmptyStarColor = new(1f, 1f, 1f, 0.18f);

        private static bool _sceneHookRegistered;

        private readonly List<ChallengeDefinition> _definitions = new();
        private readonly List<ChallengeRoundResult> _roundResults = new();
        private readonly int[] _activeRoundSkillUse = new int[6];
        private readonly InputAction[] _skillActions = new InputAction[6];
        private readonly Image[] _starImages = new Image[3];

        private ChallengeSessionPhase _state;
        private ChallengeSceneReferences _sceneReferences;
        private TMP_Text _roundCounterText;
        private TMP_Text _roundObjectiveText;
        private TMP_Text _debugText;
        private Button _nextRoundButton;
        private TMP_Text _nextRoundButtonLabel;
        private Transform _challengeHudRoot;
        private Transform _controlsCanvas;
        private Color _filledStarColor = FilledStarColor;
        private Color _emptyStarColor = EmptyStarColor;
        private string _sessionId = string.Empty;
        private string _startedAtIso = string.Empty;
        private float _debugTextHideAt = -1f;
        private float _sessionStartTime;
        private float _roundStartTime;
        private int _currentRoundIndex = -1;
        private int _currentRetryCount;
        private bool _initialized;

        public ChallengeSessionPhase CurrentState => _state;

        private void Start()
        {
            if (_initialized) return;

            ResolveSceneReferences();
            RegisterChallengeDefinitions();
            BindExistingSkillButtons();
            ConfigureSkillInput();
            ConfigureHud();
            StartNewSession();

            _initialized = true;
        }

        private void Update()
        {
            UpdateDebugTextVisibility();
            HandleKeyboardDebugShortcuts();
        }

        private void OnDestroy()
        {
            if (_nextRoundButton != null)
                _nextRoundButton.onClick.RemoveListener(HandleNextRoundClicked);

            for (int i = 0; i < _skillActions.Length; i++)
            {
                if (_skillActions[i] == null) continue;

                _skillActions[i].Disable();
                _skillActions[i].Dispose();
                _skillActions[i] = null;
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
