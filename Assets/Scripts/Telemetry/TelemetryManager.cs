using System;
using System.Collections;
using System.Text;
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

        public static TelemetryManager Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string railwayBaseUrl = "";
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

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
