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
        [SerializeField] private int requestTimeoutSeconds = 15;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Login(string username,
                          string password,
                          Action<AuthResponse> onResolved,
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
                onResolved,
                onError
            ));
        }

        public void Register(string name,
                             string birthdate,
                             string gender,
                             string username,
                             string password,
                             string email,
                             Action<AuthResponse> onResolved,
                             Action<string> onError)
        {
            if (!ValidateUsername(username, onError) ||
                !ValidatePassword(password, onError) ||
                !ValidateEmail(email, onError))
                return;

            if (password.Length < 6)
            {
                onError?.Invoke("Password must be at least 6 characters.");
                return;
            }

            var payload = new UserCreateRequest
            {
                name = name.Trim(),
                birthdate = birthdate.Trim(),
                gender = gender.Trim(),
                username = username.Trim(),
                password = password,
                email = email.Trim()
            };

            StartCoroutine(PostAuth(
                "/api/telemetry/users",
                JsonUtility.ToJson(payload),
                onResolved,
                onError
            ));
        }

        public void MarkTutorialCompleted(Action<TutorialCompletionResponse> onResolved,
                                          Action<string> onError)
        {
            if (SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
            {
                onError?.Invoke("No authenticated session is available.");
                return;
            }

            StartCoroutine(PostAuthorizedJson(
                "/api/telemetry/users/me/tutorial-complete",
                "{}",
                SessionState.Instance.AccessToken,
                onResolved,
                onError
            ));
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

        private bool ValidateEmail(string email, Action<string> onError)
        {
            string normalized = email?.Trim();
            if (!string.IsNullOrWhiteSpace(normalized) && normalized.Contains("@"))
                return true;

            onError?.Invoke("Enter a valid email address.");
            return false;
        }

        private IEnumerator PostAuth(string path,
                                     string json,
                                     Action<AuthResponse> onResolved,
                                     Action<string> onError)
        {
            string requestUrl = GetBaseUrl() + path;
            Debug.Log($"[Auth] POST {requestUrl}");

            using var req = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Auth] Request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(ExtractError(req.responseCode, req.downloadHandler.text));
                yield break;
            }

            string responseText = req.downloadHandler.text;
            Debug.Log($"[Auth] Response: {responseText}");

            if (string.IsNullOrWhiteSpace(responseText))
            {
                Debug.LogWarning("[Auth] Backend auth response was empty.");
                onError?.Invoke("Backend auth response was empty.");
                yield break;
            }

            AuthResponse auth = JsonUtility.FromJson<AuthResponse>(responseText);
            if (auth == null)
            {
                Debug.LogWarning($"[Auth] Unexpected auth response: {responseText}");
                onError?.Invoke("Backend auth response could not be read.");
                yield break;
            }

            onResolved?.Invoke(auth);
        }

        private IEnumerator PostAuthorizedJson(string path,
                                               string json,
                                               string accessToken,
                                               Action<TutorialCompletionResponse> onResolved,
                                               Action<string> onError)
        {
            string requestUrl = GetBaseUrl() + path;
            Debug.Log($"[Auth] POST {requestUrl}");

            using var req = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Auth] Authorized request failed: {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onError?.Invoke(ExtractError(req.responseCode, req.downloadHandler.text));
                yield break;
            }

            string responseText = req.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                onError?.Invoke("Backend response was empty.");
                yield break;
            }

            TutorialCompletionResponse result = JsonUtility.FromJson<TutorialCompletionResponse>(responseText);
            if (result == null)
            {
                onError?.Invoke("Backend response could not be read.");
                yield break;
            }

            onResolved?.Invoke(result);
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
            if (responseCode == 404 && !string.IsNullOrWhiteSpace(responseBody) && responseBody.Contains("Not Found"))
                return "Auth endpoint not found.";

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    ErrorResponse apiError = JsonUtility.FromJson<ErrorResponse>(responseBody);
                    if (apiError != null && !string.IsNullOrWhiteSpace(apiError.detail))
                        return apiError.detail;
                }
                catch (ArgumentException)
                {
                    string validationMessage = ExtractValidationMessage(responseBody);
                    if (!string.IsNullOrWhiteSpace(validationMessage))
                        return validationMessage;
                }

                string detailMessage = ExtractQuotedJsonValue(responseBody, "\"detail\":\"");
                if (!string.IsNullOrWhiteSpace(detailMessage))
                    return detailMessage;

                return $"{responseCode}: {responseBody}";
            }

            return $"{responseCode}: Request failed.";
        }

        private string ExtractValidationMessage(string responseBody)
        {
            string validationMessage = ExtractQuotedJsonValue(responseBody, "\"msg\":\"");
            if (!string.IsNullOrWhiteSpace(validationMessage))
                return validationMessage;

            if (responseBody.Contains("\"detail\":["))
                return "Some registration fields are invalid or missing.";

            return null;
        }

        private string ExtractQuotedJsonValue(string source, string marker)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(marker))
                return null;

            int markerIndex = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return null;

            int valueStart = markerIndex + marker.Length;
            if (valueStart >= source.Length)
                return null;

            int valueEnd = source.IndexOf('"', valueStart);
            if (valueEnd < 0 || valueEnd <= valueStart)
                return null;

            string rawValue = source.Substring(valueStart, valueEnd - valueStart);
            return rawValue
                .Replace("\\\"", "\"")
                .Replace("\\n", " ")
                .Replace("\\/", "/")
                .Trim();
        }
    }
}
