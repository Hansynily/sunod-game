using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void SetupSkillButtons()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            _sceneReferences = FindSceneReferences(activeScene);

            if (_sceneReferences == null)
            {
                Debug.LogError("[DemoGameplay] DemoSceneReferences not found. Demo skill buttons must be bound from the scene.");
                return;
            }

            if (!_sceneReferences.HasDemoSkillReferences())
            {
                Debug.LogError("[DemoGameplay] DemoSceneReferences is missing one or more demo skill button references.");
            }

            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                OnScreenButton button = _sceneReferences.DemoSkillButtons != null && i < _sceneReferences.DemoSkillButtons.Length
                    ? _sceneReferences.DemoSkillButtons[i]
                    : null;
                TMP_Text label = _sceneReferences.DemoSkillLabels != null && i < _sceneReferences.DemoSkillLabels.Length
                    ? _sceneReferences.DemoSkillLabels[i]
                    : null;

                _slotButtons[i] = button;
                _slotButtonImages[i] = button != null ? button.GetComponent<Image>() : null;
                _slotButtonLabels[i] = label;

                if (button != null)
                {
                    Button clickable = button.GetComponent<Button>();
                    if (clickable == null)
                        clickable = button.gameObject.AddComponent<Button>();

                    clickable.transition = Selectable.Transition.None;
                    clickable.onClick.RemoveAllListeners();
                    clickable.onClick.AddListener(() => OnSkillPressed(slot));
                    button.gameObject.SetActive(false);
                }

                if (_slotButtons[i] == null)
                    Debug.LogWarning($"[DemoGameplay] Missing on-screen button for Skill{i}.");

                if (_slotButtonLabels[i] == null)
                    Debug.LogWarning($"[DemoGameplay] Missing label reference for Skill{i}.");

                if (_slotButtonImages[i] == null)
                    Debug.LogWarning($"[DemoGameplay] Skill{i} is missing an Image component.");
            }
        }

        private static DemoSceneReferences FindSceneReferences(Scene scene)
        {
            DemoSceneReferences[] references = FindObjectsByType<DemoSceneReferences>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < references.Length; i++)
            {
                DemoSceneReferences current = references[i];
                if (current == null || current.gameObject == null) continue;
                if (!current.gameObject.scene.IsValid()) continue;
                if (current.gameObject.scene.handle != scene.handle) continue;
                return current;
            }

            return null;
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
