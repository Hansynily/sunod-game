using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_QuestTimer : MonoBehaviour
{
    [SerializeField] private float totalTime = 30f;
    [SerializeField] private Image[] starImages = new Image[5];
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private Color starFilledColor = Color.white;
    [SerializeField] private Color starEmptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private TMP_Text debugText;

    private float timeRemaining;
    private bool isRunning = false;
    private bool isComplete = false;
    private TMP_Text runtimeDebugText;

    private void Awake()
    {
        EnsureDebugTextReference();
    }

    public void StartQuest()
    {
        timeRemaining = Mathf.Max(0f, totalTime);
        isRunning = true;
        isComplete = false;
        UpdateDisplayedStars();
        UpdateDebugText();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;
        if (timeRemaining > 0f)
        {
            UpdateDisplayedStars();
            UpdateDebugText();
            return;
        }

        timeRemaining = 0f;
        UpdateDisplayedStars();
        UpdateDebugText();
        Fail();
    }

    public void CompleteQuest()
    {
        if (isComplete)
        {
            return;
        }

        isComplete = true;
        isRunning = false;

        int stars = GetStarCountForTimeRemaining();
        SetStarsForCount(stars);

        UpdateDebugText($"Quest complete | Time left: {timeRemaining:F1}s | Stars: {stars}/5");
    }

    public float GetTimeRemaining()
    {
        return timeRemaining;
    }

    public bool IsRunning()
    {
        return isRunning;
    }

    private void Fail()
    {
        isRunning = false;
        isComplete = true;
        SetAllStars(false);
        UpdateDebugText("Quest failed | Time left: 0.0s | Stars: 0/5");
        Debug.Log("Quest failed");
    }

    private void SetAllStars(bool filled)
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null)
            {
                continue;
            }

            SetStarState(starImages[i], filled);
        }
    }

    private void SetStarState(Image starImage, bool filled)
    {
        if (starImage == null)
        {
            return;
        }

        if (starFilled != null || starEmpty != null)
        {
            starImage.sprite = filled ? starFilled : starEmpty;
        }

        starImage.color = filled ? starFilledColor : starEmptyColor;
    }

    private void UpdateDisplayedStars()
    {
        if (!isRunning && !isComplete)
        {
            return;
        }

        SetStarsForCount(GetStarCountForTimeRemaining());
    }

    private void SetStarsForCount(int stars)
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null)
            {
                continue;
            }

            SetStarState(starImages[i], i < stars);
        }
    }

    private int GetStarCountForTimeRemaining()
    {
        float ratio = totalTime > 0f ? timeRemaining / totalTime : 0f;

        if (ratio >= 0.80f)
        {
            return 5;
        }

        if (ratio >= 0.60f)
        {
            return 4;
        }

        if (ratio >= 0.40f)
        {
            return 3;
        }

        if (ratio >= 0.20f)
        {
            return 2;
        }

        return timeRemaining > 0f ? 1 : 0;
    }

    private void UpdateDebugText(string overrideMessage = null)
    {
        TMP_Text targetText = GetActiveDebugText();
        if (targetText == null)
        {
            return;
        }

        targetText.text = string.IsNullOrEmpty(overrideMessage)
            ? $"Quest Time: {timeRemaining:F1}s"
            : overrideMessage;
    }

    private TMP_Text GetActiveDebugText()
    {
        EnsureDebugTextReference();

        if (debugText != null && debugText.gameObject.activeInHierarchy)
        {
            return debugText;
        }

        if (runtimeDebugText == null)
        {
            runtimeDebugText = CreateRuntimeDebugText();
        }

        return runtimeDebugText;
    }

    private void EnsureDebugTextReference()
    {
        if (debugText != null)
        {
            return;
        }

        TMP_Text[] textObjects = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < textObjects.Length; i++)
        {
            TMP_Text candidate = textObjects[i];
            if (candidate == null || candidate.name != "DebugText")
            {
                continue;
            }

            if (candidate.hideFlags != HideFlags.None || candidate.gameObject.scene.rootCount == 0)
            {
                continue;
            }

            debugText = candidate;
            return;
        }
    }

    private TMP_Text CreateRuntimeDebugText()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new("QuestTimerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject textObject = new("QuestTimerDebugText_Auto", typeof(RectTransform));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -140f);
        rect.sizeDelta = new Vector2(700f, 80f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }
}
