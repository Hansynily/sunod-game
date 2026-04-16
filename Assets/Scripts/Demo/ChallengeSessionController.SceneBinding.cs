using TMPro;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        private void ResolveSceneReferences()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            _sceneReferences = FindSceneReferences(activeScene);

            if (_sceneReferences != null)
            {
                _challengeHudRoot = _sceneReferences.ChallengeHudRoot;
                _controlsCanvas = _sceneReferences.ControlsCanvas;
                _roundCounterText = _sceneReferences.RoundCounterText;
                _roundObjectiveText = _sceneReferences.RoundObjectiveText;
                _debugText = _sceneReferences.DebugText;
                _nextRoundButton = _sceneReferences.NextRoundButton;
                _nextRoundButtonLabel = _sceneReferences.NextRoundButtonLabel;
                _filledStarColor = _sceneReferences.FilledStarColor;
                _emptyStarColor = _sceneReferences.EmptyStarColor;

                Image[] starImages = _sceneReferences.StarImages;
                for (int i = 0; i < _starImages.Length; i++)
                    _starImages[i] = starImages != null && i < starImages.Length ? starImages[i] : null;

                if (_nextRoundButton != null && _nextRoundButtonLabel == null)
                    _nextRoundButtonLabel = _nextRoundButton.GetComponentInChildren<TMP_Text>(true);
            }
            else
            {
                _challengeHudRoot = FindSceneObjectByName(ChallengeHudName, activeScene)?.transform;
                _controlsCanvas = FindSceneObjectByName(ControlsCanvasName, activeScene)?.transform;

                _roundCounterText = FindSceneObjectByName(RoundCounterName, activeScene)?.GetComponent<TMP_Text>();
                _roundObjectiveText = FindSceneObjectByName(RoundObjectiveName, activeScene)?.GetComponent<TMP_Text>();
                _debugText = FindSceneObjectByName(DebugTextName, activeScene)?.GetComponent<TMP_Text>();

                GameObject starsDisplay = FindSceneObjectByName(StarsDisplayName, activeScene);
                if (starsDisplay != null)
                {
                    _starImages[0] = FindChildImage(starsDisplay.transform, "Star_1");
                    _starImages[1] = FindChildImage(starsDisplay.transform, "Star_2");
                    _starImages[2] = FindChildImage(starsDisplay.transform, "Star_3");
                }

                _nextRoundButton = FindSceneObjectByName(NextRoundButtonName, activeScene)?.GetComponent<Button>();
                if (_nextRoundButton != null)
                    _nextRoundButtonLabel = _nextRoundButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (_nextRoundButton != null)
            {
                _nextRoundButton.onClick.RemoveListener(HandleNextRoundClicked);
                _nextRoundButton.onClick.AddListener(HandleNextRoundClicked);
            }
        }

        private void RegisterChallengeDefinitions()
        {
            _definitions.Clear();

            ChallengeStarThresholds defaultThresholds = new ChallengeStarThresholds
            {
                three_star_max_retry_count = 0,
                two_star_max_retry_count = 1,
                one_star_max_retry_count = 2,
                zero_star_min_retry_count = 3
            };

            Scene activeScene = SceneManager.GetActiveScene();
            for (int i = 0; i < ChallengeIds.Length; i++)
            {
                GameObject contentRoot = _sceneReferences != null &&
                                         _sceneReferences.ChallengeRoots != null &&
                                         i < _sceneReferences.ChallengeRoots.Length
                    ? _sceneReferences.ChallengeRoots[i]
                    : FindSceneObjectByName(ChallengeRootNames[i], activeScene);
                _definitions.Add(new ChallengeDefinition(ChallengeIds[i], ChallengeTags[i], contentRoot, defaultThresholds));
            }
        }

        private void ConfigureHud()
        {
            if (_challengeHudRoot != null)
                _challengeHudRoot.gameObject.SetActive(true);

            if (_nextRoundButtonLabel != null)
                _nextRoundButtonLabel.text = "Next Round";

            SetNextRoundButtonVisible(false);
            SetStarsDisplay(0);
            if (_debugText != null)
                _debugText.gameObject.SetActive(false);
        }

        private void BindExistingSkillButtons()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (_controlsCanvas == null)
            {
                Debug.LogWarning("[ChallengeSession] Controls UI canvas not found. Existing skill buttons were not bound, but keyboard input still works.");
                return;
            }

            OnScreenButton[] skillButtons = _sceneReferences?.SkillButtons;
            for (int i = 0; i < 6; i++)
            {
                OnScreenButton onScreenButton = skillButtons != null && i < skillButtons.Length
                    ? skillButtons[i]
                    : FindSceneObjectByName($"Skill{i}", activeScene)?.GetComponent<OnScreenButton>();
                if (onScreenButton == null)
                    Debug.LogWarning($"[ChallengeSession] Existing skill button 'Skill{i}' is missing an OnScreenButton component.");
            }
        }

        private void ConfigureSkillInput()
        {
            for (int i = 0; i < _skillActions.Length; i++)
            {
                if (_skillActions[i] != null)
                {
                    _skillActions[i].Disable();
                    _skillActions[i].Dispose();
                }

                int skillIndex = i;
                InputAction action = new($"ChallengeSkill_{skillIndex}", InputActionType.Button);
                action.AddBinding($"<Keyboard>/numpad{skillIndex}");
                action.performed += _ => HandleSkillPressed(skillIndex);
                action.Enable();

                _skillActions[i] = action;
            }
        }

        private void HandleSkillPressed(int skillIndex)
        {
            if (_externallyPaused) return;
            if (_state != ChallengeSessionPhase.RoundActive) return;
            if (skillIndex < 0 || skillIndex >= 6) return;

            SetDebugText($"{GetSkillDebugLabel(skillIndex)} pressed.");

            DemoGameplayManager demoGameplayManager = FindFirstObjectByType<DemoGameplayManager>();
            if (demoGameplayManager != null && demoGameplayManager.enabled)
            {
                demoGameplayManager.TriggerSkill(skillIndex);
                return;
            }

            RecordSkillUse((RiasecCode)skillIndex);
        }

        private void ApplyChallengeRootVisibility(int activeIndex)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                GameObject contentRoot = _definitions[i].content_root;
                if (contentRoot == null) continue;

                contentRoot.SetActive(i == activeIndex);
            }
        }

        private void UpdateHudForRound(ChallengeDefinition definition, int roundIndex)
        {
            if (_roundCounterText != null)
                _roundCounterText.text = $"Round {roundIndex + 1} / {_definitions.Count}";

            if (_roundObjectiveText != null)
                _roundObjectiveText.text = BuildObjectiveText(definition.challenge_id);
        }

        private static string BuildObjectiveText(string challengeId)
        {
            return challengeId switch
            {
                "challenge_cat_quest" => "Find the cat.",
                "challenge_stub_02" => "Stub round 2.",
                "challenge_stub_03" => "Stub round 3.",
                "challenge_stub_04" => "Stub round 4.",
                "challenge_stub_05" => "Stub round 5.",
                "challenge_stub_06" => "Stub round 6.",
                _ => "Complete the active challenge to continue."
            };
        }

        private void SetNextRoundButtonVisible(bool isVisible)
        {
            if (_nextRoundButton == null) return;

            _nextRoundButton.interactable = isVisible;
            _nextRoundButton.gameObject.SetActive(isVisible);
        }

        private void SetStarsDisplay(int starsEarned)
        {
            for (int i = 0; i < _starImages.Length; i++)
            {
                if (_starImages[i] == null) continue;
                _starImages[i].color = i < starsEarned ? _filledStarColor : _emptyStarColor;
            }
        }

        private static ChallengeSceneReferences FindSceneReferences(Scene scene)
        {
            ChallengeSceneReferences[] references = FindObjectsByType<ChallengeSceneReferences>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < references.Length; i++)
            {
                ChallengeSceneReferences current = references[i];
                if (current == null || current.gameObject == null) continue;
                if (!current.gameObject.scene.IsValid()) continue;
                if (current.gameObject.scene.handle != scene.handle) continue;
                return current;
            }

            return null;
        }

        private static GameObject FindSceneObjectByName(string objectName, Scene scene)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || current.gameObject == null) continue;
                if (current.name != objectName) continue;
                if (!current.gameObject.scene.IsValid()) continue;
                if (current.gameObject.scene.handle != scene.handle) continue;

                return current.gameObject;
            }

            return null;
        }

        private static Image FindChildImage(Transform parent, string childName)
        {
            if (parent == null) return null;

            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name != childName) continue;
                return children[i].GetComponent<Image>();
            }

            return null;
        }
    }
}
