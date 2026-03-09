using UnityEngine;
using UnityEngine.UI;
using SunodGame.Core;

namespace SunodGame.UI
{
    public class CutsceneUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button btnSkip;

        private void Start()
        {
            if (btnSkip == null)
            {
                return;
            }

            btnSkip.onClick.AddListener(OnSkipClicked);
        }

        private void OnDestroy()
        {
            if (btnSkip != null)
                btnSkip.onClick.RemoveListener(OnSkipClicked);
        }

        private void OnSkipClicked()
        {
            SceneLoader.GoToPlay();
        }
    }
}
