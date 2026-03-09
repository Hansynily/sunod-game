using UnityEngine;

namespace SunodGame.Core
{
    /// This is persistent on all scenes!!
    /// Stores the player identity and current run state.
    public class SessionState : MonoBehaviour
    {
        public static SessionState Instance { get; private set; }

        public string Username { get; private set; }
        public string AuthPlayerId { get; private set; }

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

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetUsername(string username)
        {
            SetAuthenticatedUser(username, null);
        }

        public void SetAuthenticatedUser(string username, string playerId)
        {
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            AuthPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
            Debug.Log($"[Session] Username set -> {Username}  PlayerID -> {PlayerId}");
        }

        public void ClearUser()
        {
            Username = null;
            AuthPlayerId = null;
        }

        public void BeginRun(string questId)
        {
            CurrentQuestId = questId;
            RunStartTime = Time.realtimeSinceStartup;
            Debug.Log($"[Session] Run started -> {questId}");
        }

        public int GetElapsedSeconds()
        {
            return Mathf.RoundToInt(Time.realtimeSinceStartup - RunStartTime);
        }
    }
}
