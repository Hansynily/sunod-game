using UnityEngine;
using UnityEngine.UI;
using SunodGame.Core;

namespace SunodGame.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button btnPlay;

        [Header("Navigation")]
        [SerializeField] private string playSceneName = SceneLoader.SCENE_CUTSCENE;

        private void Start()
        {
            if (btnPlay == null)
            {
                Debug.LogError("[MainMenuUI] Play button is not assigned.");
                return;
            }

            btnPlay.onClick.AddListener(OnPlayClicked);
        }

        private void OnDestroy()
        {
            if (btnPlay != null)
                btnPlay.onClick.RemoveListener(OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            SceneLoader.LoadByName(playSceneName);
        }
    }
}
