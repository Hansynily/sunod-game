using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_RunCompleteZone : MonoBehaviour
{
    [SerializeField] private Button triggerButton;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private string nextSceneName = "EndScene";
    [SerializeField] private string loadingMessage = "Analyzing your journey...";

    private string defaultButtonLabel = string.Empty;
    private bool isPredictionRunning;

    private void Awake()
    {
        if (triggerButton == null)
        {
            triggerButton = GetComponent<Button>();
        }

        if (buttonLabel == null)
        {
            buttonLabel = GetComponentInChildren<TMP_Text>(true);
        }

        defaultButtonLabel = buttonLabel != null ? buttonLabel.text : string.Empty;
    }

    private void OnEnable()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(HandleTriggered);
        }
    }

    private void OnDisable()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveListener(HandleTriggered);
        }
    }

    private void HandleTriggered()
    {
        if (isPredictionRunning)
        {
            return;
        }

        vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null)
        {
            Debug.LogWarning("[vc_RunCompleteZone] Session telemetry is unavailable.");
            LoadEndScene();
            return;
        }

        if (!AreAllQuestsRecorded(telemetry))
        {
            Debug.LogWarning("[vc_RunCompleteZone] Not all quests are complete yet. Prediction was skipped.");
            return;
        }

        StartCoroutine(SubmitPredictionAndLoad(telemetry));
    }

    private IEnumerator SubmitPredictionAndLoad(vc_SessionTelemetry telemetry)
    {
        isPredictionRunning = true;
        SetLoadingState(true);

        bool runSummarySucceeded = false;
        string runSummaryError = null;

        yield return telemetry.SubmitRunSummary(
            success =>
            {
                runSummarySucceeded = success != null && success.success;
            },
            error =>
            {
                runSummaryError = error;
            });

        if (!runSummarySucceeded && !string.IsNullOrWhiteSpace(runSummaryError))
        {
            Debug.LogWarning($"[vc_RunCompleteZone] Run summary submission failed: {runSummaryError}");
        }

        int predictedCluster = -1;
        bool receivedResult = false;

        yield return telemetry.SubmitAndPredict(cluster =>
        {
            predictedCluster = cluster;
            receivedResult = true;
        });

        if (!receivedResult)
        {
            predictedCluster = telemetry.PredictedCluster;
        }

        Debug.Log($"[vc_RunCompleteZone] Prediction complete. Cluster={predictedCluster}.");
        SetLoadingState(false);
        isPredictionRunning = false;
        LoadEndScene();
    }

    private bool AreAllQuestsRecorded(vc_SessionTelemetry telemetry)
    {
        vc_QuestRoom[] rooms = FindObjectsByType<vc_QuestRoom>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (rooms.Length == 0)
            return true;

        var recorded = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<vc_SessionTelemetry.QuestRecord> records = telemetry.GetAllRecords();
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null && !string.IsNullOrWhiteSpace(records[i].questId))
                recorded.Add(records[i].questId);
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(rooms[i].QuestId) && !recorded.Contains(rooms[i].QuestId))
                return false;
        }

        return true;
    }

    private void SetLoadingState(bool isLoading)
    {
        if (triggerButton != null)
        {
            triggerButton.interactable = !isLoading;
        }

        if (buttonLabel != null)
        {
            buttonLabel.text = isLoading ? loadingMessage : defaultButtonLabel;
        }
    }

    private void LoadEndScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[vc_RunCompleteZone] Next scene name is empty.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
