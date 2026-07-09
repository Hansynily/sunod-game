using UnityEngine;
using UnityEngine.SceneManagement;
using SunodGame.Core;

public class vc_AppStartup : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";

    void Awake()
    {
        if (SessionState.Instance != null && SessionState.Instance.TryRestoreSession())
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
