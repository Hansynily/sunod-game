using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SunodGame.Core;

namespace SunodGame.UI
{
    public class EndSceneButtonBinder : MonoBehaviour
    {
        private const string EndSceneName = "EndScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != EndSceneName) return;
            if (FindAnyObjectByType<EndSceneButtonBinder>() != null) return;

            var go = new GameObject("[EndScene Button Binder]");
            go.AddComponent<EndSceneButtonBinder>();
        }

        private void Start()
        {
            BindButton("BTN_PlayAgain", OnPlayAgainPressed);
            BindButton("BTN_MainMenu", OnMainMenuPressed);
        }

        private static void BindButton(string objectName, UnityEngine.Events.UnityAction action)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                Debug.LogWarning($"[EndSceneButtonBinder] Could not find '{objectName}'.");
                return;
            }

            Button button = target.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning($"[EndSceneButtonBinder] '{objectName}' has no Button component.");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void OnPlayAgainPressed()
        {
            SceneLoader.GoToPlay();
        }

        private static void OnMainMenuPressed()
        {
            SceneLoader.GoToMainMenu();
        }
    }
}
