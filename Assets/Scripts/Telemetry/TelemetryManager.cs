using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SunodGame.Core;
using SunodGame.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace SunodGame.Telemetry
{
    public class TelemetryManager : MonoBehaviour
    {
        private const string BackendUrlPrefKey = "sunod.backend.url";
        private const string BackendModePrefKey = "sunod.backend.mode";
        public const string BackendModeRailway = "Railway";
        public const string BackendModeCustom = "Custom";

        // Set when End Run could not reach the server to mark the run finished. The main
        // menu treats this as "run is finished" locally (Continue stays greyed) and
        // retries the finish call until it lands. Cleared on success, reset, or new run.
        public const string RunFinishPendingPrefKey = "sunod.run_finish_pending";

        public static TelemetryManager Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string railwayBaseUrl = "http://sunodserver.duckdns.org:8000";
        [SerializeField] private string editorLocalBaseUrl = "http://localhost:8000";
        [SerializeField] private string localBaseUrl = "http://192.168.1.107:8000";

        public string BaseUrl => ResolveConfiguredBaseUrl();

        public string CurrentBackendMode
        {
            get
            {
                string savedMode = PlayerPrefs.GetString(BackendModePrefKey, string.Empty);
                if (savedMode == BackendModeCustom)
                    return BackendModeCustom;

                if (savedMode == "Local")
                    return BackendModeCustom;

                return BackendModeRailway;
            }
        }

        private string ActiveBaseUrl => string.IsNullOrWhiteSpace(BaseUrl)
            ? string.Empty
            : BaseUrl.TrimEnd('/');

        public string RailwayPresetUrl => NormalizeUrl(railwayBaseUrl);

        public string DevelopmentFallbackUrl => NormalizeUrl(Application.isEditor ? editorLocalBaseUrl : localBaseUrl);

        [Header("Debug")]
        [SerializeField] private bool bypassApiCalls = false;
        [SerializeField] private string bypassMessage = "Debug bypass enabled. No API request sent.";
        [SerializeField] private int requestTimeoutSeconds = 10;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var root = new GameObject("[TelemetryManager]");
            root.AddComponent<TelemetryManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[Telemetry] Active backend -> {BaseUrl}");
        }

        public bool TryUseRailwayBackend(string overrideUrl, out string resolvedUrl, out string errorMessage)
        {
            resolvedUrl = NormalizeUrl(overrideUrl);
            if (string.IsNullOrWhiteSpace(resolvedUrl))
                resolvedUrl = RailwayPresetUrl;

            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                errorMessage = "Railway URL is not configured yet.";
                return false;
            }

            SaveBackendSelection(resolvedUrl, BackendModeRailway);
            errorMessage = null;
            return true;
        }

        public bool TryUseRailwayBackend(out string resolvedUrl, out string errorMessage)
        {
            return TryUseRailwayBackend(string.Empty, out resolvedUrl, out errorMessage);
        }

        public bool TryUseCustomBackend(string customUrl, out string resolvedUrl, out string errorMessage)
        {
            resolvedUrl = NormalizeUrl(customUrl);
            if (!IsValidBackendUrl(resolvedUrl))
            {
                errorMessage = "Enter a valid http:// or https:// backend URL.";
                return false;
            }

            SaveBackendSelection(resolvedUrl, BackendModeCustom);
            errorMessage = null;
            return true;
        }

        // Legacy per-quest telemetry endpoint. Kept for compatibility outside the active demo flow.
        public void SubmitQuestAttempt(QuestAttemptTelemetryIn payload,
                                        Action<QuestAttemptTelemetryOut> onSuccess = null,
                                        Action<string>                   onError   = null)
        {
            string fallbackPlayerId = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            string fallbackUsername = "DemoPlayer";

            payload.player_id = SessionState.Instance != null
                ? SessionState.Instance.PlayerId
                : fallbackPlayerId;
            payload.username = SessionState.Instance != null && !string.IsNullOrWhiteSpace(SessionState.Instance.Username)
                ? SessionState.Instance.Username
                : fallbackUsername;

            if (IsBypassEnabled())
            {
                var simulated = new QuestAttemptTelemetryOut
                {
                    success = true,
                    message = bypassMessage
                };

                Debug.LogWarning("[Telemetry] API bypass is enabled. Returning simulated success.");
                (onSuccess ?? ((r) => Debug.Log($"[Telemetry] Quest submitted -> {r.message}"))).Invoke(simulated);
                return;
            }

            var skillsJson = new StringBuilder();
            skillsJson.Append("[");
            for (int i = 0; i < payload.selected_skills.Count; i++)
            {
                var s = payload.selected_skills[i];
                skillsJson.Append($"{{\"riasec_code\":\"{s.riasec_code}\",\"skill_name\":\"{s.skill_name}\"}}");
                if (i < payload.selected_skills.Count - 1) skillsJson.Append(",");
            }
            skillsJson.Append("]");

            string json =
                $"{{" +
                $"\"player_id\":\"{payload.player_id}\"," +
                $"\"username\":\"{payload.username}\"," +
                $"\"quest_id\":\"{payload.quest_id}\"," +
                $"\"quest_result\":\"{payload.quest_result}\"," +
                $"\"time_spent_seconds\":{payload.time_spent_seconds}," +
                $"\"selected_skills\":{skillsJson}" +
                $"}}";

            StartCoroutine(PostJson(
                "/api/telemetry/quest-attempt",
                json,
                onSuccess ?? ((r) => Debug.Log($"[Telemetry] Quest submitted -> {r.message}")),
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Quest error: {e}"))
            ));
        }

        public void SubmitRunComplete(RunSummaryTelemetryPayload payload,
                                        Action<RunSummaryTelemetryOut> onSuccess = null,
                                        Action<string>                 onError   = null)
        {
            string fallbackPlayerId = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            string fallbackUsername = "DemoPlayer";

            if (string.IsNullOrWhiteSpace(payload.player_id))
            {
                payload.player_id = SessionState.Instance != null
                    ? SessionState.Instance.PlayerId
                    : fallbackPlayerId;
            }

            if (string.IsNullOrWhiteSpace(payload.username))
            {
                payload.username = SessionState.Instance != null && !string.IsNullOrWhiteSpace(SessionState.Instance.Username)
                    ? SessionState.Instance.Username
                    : fallbackUsername;
            }

            if (string.IsNullOrWhiteSpace(payload.scene_version))
                payload.scene_version = "single_room_v1";

            if (payload.rounds == null)
                payload.rounds = new System.Collections.Generic.List<ChallengeRoundTelemetryPayload>();

            if (IsBypassEnabled())
            {
                string message = $"{bypassMessage} Local rubric fallback will be used.";
                Debug.LogWarning("[Telemetry] API bypass is enabled. Skipping run summary request.");
                (onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run summary bypassed -> {e}"))).Invoke(message);
                return;
            }

            string json = JsonUtility.ToJson(payload);
            StartCoroutine(PostJson(
                "/api/telemetry/run-complete",
                json,
                onSuccess ?? ((r) => Debug.Log($"[Telemetry] Run summary submitted -> {r.message}")),
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run summary error: {e}"))
            ));
        }

        public void GetMyProgress(Action<PlayerProgressResponse> onSuccess,
                                    Action<string> onError = null)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("Not signed in.");
                return;
            }

            StartCoroutine(GetAuthorizedJson(
                "/api/telemetry/users/me/progress",
                SessionState.Instance.AccessToken,
                onSuccess,
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Progress fetch error: {e}"))
            ));
        }

        private IEnumerator GetAuthorizedJson<TRes>(string path, string accessToken,
                                                      Action<TRes>   onSuccess,
                                                      Action<string> onError)
        {
            string requestUrl = ActiveBaseUrl + path;
            Debug.Log($"[Telemetry] GET {requestUrl}");

            using var req = UnityWebRequest.Get(requestUrl);
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                TRes result = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
                onSuccess?.Invoke(result);
            }
            else
            {
                Debug.LogWarning($"[Telemetry] Request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(BuildErrorMessage(req));
            }
        }

        // ── Run-state (Continue / Reset save-state) ────────────────────────
        // riasec_scores travels as a JSON object ({"R":5,...}), which JsonUtility cannot
        // (de)serialize into a Dictionary - so the PUT body is hand-built here and the GET
        // response's riasec_scores is parsed out with a small regex, same as the rest of
        // this file already hand-builds JSON for dictionary-shaped data (see SubmitQuestAttempt).

        public void GetMyRunState(Action<RunStateResponse, Dictionary<string, float>> onSuccess,
                                    Action<string> onError = null)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("Not signed in.");
                return;
            }

            StartCoroutine(GetRunStateCoroutine(
                SessionState.Instance.AccessToken,
                onSuccess,
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run-state fetch error: {e}"))
            ));
        }

        private IEnumerator GetRunStateCoroutine(string accessToken,
                                                   Action<RunStateResponse, Dictionary<string, float>> onSuccess,
                                                   Action<string> onError)
        {
            string requestUrl = ActiveBaseUrl + "/api/telemetry/users/me/run-state";
            using var req = UnityWebRequest.Get(requestUrl);
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildErrorMessage(req));
                yield break;
            }

            string text = req.downloadHandler.text;
            RunStateResponse response = JsonUtility.FromJson<RunStateResponse>(text);
            Dictionary<string, float> riasec = ParseRiasecScoresObject(text);
            onSuccess?.Invoke(response, riasec);
        }

        public void PutMyRunState(RunStatePayload payload, Dictionary<string, float> riasecScores,
                                    Action<RunStateResponse> onSuccess = null,
                                    Action<string>           onError   = null)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("Not signed in.");
                return;
            }

            string json = BuildRunStateJson(payload, riasecScores);
            StartCoroutine(PutJsonAuthorized(
                "/api/telemetry/users/me/run-state",
                json,
                SessionState.Instance.AccessToken,
                onSuccess,
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run-state push error: {e}"))
            ));
        }

        public void ResetMyRunState(Action<RunStateResetResponse> onSuccess = null,
                                      Action<string>                onError   = null)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("Not signed in.");
                return;
            }

            StartCoroutine(PostAuthorizedNoBody(
                "/api/telemetry/users/me/run-state/reset",
                SessionState.Instance.AccessToken,
                onSuccess ?? ((r) => Debug.Log($"[Telemetry] Run reset -> {r.message}")),
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run reset error: {e}"))
            ));
        }

        // End Run. One POST to a dedicated endpoint that atomically sets run_finished
        // server-side. The old approach (GET the state, re-PUT it with the flag) had
        // avoidable failure modes: two round trips, and the PUT could be rejected by
        // payload validation. This cannot.
        public void FinishMyRunState(Action<RunStateResetResponse> onSuccess = null,
                                       Action<string>                onError   = null)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("Not signed in.");
                return;
            }

            StartCoroutine(PostAuthorizedNoBody(
                "/api/telemetry/users/me/run-state/finish",
                SessionState.Instance.AccessToken,
                onSuccess ?? ((r) => Debug.Log($"[Telemetry] Run finished -> {r.message}")),
                onError ?? ((e) => Debug.LogWarning($"[Telemetry] Run finish error: {e}"))
            ));
        }

        private static string BuildRunStateJson(RunStatePayload payload, Dictionary<string, float> riasecScores)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"session_id\":\"{EscapeJson(payload.session_id)}\",");
            sb.Append("\"completed_quest_ids\":[");
            for (int i = 0; i < payload.completed_quest_ids.Count; i++)
            {
                sb.Append($"\"{EscapeJson(payload.completed_quest_ids[i])}\"");
                if (i < payload.completed_quest_ids.Count - 1) sb.Append(",");
            }
            sb.Append("],");

            sb.Append("\"riasec_scores\":{");
            if (riasecScores != null)
            {
                int i = 0;
                foreach (var kvp in riasecScores)
                {
                    sb.Append($"\"{EscapeJson(kvp.Key)}\":{kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    if (i < riasecScores.Count - 1) sb.Append(",");
                    i++;
                }
            }
            sb.Append("},");

            sb.Append("\"quest_records\":[");
            if (payload.quest_records != null)
            {
                bool firstRecord = true;
                for (int i = 0; i < payload.quest_records.Count; i++)
                {
                    RunStateQuestRecordDto r = payload.quest_records[i];
                    if (r == null) continue;
                    if (!firstRecord) sb.Append(",");
                    firstRecord = false;
                    sb.Append("{");
                    sb.Append($"\"quest_id\":\"{EscapeJson(r.quest_id)}\",");
                    sb.Append($"\"quest_name\":\"{EscapeJson(r.quest_name)}\",");
                    sb.Append($"\"primary_riasec\":\"{EscapeJson(r.primary_riasec)}\",");
                    sb.Append($"\"question_code\":\"{EscapeJson(r.question_code)}\",");
                    sb.Append($"\"completed\":{(r.completed ? "true" : "false")},");
                    sb.Append($"\"stars\":{r.stars},");
                    sb.Append($"\"time_spent_seconds\":{r.time_spent_seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                    sb.Append($"\"skill_use_r\":{r.skill_use_r},");
                    sb.Append($"\"skill_use_i\":{r.skill_use_i},");
                    sb.Append($"\"skill_use_a\":{r.skill_use_a},");
                    sb.Append($"\"skill_use_s\":{r.skill_use_s},");
                    sb.Append($"\"skill_use_e\":{r.skill_use_e},");
                    sb.Append($"\"skill_use_c\":{r.skill_use_c}");
                    sb.Append("}");
                }
            }
            sb.Append("],");

            sb.Append("\"owned_skills\":[");
            for (int i = 0; i < payload.owned_skills.Count; i++)
            {
                sb.Append($"\"{EscapeJson(payload.owned_skills[i])}\"");
                if (i < payload.owned_skills.Count - 1) sb.Append(",");
            }
            sb.Append("],");

            sb.Append($"\"total_stars\":{payload.total_stars},");
            sb.Append($"\"floor_scene\":\"{EscapeJson(payload.floor_scene)}\",");
            sb.Append($"\"tutorial_completed\":{(payload.tutorial_completed ? "true" : "false")},");
            sb.Append($"\"run_finished\":{(payload.run_finished ? "true" : "false")}");
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static readonly Regex RiasecScoresBlockPattern =
            new Regex("\"riasec_scores\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Compiled);
        private static readonly Regex RiasecScoresPairPattern =
            new Regex("\"([A-Za-z]+)\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)", RegexOptions.Compiled);

        private static Dictionary<string, float> ParseRiasecScoresObject(string json)
        {
            var result = new Dictionary<string, float>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            Match blockMatch = RiasecScoresBlockPattern.Match(json);
            if (!blockMatch.Success) return result;

            foreach (Match pair in RiasecScoresPairPattern.Matches(blockMatch.Groups[1].Value))
            {
                if (float.TryParse(pair.Groups[2].Value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float value))
                {
                    result[pair.Groups[1].Value] = value;
                }
            }
            return result;
        }

        private IEnumerator PutJsonAuthorized<TRes>(string path, string json, string accessToken,
                                                      Action<TRes>   onSuccess,
                                                      Action<string> onError)
        {
            string requestUrl = ActiveBaseUrl + path;
            Debug.Log($"[Telemetry] PUT {requestUrl}");

            using var req = new UnityWebRequest(requestUrl, "PUT");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                TRes result = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
                onSuccess?.Invoke(result);
            }
            else
            {
                Debug.LogWarning($"[Telemetry] Request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(BuildErrorMessage(req));
            }
        }

        private IEnumerator PostAuthorizedNoBody<TRes>(string path, string accessToken,
                                                         Action<TRes>   onSuccess,
                                                         Action<string> onError)
        {
            string requestUrl = ActiveBaseUrl + path;
            Debug.Log($"[Telemetry] POST {requestUrl}");

            using var req = new UnityWebRequest(requestUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                TRes result = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
                onSuccess?.Invoke(result);
            }
            else
            {
                Debug.LogWarning($"[Telemetry] Request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(BuildErrorMessage(req));
            }
        }

        private bool IsBypassEnabled()
        {
            if (!bypassApiCalls) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        // Convenience wrappers for local logging only.
        public void TagSessionStart() =>
            Debug.Log($"[Telemetry] session_start -> player:{SessionState.Instance?.PlayerId}");

        public void TagSessionEnd() =>
            Debug.Log($"[Telemetry] session_end -> player:{SessionState.Instance?.PlayerId}");

        public void TagButtonClick(string buttonName) =>
            Debug.Log($"[Telemetry] button_click -> {buttonName}");

        private IEnumerator PostJson<TRes>(string path, string json,
                                            Action<TRes>   onSuccess,
                                            Action<string> onError)
        {
            string requestUrl = ActiveBaseUrl + path;
            Debug.Log($"[Telemetry] POST {requestUrl}");

            using var req = new UnityWebRequest(requestUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Telemetry] Response: {req.downloadHandler.text}");
                TRes result = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
                onSuccess?.Invoke(result);
            }
            else
            {
                Debug.LogWarning($"[Telemetry] Request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(BuildErrorMessage(req));
            }
        }

        private static string BuildErrorMessage(UnityWebRequest req)
        {
            string responseText = req.downloadHandler?.text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(responseText))
                return $"{req.responseCode}: {responseText}";

            if (!string.IsNullOrWhiteSpace(req.error))
                return req.error;

            return $"Request failed with result {req.result}.";
        }

        private string ResolveConfiguredBaseUrl()
        {
            string savedUrl = NormalizeUrl(PlayerPrefs.GetString(BackendUrlPrefKey, string.Empty));
            if (!string.IsNullOrWhiteSpace(savedUrl))
                return savedUrl;

            string defaultRailwayUrl = RailwayPresetUrl;
            if (!string.IsNullOrWhiteSpace(defaultRailwayUrl))
                return defaultRailwayUrl;

            return DevelopmentFallbackUrl;
        }

        private void SaveBackendSelection(string url, string mode)
        {
            PlayerPrefs.SetString(BackendUrlPrefKey, NormalizeUrl(url));
            PlayerPrefs.SetString(BackendModePrefKey, mode);
            PlayerPrefs.Save();
        }

        private static string NormalizeUrl(string url)
        {
            return string.IsNullOrWhiteSpace(url)
                ? string.Empty
                : url.Trim().TrimEnd('/');
        }

        private static bool IsValidBackendUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
