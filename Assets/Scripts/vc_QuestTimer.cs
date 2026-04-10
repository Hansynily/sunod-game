using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        EnsureDebugTextReference();
    }

    public void StartQuest()
    {
        timeRemaining = Mathf.Max(0f, totalTime);
        isRunning = true;
        isComplete = false;
        ResetFinalState();
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
