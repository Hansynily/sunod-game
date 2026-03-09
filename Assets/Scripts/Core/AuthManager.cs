using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.Core
{
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string baseUrl = "http://localhost:8000";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Login(string username,
                          string password,
                          Action onSuccess,
                          Action<string> onError)
        {
            if (!ValidateUsername(username, onError) || !ValidatePassword(password, onError))
                return;

            var payload = new UserLoginRequest
            {
                username = username.Trim(),
                password = password
            };

            StartCoroutine(PostAuth(
                "/api/telemetry/auth/login",
                JsonUtility.ToJson(payload),
                onSuccess,
                onError
            ));
        }

        public void Register(string username,
                             string password,
                             Action onSuccess,
                             Action<string> onError)
        {
            if (!ValidateUsername(username, onError) || !ValidatePassword(password, onError))
                return;

            if (password.Length < 6)
            {
                onError?.Invoke("Password must be at least 6 characters.");
                return;
            }

            var payload = new UserCreateRequest
            {
                username = username.Trim(),
                password = password
            };

            StartCoroutine(PostAuth(
                "/api/telemetry/users",
                JsonUtility.ToJson(payload),
                onSuccess,
                onError
            ));
        }

        public void Logout()
        {
            TelemetryManager.Instance?.TagSessionEnd();
            SessionState.Instance?.ClearUser();
        }

        private bool ValidateUsername(string username, Action<string> onError)
        {
            if (!string.IsNullOrWhiteSpace(username)) return true;

            onError?.Invoke("Username cannot be empty.");
            return false;
        }

        private bool ValidatePassword(string password, Action<string> onError)
        {
            if (!string.IsNullOrWhiteSpace(password)) return true;

            onError?.Invoke("Password cannot be empty.");
            return false;
        }

        private IEnumerator PostAuth(string path,
                                     string json,
                                     Action onSuccess,
                                     Action<string> onError)
        {
            using var req = new UnityWebRequest(GetBaseUrl() + path, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ExtractError(req.responseCode, req.downloadHandler.text));
                yield break;
            }

            AuthResponse auth = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            if (auth == null ||
                string.IsNullOrWhiteSpace(auth.username) ||
                string.IsNullOrWhiteSpace(auth.player_id))
            {
                Debug.LogWarning($"[Auth] Unexpected auth response: {req.downloadHandler.text}");
                onError?.Invoke("Backend auth response is missing some ids.");
                yield break;
            }

            SessionState.Instance?.SetAuthenticatedUser(auth.username, auth.player_id);
            TelemetryManager.Instance?.TagSessionStart();
            onSuccess?.Invoke();
        }

        private string GetBaseUrl()
        {
            string configuredBaseUrl = TelemetryManager.Instance != null &&
                                       !string.IsNullOrWhiteSpace(TelemetryManager.Instance.BaseUrl)
                ? TelemetryManager.Instance.BaseUrl
                : baseUrl;

            return configuredBaseUrl.TrimEnd('/');
        }

        private string ExtractError(long responseCode, string responseBody)
        {
            if (responseCode == 404 && responseBody.Contains("Not Found"))
                return "Auth endpoint not found.";

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                ErrorResponse apiError = JsonUtility.FromJson<ErrorResponse>(responseBody);
                if (apiError != null && !string.IsNullOrWhiteSpace(apiError.detail))
                    return apiError.detail;

                return $"{responseCode}: {responseBody}";
            }

            return $"{responseCode}: Request failed.";
        }
    }
}
