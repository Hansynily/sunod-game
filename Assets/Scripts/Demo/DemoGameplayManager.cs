using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.Demo
{
    // This will follow SRP (as standalone quest) soon as CatQuest is implemented properly.
    // Hardcoded UI elements 

    public partial class DemoGameplayManager : MonoBehaviour
    {
        private enum CatState
        {
            Idle,
            Fleeing,
            Following,
            Frozen
        }

        private class PawPrintData
        {
            public GameObject go;
            public SpriteRenderer renderer;
            public float expiresAt;
        }

        private const string DemoSceneName = "DemoPlayScene";
        private const float ToastDuration = 1.5f;
        private const float PawPrintLifetime = 3f;
        private const float PawPrintInterval = 0.5f;
        private const float RiverWorldX = 8f;
        private const float CatSpawnWorldX = 11.5f;
        private const float RiverWidth = 1.8f;
        private const float BridgeHeight = 0.5f;
        private const float BuildZoneWidth = 1.6f;
        private const float MimicFollowDuration = 0.5f;

        private static Sprite _whiteSprite;
        private static bool _sceneHookRegistered;

        private readonly bool[] _collected = new bool[6];
        private readonly int[] _skillUseCount = new int[6];
        private readonly int[] _firstUseOrder = new int[6];
        private readonly OnScreenButton[] _slotButtons = new OnScreenButton[4];
        private readonly Image[] _slotButtonImages = new Image[4];
        private readonly TMP_Text[] _slotButtonLabels = new TMP_Text[4];
        private readonly bool[] _slotUnlocked = new bool[4];
        private readonly int[] _slotSkillIndex = { -1, -1, -1, -1 };
        private readonly InputAction[] _slotActions = new InputAction[4];
        private readonly List<float> _bondExpirations = new();
        private readonly List<PawPrintData> _pawPrints = new();
        private readonly List<GameObject> _planDots = new();

        private AudioSource _audioSource;
        private AudioClip _mimicClip;
        private Transform _player;
        private GameObject _cat;
        private SpriteRenderer _catRenderer;
        private BoxCollider2D _riverBlocker;
        private BoxCollider2D _buildZone;
        private GameObject _bridge;
        private CatState _catState = CatState.Idle;
        private Rect _worldBounds;
        private Rect _catBounds;
        private Vector3 _riverCenter;

        private TMP_Text _toastText;
        private TMP_Text _objectiveText;
        private Image _xrayOverlay;
        private GameObject _winPanel;
        private Button _continueButton;
        private Coroutine _toastRoutine;

        private bool _initialized;
        private bool _buildUsed;
        private bool _trackActive;
        private bool _planUnlocked;
        private bool _winShown;
        private bool _submittingResults;
        private int _collectedSkillCount;
        private int _nextUseOrder;

        private float _catFollowUntil;
        private float _catFrozenUntil;
        private float _nextPawPrintAt;
        private float _nextPlanUpdateAt;
        private float _nextRequirementToastAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _sceneHookRegistered = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (_sceneHookRegistered) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapCurrentScene()
        {
            EnsureManagerForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureManagerForScene(scene);
        }

        private static void EnsureManagerForScene(Scene scene)
        {
            if (scene.name != DemoSceneName) return;
            if (FindAnyObjectByType<DemoGameplayManager>() != null) return;

            var go = new GameObject("[Demo Gameplay Manager]");
            go.AddComponent<DemoGameplayManager>();
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;
                Texture2D tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                return _whiteSprite;
            }
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != DemoSceneName)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < _firstUseOrder.Length; i++)
                _firstUseOrder[i] = int.MaxValue;

            GameSessionData.Reset();

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _mimicClip = CreateMimicClip();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
            {
                Debug.LogError("[DemoGameplay] Player not found in scene.");
                return;
            }

            DisableLegacySkillFlow();
            RemoveLegacyPickups();
            SetupSkillButtons();
            SetupInputActions();

            if (SessionState.Instance != null && string.IsNullOrWhiteSpace(SessionState.Instance.CurrentQuestId))
                SessionState.Instance.BeginRun("cat_demo_quest");

            BuildHud();
            BuildEnvironment();
            SpawnCollectibles();
            SpawnCat();
            UpdateObjectiveText();
            ShowToast("Collect at least 3 skills, then find the cat.");
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            UpdateBondTimers();
            UpdateCatBehavior();
            UpdatePawPrints();
            UpdatePlanIndicator();
            UpdateSkillButtonVisuals();
            TryTriggerWin();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slotActions.Length; i++)
            {
                if (_slotActions[i] == null) continue;
                _slotActions[i].Disable();
                _slotActions[i].Dispose();
                _slotActions[i] = null;
            }

            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinuePressed);
        }

        private void DisableLegacySkillFlow()
        {
            var playerSkillInput = _player.GetComponent<PlayerSkillInput>();
            if (playerSkillInput != null) playerSkillInput.enabled = false;

            var skillController = _player.GetComponent<SkillController>();
            if (skillController != null) skillController.enabled = false;
        }

        private void RemoveLegacyPickups()
        {
            SkillPickup[] pickups = FindObjectsByType<SkillPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SkillPickup pickup in pickups)
                Destroy(pickup.gameObject);
        }

        private void SetupSkillButtons()
        {
            OnScreenButton[] buttons = FindObjectsByType<OnScreenButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (OnScreenButton button in buttons)
            {
                int slot = SlotFromControlPath(button.controlPath);
                if (slot < 0 || slot > 3) continue;

                _slotButtons[slot] = button;
                _slotButtonImages[slot] = button.GetComponent<Image>();
                _slotButtonLabels[slot] = GetOrCreateButtonLabel(button.transform as RectTransform);
                button.gameObject.SetActive(false);
            }

            for (int i = 0; i < 4; i++)
            {
                if (_slotButtons[i] == null)
                    Debug.LogWarning($"[DemoGameplay] Missing on-screen button for Skill{i}.");
            }
        }

        private static int SlotFromControlPath(string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath)) return -1;
            if (controlPath.Contains("numpad0", StringComparison.OrdinalIgnoreCase)) return 0;
            if (controlPath.Contains("numpad1", StringComparison.OrdinalIgnoreCase)) return 1;
            if (controlPath.Contains("numpad2", StringComparison.OrdinalIgnoreCase)) return 2;
            if (controlPath.Contains("numpad3", StringComparison.OrdinalIgnoreCase)) return 3;
            return -1;
        }

        private TMP_Text GetOrCreateButtonLabel(RectTransform buttonRect)
        {
            if (buttonRect == null) return null;

            TMP_Text existing = buttonRect.GetComponentInChildren<TMP_Text>(true);
            if (existing != null) return existing;

            GameObject labelGo = new("SkillLabel", typeof(RectTransform));
            labelGo.transform.SetParent(buttonRect, false);

            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(120f, 24f);

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 12;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = string.Empty;
            label.raycastTarget = false;
            return label;
        }

        private void SetupInputActions()
        {
            string[] secondary = { "m", "j", "k", "l" };

            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                InputAction action = new($"Skill{slot}", InputActionType.Button);
                action.AddBinding($"<Keyboard>/numpad{slot}");
                action.AddBinding($"<Keyboard>/{secondary[slot]}");
                action.performed += _ => OnSkillPressed(slot);
                action.Enable();
                _slotActions[slot] = action;
            }
        }
    }
}
