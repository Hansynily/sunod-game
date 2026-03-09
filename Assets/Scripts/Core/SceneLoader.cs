using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{


    public static class SceneLoader
    {
        //FOLLOW AS INDEX e.g. 
        //INDEX 0 -> LoginRegisterScene
        //INDEX 1 -> MainMenu
        //etc..
        public const string SCENE_LOGIN    = "LoginRegisterScene";
        public const string SCENE_MAINMENU = "MainMenu";
        public const string SCENE_CUTSCENE = "Cutscene";
        public const string SCENE_PLAY     = "DemoPlayScene";
        public const string SCENE_END      = "EndScene";

        public static void GoToLogin()    => SceneManager.LoadScene(SCENE_LOGIN);
        public static void GoToMainMenu() => SceneManager.LoadScene(SCENE_MAINMENU);
        public static void GoToCutscene() => SceneManager.LoadScene(SCENE_CUTSCENE);
        public static void GoToPlay()     => SceneManager.LoadScene(SCENE_PLAY);
        public static void GoToEnd()      => SceneManager.LoadScene(SCENE_END);

        public static void LoadByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoader] Scene name is empty.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        /// Reload
        public static void Reload()
            => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
