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
        public string questionCode;   // RIASEC item this quest represents: "R1".."C8" (Option B)
        public bool completed;
        public int starsEarned;
        public int itemScore;         // 1-5 Option B score (see ComputeItemScore)
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

    [Header("Option B prediction")]
    [Tooltip("OFF (default): players see a predicted career even mid-progress - unfilled slots " +
             "neutral-fill to 3 and prediction still runs. EndScene/UI must label this provisional " +
             "until all 48 slots are real. ON: prediction only runs once all 48 slots are filled " +
             "(cleanest thesis data, but no in-progress career).")]
    [SerializeField] private bool requireCompleteRun = false;

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

    public void RecordQuestResult(string questId, string questName, string primaryRiasec, string questionCode, bool completed, int starsEarned, float finalTimeRemainingSeconds, float timeSpentSeconds, IDictionary<string, int> skillUsageCounts)
    {
        EnsureSessionStarted();
        RefreshPlayerIdentity();

        int normalizedStars = Mathf.Clamp(starsEarned, 0, 3);

        questRecords.Add(new QuestRecord
        {
            questId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim(),
            questName = string.IsNullOrWhiteSpace(questName) ? string.Empty : questName.Trim(),
            primaryRiasec = NormalizeRiasecLetter(primaryRiasec) ?? string.Empty,
            questionCode = string.IsNullOrWhiteSpace(questionCode) ? string.Empty : questionCode.Trim().ToUpperInvariant(),
            completed = completed,
            starsEarned = normalizedStars,
            itemScore = ComputeItemScore(completed, normalizedStars),
            finalTimeRemainingSeconds = Mathf.Max(0f, finalTimeRemainingSeconds),
            timeSpentSeconds = Mathf.Max(0f, timeSpentSeconds),
            skillUsageCounts = NormalizeSkillUsageCounts(skillUsageCounts)
        });

        Debug.Log($"[Telemetry] Quest recorded: {questId} | Code: {questionCode} | Stars: {starsEarned} | Completed: {completed}");
    }

    // Option B per-item score. This is the SINGLE place the 1-5 rule lives - change it here only.
    // Rule (pending adviser sign-off): not completed -> 1; completed 0*->2, 1*->3, 2*->4, 3*->5.
    public static int ComputeItemScore(bool completed, int starsNormalized)
    {
        if (!completed) return 1;
        return 2 + Mathf.Clamp(starsNormalized, 0, 3);
    }

    public IReadOnlyList<QuestRecord> GetAllRecords()
    {
        return questRecords.AsReadOnly();
    }

    /// <summary>
    /// The current records as run-state checkpoint DTOs, deduplicated to the latest
    /// record per questId (same rule as BuildRunSummaryPayload and the 48-item adapter).
    /// Pushed to the server after each quest so a resumed run can rebuild them.
    /// </summary>
    public List<RunStateQuestRecordDto> BuildRunStateRecords()
    {
        Dictionary<string, int> latestIndexByQuestId = new Dictionary<string, int>();
        List<string> orderedQuestIds = new List<string>();
        for (int i = 0; i < questRecords.Count; i++)
        {
            QuestRecord record = questRecords[i];
            if (record == null || string.IsNullOrWhiteSpace(record.questId))
            {
                continue;
            }

            if (!latestIndexByQuestId.ContainsKey(record.questId))
            {
                orderedQuestIds.Add(record.questId);
            }
            latestIndexByQuestId[record.questId] = i;
        }

        List<RunStateQuestRecordDto> result = new List<RunStateQuestRecordDto>(orderedQuestIds.Count);
        foreach (string questId in orderedQuestIds)
        {
            QuestRecord record = questRecords[latestIndexByQuestId[questId]];
            result.Add(new RunStateQuestRecordDto
            {
                quest_id = record.questId,
                quest_name = record.questName ?? string.Empty,
                primary_riasec = record.primaryRiasec ?? string.Empty,
                question_code = record.questionCode ?? string.Empty,
                completed = record.completed,
                stars = Mathf.Clamp(record.starsEarned, 0, 3),
                time_spent_seconds = Mathf.Max(0f, record.timeSpentSeconds),
                skill_use_r = GetSkillUseCount(record.skillUsageCounts, "R"),
                skill_use_i = GetSkillUseCount(record.skillUsageCounts, "I"),
                skill_use_a = GetSkillUseCount(record.skillUsageCounts, "A"),
                skill_use_s = GetSkillUseCount(record.skillUsageCounts, "S"),
                skill_use_e = GetSkillUseCount(record.skillUsageCounts, "E"),
                skill_use_c = GetSkillUseCount(record.skillUsageCounts, "C")
            });
        }

        return result;
    }

    /// <summary>
    /// Continue: rebuilds prior sessions' quest records from the server checkpoint so the
    /// run summary, the 48-item prediction vector, and the progress counts cover the WHOLE
    /// playthrough, not just the quests played since the app launched. Restored records
    /// never overwrite a record already present for the same questId.
    /// </summary>
    public void SeedRestoredRecords(IEnumerable<RunStateQuestRecordDto> restored)
    {
        if (restored == null)
        {
            return;
        }

        EnsureSessionStarted();

        HashSet<string> existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (QuestRecord record in questRecords)
        {
            if (record != null && !string.IsNullOrWhiteSpace(record.questId))
            {
                existingIds.Add(record.questId);
            }
        }

        int seeded = 0;
        foreach (RunStateQuestRecordDto dto in restored)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.quest_id) || existingIds.Contains(dto.quest_id))
            {
                continue;
            }

            int stars = Mathf.Clamp(dto.stars, 0, 3);
            Dictionary<string, int> usage = CreateEmptySkillUsageCounts();
            usage["R"] = Mathf.Max(0, dto.skill_use_r);
            usage["I"] = Mathf.Max(0, dto.skill_use_i);
            usage["A"] = Mathf.Max(0, dto.skill_use_a);
            usage["S"] = Mathf.Max(0, dto.skill_use_s);
            usage["E"] = Mathf.Max(0, dto.skill_use_e);
            usage["C"] = Mathf.Max(0, dto.skill_use_c);

            questRecords.Add(new QuestRecord
            {
                questId = dto.quest_id.Trim(),
                questName = dto.quest_name ?? string.Empty,
                primaryRiasec = NormalizeRiasecLetter(dto.primary_riasec) ?? string.Empty,
                questionCode = string.IsNullOrWhiteSpace(dto.question_code) ? string.Empty : dto.question_code.Trim().ToUpperInvariant(),
                completed = dto.completed,
                starsEarned = stars,
                itemScore = ComputeItemScore(dto.completed, stars),
                finalTimeRemainingSeconds = 0f,
                timeSpentSeconds = Mathf.Max(0f, dto.time_spent_seconds),
                skillUsageCounts = usage
            });
            existingIds.Add(dto.quest_id);
            seeded++;
        }

        Debug.Log($"[Telemetry] Restored {seeded} quest record(s) from the server checkpoint (session now holds {questRecords.Count}).");
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
        SessionSummary summary = GetSessionSummary();
        PredictedCluster = -1;
        PredictedCareerCluster = -1;
        PredictedCareerResult = string.Empty;
        PredictedClusterLabel = string.Empty;
        PredictedCareerFamily = string.Empty;
        PredictedClusterHollandCode = string.Empty;
        PredictedSource = string.Empty;
        PredictedModelVersion = string.Empty;
        vc_RiasecAdapter.BuildResult buildResult = vc_RiasecAdapter.BuildModelInput(GetAllRecords(), requireCompleteRun);
        float[] features = buildResult.Features;

        if (buildResult.IgnoredRecords.Count > 0)
        {
            Debug.LogWarning($"[vc_SessionTelemetry] {buildResult.IgnoredRecords.Count} quest record(s) had no/unknown question code and were excluded from the model vector: {string.Join(", ", buildResult.IgnoredRecords)}");
        }

        if (requireCompleteRun && !buildResult.IsComplete)
        {
            Debug.LogWarning($"[vc_SessionTelemetry] Skipping /api/predict - incomplete run (requireCompleteRun is ON). Missing question codes: {string.Join(", ", buildResult.MissingCodes)}. EndScene keeps the rubric/fallback result.");
            onResult?.Invoke(PredictedCluster);
            yield break;
        }

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
            player_id = summary.playerId,
            session_id = summary.sessionId,
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

        // The server rejects the whole run summary if two rounds share a challenge_id.
        // A quest re-recorded in one run (retry) would produce exactly that, so collapse
        // to the LATEST record per questId - the same "latest wins" rule the 48-item
        // adapter uses. First-seen order is preserved for readability.
        Dictionary<string, int> latestIndexByQuestId = new Dictionary<string, int>();
        List<string> orderedQuestIds = new List<string>();
        for (int i = 0; i < questRecords.Count; i++)
        {
            QuestRecord record = questRecords[i];
            if (record == null || string.IsNullOrWhiteSpace(record.questId))
            {
                continue;
            }

            if (!latestIndexByQuestId.ContainsKey(record.questId))
            {
                orderedQuestIds.Add(record.questId);
            }
            latestIndexByQuestId[record.questId] = i;
        }

        foreach (string questId in orderedQuestIds)
        {
            QuestRecord record = questRecords[latestIndexByQuestId[questId]];

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
