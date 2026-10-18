using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SunodGame.Core;

[DisallowMultipleComponent]
public class vc_QuestTimer : MonoBehaviour
{
    public sealed class QuestCompletionResult
    {
        public QuestCompletionResult(int finalStarsEarned, int finalStarsEarnedNormalized, bool didPassQuest, float finalTimeRemaining, float totalTimeSpent)
        {
            FinalStarsEarned = finalStarsEarned;
            FinalStarsEarnedNormalized = finalStarsEarnedNormalized;
            DidPassQuest = didPassQuest;
            FinalTimeRemaining = finalTimeRemaining;
            TotalTimeSpent = totalTimeSpent;
        }

        public int FinalStarsEarned { get; }
        public int FinalStarsEarnedNormalized { get; }
        public bool DidPassQuest { get; }
        public bool DidFailQuest => !DidPassQuest;
        public float FinalTimeRemaining { get; }
        public float TotalTimeSpent { get; }
    }

    [SerializeField] private float totalTime = 30f;
    [SerializeField] private Image[] starImages = new Image[5];
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private Color starFilledColor = Color.white;
    [SerializeField] private Color starEmptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private TMP_Text debugText;

    public static vc_QuestTimer Instance { get; private set; }

    private float timeRemaining;
    private bool isRunning = false;
    private bool isComplete = false;

    public int FinalStarsEarned { get; private set; }
    public int FinalStarsEarnedNormalized { get; private set; }
    public bool DidPassQuest { get; private set; }
    public bool DidFailQuest => isComplete && !DidPassQuest;
    public float FinalTimeRemaining { get; private set; }
    public float TotalTimeSpent { get; private set; }
    public QuestCompletionResult FinalResult { get; private set; }

    public event Action<QuestCompletionResult> QuestEnded;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        CacheStarScales();
        EnsureDebugTextReference();
        BuildTimeBar();
    }

    public void StartQuest()
    {
        timeRemaining = Mathf.Max(0f, totalTime);
        isRunning = true;
        isComplete = false;
        _shownStars = -1;
        StopAllCoroutines();
        ResetStarTransforms();
        ResetFinalState();
        UpdateDisplayedStars();
        UpdateDebugText();
        UpdateTimeBar();
        MaybeShowTimerTip();
    }

    /// <summary>Shows or hides the star row (town mode hides it while exploring).</summary>
    public void SetStarsVisible(bool visible)
    {
        if (!visible) { StopAllCoroutines(); ResetStarTransforms(); }
        // Town mode draws its own star row + bar (vc_RunProgressUI): the old uGUI row sits
        // above the top of the screen in that HUD, so it stays off there.
        vc_RunProgressUI town = vc_RunProgressUI.Instance;
        foreach (Image star in starImages)
            if (star != null) star.enabled = visible && town == null;
        if (_barRoot != null) _barRoot.SetActive(visible && town == null);
        if (town != null)
        {
            town.SetTimerVisible(visible);
            if (visible) { town.SetTimerStars(Mathf.Max(0, _shownStars < 0 ? 5 : _shownStars), false); UpdateTimeBar(); }
        }
    }

    // ── Time bar: drains under the stars so the countdown is easy to read ──
    // Tick marks show where each star goes out (80/60/40/20 %). The fill changes colour with
    // the star count and blinks red for the last star.

    private const string TimerTipPrefKey = "sunod.timer_tip_seen";
    private GameObject _barRoot;
    private Image _barFill;
    private bool _tipHold;   // first quest only: the clock waits while the tip is open

    private static Sprite _pixel;
    private static Sprite Pixel
    {
        get
        {
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return _pixel;
        }
    }

    private void BuildTimeBar()
    {
        if (starImages == null || starImages.Length == 0 || starImages[0] == null) return;
        RectTransform first = starImages[0].rectTransform, last = starImages[starImages.Length - 1] != null ? starImages[starImages.Length - 1].rectTransform : first;
        Transform parent = first.parent;

        float starHalf = first.sizeDelta.x * first.localScale.x * 0.5f;
        float left = first.anchoredPosition.x - starHalf, right = last.anchoredPosition.x + starHalf;
        float width = right - left, y = first.anchoredPosition.y - starHalf - 9f;
        const float h = 10f, border = 2f;

        _barRoot = new GameObject("TimeBar", typeof(RectTransform));
        var rt = (RectTransform)_barRoot.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = first.anchorMin;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((left + right) * 0.5f, y);
        rt.sizeDelta = new Vector2(width + border * 2f, h + border * 2f);

        Image frame = MakeBarImage("Frame", rt, Vector2.zero, rt.sizeDelta, new Color32(70, 46, 34, 255));
        MakeBarImage("Back", rt, Vector2.zero, new Vector2(width, h), new Color32(246, 232, 200, 255));
        _barFill = MakeBarImage("Fill", rt, Vector2.zero, new Vector2(width, h), Color.white);
        _barFill.type = Image.Type.Filled;
        _barFill.fillMethod = Image.FillMethod.Horizontal;
        _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        for (int i = 1; i <= 4; i++)
            MakeBarImage("Tick", rt, new Vector2(-width * 0.5f + width * i * 0.2f, 0f), new Vector2(border, h), new Color32(70, 46, 34, 200));
        frame.raycastTarget = false;
        _barRoot.SetActive(false);
    }

    private static Image MakeBarImage(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = Pixel;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static readonly Color32[] BarColors =
    {
        new Color32(214, 64, 52, 255),   // 1 star left
        new Color32(240, 128, 40, 255),  // 2
        new Color32(240, 190, 50, 255),  // 3
        new Color32(150, 200, 70, 255),  // 4
        new Color32(80, 180, 90, 255),   // 5
    };

    private void UpdateTimeBar()
    {
        float ratio = totalTime > 0f ? Mathf.Clamp01(timeRemaining / totalTime) : 0f;
        vc_RunProgressUI.Instance?.SetTimerFill(ratio, GetStarCountForTimeRemaining(), isRunning);
        if (_barFill == null) return;
        // whole-pixel steps, like the rest of the pixel art
        float w = _barFill.rectTransform.sizeDelta.x;
        _barFill.fillAmount = w > 0f ? Mathf.Round(ratio * w) / w : ratio;
        int stars = Mathf.Clamp(GetStarCountForTimeRemaining(), 1, 5);
        Color c = BarColors[stars - 1];
        if (stars == 1 && isRunning && Mathf.Repeat(Time.time, 0.5f) < 0.25f) c = new Color32(255, 236, 210, 255);
        _barFill.color = c;
    }

    /// <summary>
    /// First quest ever on this device: explain the countdown before it starts. The clock
    /// waits while the tip is open, then runs as normal.
    /// </summary>
    private void MaybeShowTimerTip()
    {
        if (PlayerPrefs.GetInt(TimerTipPrefKey, 0) == 1 || vc_DialogPanel.Instance == null || vc_TownStage.Current == null) return;
        _tipHold = true;
        if (vc_RunProgressUI.Instance != null && vc_RunProgressUI.Instance.TimerElement != null)
            vc_UIPointer.PointAt(vc_RunProgressUI.Instance.TimerElement);
        else
            vc_UIPointer.PointAtWorldUI(_barRoot != null ? _barRoot.transform as RectTransform : null);
        vc_DialogPanel.Instance.ShowMessage(
            "Beat the clock!",
            "The stars at the top are your timer. They start full, and one goes out every few seconds while you work.\n\n" +
            "The bar under them drains too. The faster you finish, the more stars you keep. If the bar runs out, time's up and the quest ends.",
            () =>
            {
                _tipHold = false;
                vc_UIPointer.Hide();
                PlayerPrefs.SetInt(TimerTipPrefKey, 1);
                PlayerPrefs.Save();
            });
    }

    /// <summary>Debug menu: stops the countdown while true.</summary>
    public bool DebugFrozen { get; set; }

    /// <summary>Debug menu: adds (or removes) seconds on the running timer.</summary>
    public void DebugAddTime(float seconds)
    {
        if (!isRunning) return;
        timeRemaining = Mathf.Clamp(timeRemaining + seconds, 0.01f, totalTime);
        UpdateDisplayedStars();
    }

    /// <summary>Debug menu: ends the running quest as a fail, same as running out of time.</summary>
    public void DebugForceFail() => AbortQuest();

    /// <summary>
    /// Ends the running quest as not completed right away - used when the player cancels
    /// ("Not for me") from inside a quest. Scores like a fail (item = 1).
    /// </summary>
    public void AbortQuest()
    {
        if (!isRunning) return;
        timeRemaining = 0f;
        UpdateDisplayedStars();
        Fail();
    }

    private void Update()
    {
        if (!isRunning || DebugFrozen || _tipHold)
        {
            return;
        }

        float before = timeRemaining;
        timeRemaining -= Time.deltaTime;
        // Wood-block tick on each of the last 5 seconds.
        if (timeRemaining > 0f && timeRemaining <= 5f && Mathf.CeilToInt(before) != Mathf.CeilToInt(timeRemaining))
            vc_Sfx.Play("timer_tick", 0.7f);
        if (timeRemaining > 0f)
        {
            UpdateDisplayedStars();
            UpdateDebugText();
            UpdateTimeBar();
            return;
        }

        timeRemaining = 0f;
        UpdateDisplayedStars();
        UpdateDebugText();
        UpdateTimeBar();
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
        StoreFinalResult(true, stars, timeRemaining);

        UpdateDebugText($"Quest complete | Time left: {timeRemaining:F1}s | Stars: {stars}/5");
        QuestEnded?.Invoke(FinalResult);
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
        StoreFinalResult(false, 0, 0f);
        UpdateDebugText("Quest failed | Time left: 0.0s | Stars: 0/5");
        QuestEnded?.Invoke(FinalResult);
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

    private int _shownStars = -1;
    private Vector3[] _starBaseScale;

    /// <summary>Remember each star's authored scale once (they're 0.4 in the town HUD), and make
    /// the stars click-through so they never block the pause button.</summary>
    private void CacheStarScales()
    {
        if (_starBaseScale != null && _starBaseScale.Length == starImages.Length) return;
        _starBaseScale = new Vector3[starImages.Length];
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            _starBaseScale[i] = starImages[i].rectTransform.localScale;
            starImages[i].raycastTarget = false;
        }
    }

    private void ResetStarTransforms()
    {
        CacheStarScales();
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            starImages[i].rectTransform.localScale = _starBaseScale[i];
            starImages[i].rectTransform.localRotation = Quaternion.identity;
        }
    }

    private void SetStarsForCount(int stars)
    {
        // A star just went out: give it a little drop so the player notices.
        if (isRunning && _shownStars > stars && stars >= 0 && stars < starImages.Length && starImages[stars] != null)
            StartCoroutine(PopLostStar(starImages[stars].rectTransform, stars));
        vc_RunProgressUI.Instance?.SetTimerStars(stars, isRunning);
        _shownStars = stars;

        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null)
            {
                continue;
            }

            SetStarState(starImages[i], i < stars);
        }
    }

    private System.Collections.IEnumerator PopLostStar(RectTransform star, int index)
    {
        if (star == null) yield break;
        vc_Sfx.Play("timer_tick", 0.5f);
        CacheStarScales();
        Vector3 baseScale = _starBaseScale[index];
        const float dur = 0.35f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            float s = 1f + 0.45f * Mathf.Sin(k * Mathf.PI);          // swell then settle
            star.localScale = baseScale * s;
            star.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 3f) * 14f * (1f - k));
            yield return null;
        }
        star.localScale = baseScale;
        star.localRotation = Quaternion.identity;
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

    private void ResetFinalState()
    {
        FinalStarsEarned = 0;
        FinalStarsEarnedNormalized = 0;
        DidPassQuest = false;
        FinalTimeRemaining = 0f;
        TotalTimeSpent = 0f;
        FinalResult = null;
    }

    private void StoreFinalResult(bool didPassQuest, int finalStarsEarned, float finalTimeRemaining)
    {
        FinalStarsEarned = Mathf.Clamp(finalStarsEarned, 0, 5);
        FinalStarsEarnedNormalized = NormalizeStarsToThreeStar(FinalStarsEarned);
        DidPassQuest = didPassQuest;
        FinalTimeRemaining = Mathf.Max(0f, finalTimeRemaining);
        TotalTimeSpent = Mathf.Max(0f, totalTime - FinalTimeRemaining);
        FinalResult = new QuestCompletionResult(
            FinalStarsEarned,
            FinalStarsEarnedNormalized,
            DidPassQuest,
            FinalTimeRemaining,
            TotalTimeSpent);
    }

    private static int NormalizeStarsToThreeStar(int fiveStarCount)
    {
        if (fiveStarCount <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.RoundToInt((fiveStarCount / 5f) * 3f), 0, 3);
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
        return debugText != null && debugText.gameObject.activeInHierarchy
            ? debugText
            : null;
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
            if (candidate == null || (candidate.name != "QuestInfo" && candidate.name != "DebugText"))
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
}
