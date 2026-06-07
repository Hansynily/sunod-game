using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SunodGame.Core
{
    public static class SceneLoader
    {
        public const string SCENE_LOGIN    = "LoginRegisterScene";
        public const string SCENE_MAINMENU = "MainMenu";
        public const string SCENE_CUTSCENE = "Cutscene";
        public const string SCENE_GAME     = "Game_Scene";
        public const string SCENE_PLAY     = "Level1_Scene";
        public const string SCENE_TUTORIAL = "Level0_Tutorial";
        public const string SCENE_END      = "EndScene";

        public static void GoToLogin()    => SceneManager.LoadScene(SCENE_LOGIN);
        public static void GoToMainMenu() => SceneManager.LoadScene(SCENE_MAINMENU);
        public static void GoToCutscene() => SceneManager.LoadScene(SCENE_CUTSCENE);
        public static void GoToTutorial() => SceneManager.LoadScene(SCENE_TUTORIAL);
        public static void GoToEnd()      => SceneManager.LoadScene(SCENE_END);

        public static void GoToPlay()
        {
            ResetForNewRun();
            SceneManager.LoadScene(SCENE_GAME);
        }

        public static void LoadByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoader] Scene name is empty.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private static void ResetForNewRun()
        {
            vc_PlayerInventory.Instance?.ClearInventory();
            vc_SessionTelemetry.Instance?.CancelCurrentSession();
            vc_SkillManager.Instance?.ResetForNewRun();
            vc_SkillZone.ResetCounter();
            GameSessionData.Reset();

            // Re-enable any UI components that were hidden during EndScene
            // (handles DontDestroyOnLoad UIDocuments/Canvases disabled by EndScene)
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!doc.enabled) doc.enabled = true;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!canvas.enabled) canvas.enabled = true;
        }
    }
}
