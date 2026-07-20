using UnityEngine;

namespace SunodGame.Core
{
    /// This is persistent on all scenes!!
    /// Stores the player identity and current run state.
    public class SessionState : MonoBehaviour
    {
        public static SessionState Instance { get; private set; }

        // Persistent login: only the token + minimal identity are stored on disk, never the
        // password. Cleared on logout or when the server rejects the stored token as invalid.
        private const string PrefKeyToken = "sunod.auth.token";
        private const string PrefKeyUsername = "sunod.auth.username";
        private const string PrefKeyPlayerId = "sunod.auth.playerId";
        private const string PrefKeyUserId = "sunod.auth.userId";

        public string Username { get; private set; }
        public string AuthPlayerId { get; private set; }
        public string AccessToken { get; private set; }
        public int AuthUserId { get; private set; }
        public string Name { get; private set; }
        public string Birthdate { get; private set; }
        public string Gender { get; private set; }
        public bool HasCompletedTutorial { get; private set; }

        public string PlayerId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(AuthPlayerId))
                    return AuthPlayerId;

#if UNITY_EDITOR
                return !string.IsNullOrWhiteSpace(Username) ? $"editor_{Username}" : "editor_guest";
#else
                return UnityEngine.SystemInfo.deviceUniqueIdentifier;
#endif
            }
        }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Username);

        public string CurrentQuestId { get; private set; }
        public float RunStartTime { get; private set; }

        public bool HasActiveRun => !string.IsNullOrWhiteSpace(CurrentQuestId);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetAuthenticatedUser(
            string username,
            string playerId,
            string accessToken,
            string name,
            string birthdate,
            string gender,
            bool hasCompletedTutorial,
            int userId = 0)
        {
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            AuthPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
            AccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
            AuthUserId = userId;
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            Birthdate = string.IsNullOrWhiteSpace(birthdate) ? null : birthdate.Trim();
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
            HasCompletedTutorial = hasCompletedTutorial;
            Debug.Log($"[Session] Username set -> {Username}  PlayerID -> {PlayerId}");

            PersistCredentials();
        }

        private void PersistCredentials()
        {
            if (string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(Username))
                return;

            PlayerPrefs.SetString(PrefKeyToken, AccessToken);
            PlayerPrefs.SetString(PrefKeyUsername, Username);
            PlayerPrefs.SetString(PrefKeyPlayerId, AuthPlayerId ?? string.Empty);
            PlayerPrefs.SetInt(PrefKeyUserId, AuthUserId);
            PlayerPrefs.Save();
        }

        private void ClearPersistedCredentials()
        {
            PlayerPrefs.DeleteKey(PrefKeyToken);
            PlayerPrefs.DeleteKey(PrefKeyUsername);
            PlayerPrefs.DeleteKey(PrefKeyPlayerId);
            PlayerPrefs.DeleteKey(PrefKeyUserId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Rehydrates the in-memory session from the locally stored token/identity (persistent
        /// login). Does NOT contact the server - the caller (vc_AppStartup) is responsible for
        /// validating the restored token before trusting the session for gameplay.
        /// </summary>
        public bool TryRestoreSession()
        {
            string token = PlayerPrefs.GetString(PrefKeyToken, string.Empty);
            string username = PlayerPrefs.GetString(PrefKeyUsername, string.Empty);
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(username))
                return false;

            Username = username;
            AuthPlayerId = PlayerPrefs.GetString(PrefKeyPlayerId, string.Empty);
            AccessToken = token;
            AuthUserId = PlayerPrefs.GetInt(PrefKeyUserId, 0);
            Debug.Log($"[Session] Restored persisted session -> {Username}");
            return true;
        }

        public void SetTutorialCompletionState(bool hasCompletedTutorial)
        {
            HasCompletedTutorial = hasCompletedTutorial;
        }

        public void ClearUser()
        {
            Username = null;
            AuthPlayerId = null;
            AccessToken = null;
            AuthUserId = 0;
            Name = null;
            Birthdate = null;
            Gender = null;
            HasCompletedTutorial = false;
            CurrentQuestId = null;
            ClearPersistedCredentials();
            RunStartTime = 0f;
        }

        public void BeginRun(string questId)
        {
            CurrentQuestId = questId;
            RunStartTime = Time.realtimeSinceStartup;
            Debug.Log($"[Session] Run started -> {questId}");
        }

        public void CancelRun()
        {
            CurrentQuestId = null;
            RunStartTime = 0f;
        }

        public int GetElapsedSeconds()
        {
            if (!HasActiveRun)
                return 0;

            return Mathf.RoundToInt(Time.realtimeSinceStartup - RunStartTime);
        }
    }
}
