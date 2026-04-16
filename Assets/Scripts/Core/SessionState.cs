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
        public string AccessToken { get; private set; }
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
            bool hasCompletedTutorial)
        {
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            AuthPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
            AccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            Birthdate = string.IsNullOrWhiteSpace(birthdate) ? null : birthdate.Trim();
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
            HasCompletedTutorial = hasCompletedTutorial;
            Debug.Log($"[Session] Username set -> {Username}  PlayerID -> {PlayerId}");
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
            Name = null;
            Birthdate = null;
            Gender = null;
            HasCompletedTutorial = false;
            CurrentQuestId = null;
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
