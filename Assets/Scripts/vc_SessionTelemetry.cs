using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public class vc_SessionTelemetry : MonoBehaviour
{
    [Serializable]
    public class QuestRecord
    {
        public string questId;
        public string questName;
        public string primaryRiasec;
        public bool completed;
        public int starsEarned;
        public float finalTimeRemainingSeconds;
        public float timeSpentSeconds;
        public Dictionary<string, int> skillUsageCounts = CreateEmptySkillUsageCounts();
    }

    [Serializable]
    public class SessionSummary
    {
        public string sessionId;
        public string playerId;
        public string username;
        public DateTime sessionStartUtc;
        public int totalQuestCount;
        public int completedQuestCount;
        public int failedQuestCount;
        public int totalStarsEarned;
        public float totalTimeSpentSeconds;
        public Dictionary<string, int> totalSkillUsageCounts = CreateEmptySkillUsageCounts();
    }

    public static vc_SessionTelemetry Instance { get; private set; }

    [SerializeField] private List<QuestRecord> questRecords = new List<QuestRecord>();

    public string SessionId { get; private set; }
    public string PlayerId { get; private set; }
    public string Username { get; private set; }
    public DateTime SessionStartUtc { get; private set; }
    public int PredictedCluster { get; private set; } = -1;
    public int PredictedCareerCluster { get; private set; } = -1;
    public string PredictedCareerResult { get; private set; } = string.Empty;
    public string PredictedClusterLabel { get; private set; } = string.Empty;
    public string PredictedCareerFamily { get; private set; } = string.Empty;
    public string PredictedClusterHollandCode { get; private set; } = string.Empty;
    public string PredictedSource { get; private set; } = string.Empty;
    public string PredictedModelVersion { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject root = new("[vc_SessionTelemetry]");
        root.AddComponent<vc_SessionTelemetry>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartSession()
    {
        RefreshPlayerIdentity();
        if (!string.IsNullOrWhiteSpace(SessionId) && questRecords.Count == 0 && PredictedCluster == -1)
        {
            return;
        }

        SessionId = Guid.NewGuid().ToString();
        SessionStartUtc = DateTime.UtcNow;
        questRecords.Clear();
        PredictedCluster = -1;
        PredictedCareerCluster = -1;
        PredictedCareerResult = string.Empty;
        PredictedClusterLabel = string.Empty;
        PredictedCareerFamily = string.Empty;
        PredictedClusterHollandCode = string.Empty;
        PredictedSource = string.Empty;
        PredictedModelVersion = string.Empty;
        Debug.Log($"[Telemetry] Session started. ID: {SessionId}");
    }

    public void CancelCurrentSession()
    {
        questRecords.Clear();
        SessionId = string.Empty;
        SessionStartUtc = default;
        PredictedCluster = -1;
        PredictedCareerCluster = -1;
        PredictedCareerResult = string.Empty;
        PredictedClusterLabel = string.Empty;
        PredictedCareerFamily = string.Empty;
        PredictedClusterHollandCode = string.Empty;
        PredictedSource = string.Empty;
        PredictedModelVersion = string.Empty;
        RefreshPlayerIdentity();
        Debug.Log("[Telemetry] Session canceled.");
    }

    public void RecordQuestResult(string questId, string questName, string primaryRiasec, bool completed, int starsEarned, float finalTimeRemainingSeconds, float timeSpentSeconds, IDictionary<string, int> skillUsageCounts)
    {
        EnsureSessionStarted();
        RefreshPlayerIdentity();

        questRecords.Add(new QuestRecord
        {
            questId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim(),
            questName = string.IsNullOrWhiteSpace(questName) ? string.Empty : questName.Trim(),
            primaryRiasec = NormalizeRiasecLetter(primaryRiasec) ?? string.Empty,
            completed = completed,
            starsEarned = Mathf.Clamp(starsEarned, 0, 3),
            finalTimeRemainingSeconds = Mathf.Max(0f, finalTimeRemainingSeconds),
            timeSpentSeconds = Mathf.Max(0f, timeSpentSeconds),
            skillUsageCounts = NormalizeSkillUsageCounts(skillUsageCounts)
        });

        Debug.Log($"[Telemetry] Quest recorded: {questId} | Stars: {starsEarned} | Completed: {completed}");
    }

    public IReadOnlyList<QuestRecord> GetAllRecords()
    {
        return questRecords.AsReadOnly();
    }

    public SessionSummary GetSessionSummary()
    {
        EnsureSessionStarted();
        RefreshPlayerIdentity();

        SessionSummary summary = new SessionSummary
        {
            sessionId = SessionId,
            playerId = PlayerId,
            username = Username,
            sessionStartUtc = SessionStartUtc,
            totalQuestCount = questRecords.Count
        };

        for (int i = 0; i < questRecords.Count; i++)
        {
            QuestRecord record = questRecords[i];
            if (record == null)
            {
                continue;
            }

            if (record.completed)
            {
                summary.completedQuestCount++;
            }
            else
            {
                summary.failedQuestCount++;
            }

            summary.totalStarsEarned += Mathf.Clamp(record.starsEarned, 0, 3);
            summary.totalTimeSpentSeconds += Mathf.Max(0f, record.timeSpentSeconds);

            MergeSkillUsageCounts(summary.totalSkillUsageCounts, record.skillUsageCounts);
        }

        return summary;
    }

    public IEnumerator SubmitAndPredict(Action<int> onResult)
    {
        EnsureSessionStarted();
        PredictedCluster = -1;
        PredictedCareerCluster = -1;
        PredictedCareerResult = string.Empty;
        PredictedClusterLabel = string.Empty;
        PredictedCareerFamily = string.Empty;
        PredictedClusterHollandCode = string.Empty;
        PredictedSource = string.Empty;
        PredictedModelVersion = string.Empty;
        float[] features = vc_RiasecAdapter.BuildModelInput(GetSessionSummary(), GetAllRecords());
        Debug.Log($"[Adapter] float[48]: {string.Join(", ", features)}");
        string baseUrl = ResolvePredictionBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Debug.LogError("[vc_SessionTelemetry] Prediction failed because no backend base URL is configured.");
            onResult?.Invoke(PredictedCluster);
            yield break;
        }

        PredictionRequestPayload payload = new PredictionRequestPayload
        {
            features = features
        };

        using UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/predict", UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 15;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[vc_SessionTelemetry] Prediction request failed: {BuildRequestError(request)}");
            onResult?.Invoke(PredictedCluster);
            yield break;
        }

        PredictionResponsePayload response = JsonUtility.FromJson<PredictionResponsePayload>(request.downloadHandler.text);
        if (response == null)
        {
            Debug.LogWarning("[vc_SessionTelemetry] Prediction response could not be parsed.");
            onResult?.Invoke(PredictedCluster);
            yield break;
        }

        PredictedCluster = response.predicted_cluster;
        PredictedCareerCluster = response.career_cluster;
        PredictedCareerResult = response.career_result ?? string.Empty;
        PredictedClusterLabel = response.cluster_label ?? string.Empty;
        PredictedCareerFamily = response.career_family ?? string.Empty;
        PredictedClusterHollandCode = response.cluster_holland_code ?? string.Empty;
        PredictedSource = response.source ?? string.Empty;
        PredictedModelVersion = response.model_version ?? string.Empty;
        GameSessionData.ApplyClusterPredictionTelemetry(response);
        Debug.Log(
            $"[Predict] Cluster received: {PredictedCluster} | Career cluster: {PredictedCareerCluster} | Career result: {PredictedCareerResult}"
        );
        string clusterPersistError = null;
        yield return SubmitPredictedCluster(
            baseUrl,
            PredictedCluster,
            PredictedCareerCluster,
            PredictedCareerResult,
            error => clusterPersistError = error);
        if (!string.IsNullOrWhiteSpace(clusterPersistError))
        {
            Debug.LogWarning($"[vc_SessionTelemetry] Cluster persistence failed: {clusterPersistError}");
        }
        onResult?.Invoke(PredictedCluster);
    }

    public IEnumerator SubmitRunSummary(Action<RunSummaryTelemetryOut> onSuccess = null, Action<string> onError = null)
    {
        EnsureSessionStarted();
        TelemetryManager telemetryManager = TelemetryManager.Instance;
        if (telemetryManager == null)
        {
            onError?.Invoke("TelemetryManager is unavailable.");
            yield break;
        }

        RefreshPlayerIdentity();
        SessionSummary localSummary = GetSessionSummary();
        GameSessionData.ApplySessionMetrics(
            localSummary.sessionId,
            localSummary.sessionStartUtc,
            localSummary.totalTimeSpentSeconds,
            localSummary.totalQuestCount,
            localSummary.completedQuestCount,
            localSummary.totalStarsEarned
        );

        bool requestCompleted = false;
        string requestError = null;
        RunSummaryTelemetryOut response = null;

        telemetryManager.SubmitRunComplete(
            BuildRunSummaryPayload(),
            success =>
            {
                response = success;
                requestCompleted = true;
            },
            error =>
            {
                requestError = error;
                requestCompleted = true;
            });

        while (!requestCompleted)
        {
            yield return null;
        }

        if (response != null && response.success)
        {
            GameSessionData.ApplyRunSummaryTelemetry(response);
            onSuccess?.Invoke(response);
            yield break;
        }

        onError?.Invoke(string.IsNullOrWhiteSpace(requestError)
            ? "Run summary response was empty or unsuccessful."
            : requestError);
    }

    private IEnumerator SubmitPredictedCluster(
        string baseUrl,
        int predictedCluster,
        int careerCluster,
        string careerResult,
        Action<string> onError = null)
    {
        RefreshPlayerIdentity();
        SessionSummary summary = GetSessionSummary();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            onError?.Invoke("No backend base URL is configured.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(summary.playerId) || string.IsNullOrWhiteSpace(summary.sessionId))
        {
            onError?.Invoke("Session cluster telemetry is missing player or session identifiers.");
            yield break;
        }

        SessionClusterTelemetryPayload payload = new SessionClusterTelemetryPayload
        {
            player_id = summary.playerId,
            session_id = summary.sessionId,
            predicted_cluster = predictedCluster,
            career_cluster = careerCluster,
            career_result = careerResult ?? string.Empty
        };

        using UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/telemetry/session-cluster", UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 15;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(BuildRequestError(request));
            yield break;
        }

        SessionClusterTelemetryOut response = JsonUtility.FromJson<SessionClusterTelemetryOut>(request.downloadHandler.text);
        if (response == null || !response.success)
        {
            onError?.Invoke("Cluster telemetry response was empty or unsuccessful.");
            yield break;
        }

        GameSessionData.ApplyClusterPredictionTelemetry(response);
    }

    private void EnsureSessionStarted()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            StartSession();
        }
    }

    private void RefreshPlayerIdentity()
    {
        SessionState sessionState = SessionState.Instance;
        PlayerId = sessionState != null ? sessionState.PlayerId : string.Empty;
        Username = sessionState != null ? sessionState.Username : string.Empty;
    }

    private RunSummaryTelemetryPayload BuildRunSummaryPayload()
    {
        SessionSummary summary = GetSessionSummary();
        RunSummaryTelemetryPayload payload = new RunSummaryTelemetryPayload
        {
            player_id = summary.playerId,
            username = summary.username,
            session_id = summary.sessionId,
            scene_version = "three_level_v1",
            total_time_spent_seconds = summary.totalTimeSpentSeconds,
            rounds = new List<ChallengeRoundTelemetryPayload>()
        };

        for (int i = 0; i < questRecords.Count; i++)
        {
            QuestRecord record = questRecords[i];
            if (record == null || string.IsNullOrWhiteSpace(record.questId))
            {
                continue;
            }

            payload.rounds.Add(new ChallengeRoundTelemetryPayload
            {
                challenge_id = record.questId,
                primary_riasec = NormalizeRiasecLetter(record.primaryRiasec) ?? "R",
                solved = record.completed,
                stars_earned = Mathf.Clamp(record.starsEarned, 0, 3),
                retry_count = 0,
                time_spent_seconds = Mathf.Max(0f, record.timeSpentSeconds),
                skill_use_r = GetSkillUseCount(record.skillUsageCounts, "R"),
                skill_use_i = GetSkillUseCount(record.skillUsageCounts, "I"),
                skill_use_a = GetSkillUseCount(record.skillUsageCounts, "A"),
                skill_use_s = GetSkillUseCount(record.skillUsageCounts, "S"),
                skill_use_e = GetSkillUseCount(record.skillUsageCounts, "E"),
                skill_use_c = GetSkillUseCount(record.skillUsageCounts, "C")
            });
        }

        return payload;
    }

    private static string ResolvePredictionBaseUrl()
    {
        string configuredBaseUrl = TelemetryManager.Instance != null
            ? TelemetryManager.Instance.BaseUrl
            : string.Empty;

        return string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? string.Empty
            : configuredBaseUrl.TrimEnd('/');
    }

    private static Dictionary<string, int> NormalizeSkillUsageCounts(IDictionary<string, int> source)
    {
        Dictionary<string, int> normalized = CreateEmptySkillUsageCounts();
        if (source == null)
        {
            return normalized;
        }

        foreach (KeyValuePair<string, int> entry in source)
        {
            string letter = NormalizeRiasecLetter(entry.Key);
            if (string.IsNullOrEmpty(letter))
            {
                continue;
            }

            normalized[letter] += Mathf.Max(0, entry.Value);
        }

        return normalized;
    }

    private static int GetSkillUseCount(IDictionary<string, int> skillUsageCounts, string letter)
    {
        if (skillUsageCounts == null)
        {
            return 0;
        }

        string normalizedLetter = NormalizeRiasecLetter(letter);
        if (string.IsNullOrEmpty(normalizedLetter))
        {
            return 0;
        }

        return skillUsageCounts.TryGetValue(normalizedLetter, out int value)
            ? Mathf.Max(0, value)
            : 0;
    }

    private static void MergeSkillUsageCounts(Dictionary<string, int> target, IDictionary<string, int> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> entry in source)
        {
            string letter = NormalizeRiasecLetter(entry.Key);
            if (string.IsNullOrEmpty(letter))
            {
                continue;
            }

            target[letter] += Mathf.Max(0, entry.Value);
        }
    }

    private static Dictionary<string, int> CreateEmptySkillUsageCounts()
    {
        return new Dictionary<string, int>
        {
            { "R", 0 },
            { "I", 0 },
            { "A", 0 },
            { "S", 0 },
            { "E", 0 },
            { "C", 0 }
        };
    }

    private static string NormalizeRiasecLetter(string rawLetter)
    {
        if (string.IsNullOrWhiteSpace(rawLetter))
        {
            return null;
        }

        string normalized = rawLetter.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "R":
            case "I":
            case "A":
            case "S":
            case "E":
            case "C":
                return normalized;
            default:
                return null;
        }
    }

    private static string BuildRequestError(UnityWebRequest request)
    {
        string responseText = request.downloadHandler?.text ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return $"{request.responseCode}: {responseText}";
        }

        if (!string.IsNullOrWhiteSpace(request.error))
        {
            return request.error;
        }

        return $"Request failed with result {request.result}.";
    }
}
