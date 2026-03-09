using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void BuildHud()
        {
            GameObject canvasGo = new("DemoHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _xrayOverlay = CreatePanelImage(canvasGo.transform, "XRayOverlay", new Color(0f, 0.8f, 1f, 0.18f));
            _xrayOverlay.gameObject.SetActive(false);

            _objectiveText = CreateHudText(
                canvasGo.transform, "ObjectiveText",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -30f), new Vector2(0f, 80f),
                36, TextAlignmentOptions.Center
            );

            _toastText = CreateHudText(
                canvasGo.transform, "ToastText",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -110f), new Vector2(0f, 70f),
                34, TextAlignmentOptions.Center
            );
            _toastText.text = string.Empty;

            BuildWinDialog(canvasGo.transform);
        }

        private static Image CreatePanelImage(Transform parent, string name, Color color)
        {
            GameObject panel = new(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateHudText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textGo = new(name, typeof(RectTransform));
            textGo.transform.SetParent(parent, false);

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private void BuildWinDialog(Transform parent)
        {
            _winPanel = new GameObject("WinDialog", typeof(RectTransform), typeof(Image));
            _winPanel.transform.SetParent(parent, false);

            RectTransform panelRect = _winPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 360f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = _winPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);

            TMP_Text message = CreateHudText(
                _winPanel.transform, "Message",
                new Vector2(0f, 0.35f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                38, TextAlignmentOptions.Center
            );
            message.text = "You found the cat!\nYou used your skills well.";

            GameObject buttonGo = new("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_winPanel.transform, false);

            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.15f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.15f);
            buttonRect.sizeDelta = new Vector2(260f, 84f);
            buttonRect.anchoredPosition = Vector2.zero;

            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);

            _continueButton = buttonGo.GetComponent<Button>();
            _continueButton.onClick.AddListener(OnContinuePressed);

            TMP_Text buttonText = CreateHudText(
                buttonGo.transform, "Label",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                32, TextAlignmentOptions.Center
            );
            buttonText.text = "Continue";

            _winPanel.SetActive(false);
        }

        private void UpdateObjectiveText()
        {
            if (_objectiveText == null) return;

            string collectPart = $"Collect skills: {_collectedSkillCount}/3 minimum";
            string catPart = _collectedSkillCount >= 3
                ? "Objective: Find and approach the cat."
                : "Objective: Unlock more skills first.";

            _objectiveText.text = $"{collectPart}\n{catPart}";
        }

        private void ShowToast(string message)
        {
            if (_toastText == null) return;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _toastText.text = message;
            Color c = _toastText.color;
            c.a = 1f;
            _toastText.color = c;

            float t = 0f;
            while (t < ToastDuration)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, t / ToastDuration);
                c.a = alpha;
                _toastText.color = c;
                yield return null;
            }

            _toastText.text = string.Empty;
        }
    }
}