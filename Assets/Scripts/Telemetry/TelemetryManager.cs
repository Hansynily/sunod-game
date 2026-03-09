using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SunodGame.Models;
using SunodGame.Core;

namespace SunodGame.Telemetry
{
    public class TelemetryManager : MonoBehaviour
    {
        public static TelemetryManager Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string baseUrl = "http://localhost:8000";

        public string BaseUrl => baseUrl;

        [Header("Debug")]
        [SerializeField] private bool bypassApiCalls = false;
        [SerializeField] private string bypassMessage = "Debug bypass enabled. No API request sent.";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        //  PRIMARY  —  POST /api/telemetry/quest-attempt

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
                (onSuccess ?? ((r) => Debug.Log($"[Telemetry] Quest submitted → {r.message}"))).Invoke(simulated);
                return;
            }

            // Build JSON manually
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
                onSuccess ?? ((r) => Debug.Log($"[Telemetry] Quest submitted → {r.message}")),
                onError   ?? ((e) => Debug.LogWarning($"[Telemetry] Quest error: {e}"))
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

        //  CONVENIENCE WRAPPERS  (local logging only)

        public void TagSessionStart() =>
            Debug.Log($"[Telemetry] session_start → player:{SessionState.Instance?.PlayerId}");

        public void TagSessionEnd() =>
            Debug.Log($"[Telemetry] session_end → player:{SessionState.Instance?.PlayerId}");

        public void TagButtonClick(string buttonName) =>
            Debug.Log($"[Telemetry] button_click → {buttonName}");

        public void TagLevelEvent(string levelName, string eventName) =>
            Debug.Log($"[Telemetry] level_event → {levelName} : {eventName}");

        //  HTTP HELPERS

        private IEnumerator PostJson<TRes>(string path, string json,
                                            Action<TRes>   onSuccess,
                                            Action<string> onError)
        {
            using var req = new UnityWebRequest(baseUrl + path, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                TRes result = JsonUtility.FromJson<TRes>(req.downloadHandler.text);
                onSuccess?.Invoke(result);
            }
            else
            {
                onError?.Invoke($"{req.responseCode}: {req.downloadHandler.text}");
            }
        }
    }
}
