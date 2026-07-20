using UnityEngine;
using UnityEngine.SceneManagement;
using SunodGame.Core;

public class vc_AppStartup : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";

    void Awake()
    {
        if (SessionState.Instance == null || !SessionState.Instance.TryRestoreSession())
            return; // no persisted session - stay on the login scene

        if (AuthManager.Instance == null)
        {
            // Can't validate without the network layer - don't trust an unverified session.
            SessionState.Instance.ClearUser();
            return;
        }

        AuthManager.Instance.ValidateSession(
            SessionState.Instance.AuthUserId,
            SessionState.Instance.AccessToken,
            isValid =>
            {
                if (isValid)
                {
                    SceneManager.LoadScene(mainMenuScene);
                }
                else
                {
                    // Stored token is dead (revoked, expired secret rotation, etc.) or the
                    // server is unreachable - this game is online-only, so don't enter a
                    // session we can't confirm. Stay on Login; the player logs in fresh.
                    Debug.LogWarning("[AppStartup] Persisted session could not be validated - clearing.");
                    SessionState.Instance.ClearUser();
                }
            });
    }
}
