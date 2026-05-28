#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SunodGame.Core;
using SunodGame.Models;

[DisallowMultipleComponent]
public class vc_DebugSkipToEnd : MonoBehaviour
{
    private static readonly string[] QuestIds = {
        "L1_CatQuest",    "L1_LostFriend",
        "L2_MissingKey",  "L2_CatMedicine",
        "L3_FallenSparrow", "L3_BlockedPath", "L3_SlipperyWay"
    };

    private static readonly string[] QuestNames = {
        "Cat Quest",      "Lost Friend",
        "Missing Key",    "Cat Medicine",
        "Fallen Sparrow", "Blocked Path", "Slippery Way"
    };

    private static readonly string[] QuestRiasec = {
        "S", "C", "R", "R", "I", "C", "E"
    };

    private static readonly string[] RiasecLetters = { "R", "I", "A", "S", "E", "C" };

    private bool _isRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var root = new GameObject("[vc_DebugSkipToEnd]");
        root.AddComponent<vc_DebugSkipToEnd>();
        DontDestroyOnLoad(root);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            TrySkip();
    }

    private void TrySkip()
    {
        if (_isRunning) return;

        if (SessionState.Instance == null || !SessionState.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[DebugSkip] Not logged in. Sign in first so the run gets recorded under your account.");
            return;
        }

        StartCoroutine(RunSkip());
    }

    private IEnumerator RunSkip()
    {
        _isRunning = true;

        var telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null)
        {
            Debug.LogWarning("[DebugSkip] vc_SessionTelemetry unavailable.");
            _isRunning = false;
            yield break;
        }

        telemetry.StartSession();

        for (int i = 0; i < QuestIds.Length; i++)
        {
            bool  done    = Random.value > 0.15f;
            int   stars   = done ? Random.Range(1, 4) : 0;
            float time    = Random.Range(20f, 90f);
            float timeRem = done ? Random.Range(0f, 30f) : 0f;
            string riasec = QuestRiasec[i];

            var usage = new Dictionary<string, int>();
            foreach (var r in RiasecLetters)
                usage[r] = Random.Range(0, 6);

            telemetry.RecordQuestResult(
                QuestIds[i], QuestNames[i], riasec,
                done, stars, timeRem, time, usage);
        }

        Debug.Log("[DebugSkip] Fake quests injected. Submitting to backend...");

        bool runSummarySucceeded = false;
        yield return telemetry.SubmitRunSummary(
            success => { runSummarySucceeded = success != null && success.success; },
            error   => { Debug.LogWarning($"[DebugSkip] Run summary failed: {error}"); });

        if (!runSummarySucceeded)
            Debug.LogWarning("[DebugSkip] Run summary did not succeed — prediction will still run.");

        yield return telemetry.SubmitAndPredict(cluster =>
            Debug.Log($"[DebugSkip] Prediction done. Cluster={cluster}"));

        Debug.Log("[DebugSkip] Done. Loading EndScene.");
        _isRunning = false;
        SceneLoader.GoToEnd();
    }
}
#endif
