using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
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
