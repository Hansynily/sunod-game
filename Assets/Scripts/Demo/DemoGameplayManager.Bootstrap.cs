using SunodGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
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
            if (scene.name != PlaySceneName) return;
            if (FindSceneReferences(scene) == null) return;
            if (FindAnyObjectByType<DemoGameplayManager>() != null) return;

            var go = new GameObject("[Demo Gameplay Manager]");
            go.AddComponent<DemoGameplayManager>();
        }

        private void Awake()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != PlaySceneName || FindSceneReferences(activeScene) == null)
            {
                Destroy(gameObject);
                return;
            }

            _useChallengeSession = ChallengeSessionController.UsesChallengeSessionShell(activeScene);
            _contentRoot = _useChallengeSession
                ? GameObject.Find("Challenge_01")?.transform
                : null;

            for (int i = 0; i < _firstUseOrder.Length; i++)
                _firstUseOrder[i] = int.MaxValue;

            if (!_useChallengeSession)
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

            SetupSkillButtons();
            SetupInputActions();

            if (!_useChallengeSession &&
                SessionState.Instance != null &&
                string.IsNullOrWhiteSpace(SessionState.Instance.CurrentQuestId))
                SessionState.Instance.BeginRun("cat_demo_quest");

            BuildHud();
            BuildEnvironment();

            SpawnCat();
            UpdateObjectiveText();
            if (!_useChallengeSession)
                ShowToast("Find and approach the cat.");
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
    }
}
