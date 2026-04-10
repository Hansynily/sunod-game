using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    public class EndSceneUI : MonoBehaviour
    {
        private struct ClusterInfo
        {
            public string HollandCode;
            public string Label;
            public string[] ExampleCareers;

            public ClusterInfo(string hollandCode, string label, params string[] exampleCareers)
            {
                HollandCode = hollandCode;
                Label = label;
                ExampleCareers = exampleCareers;
            }
        }

        private static readonly ClusterInfo[] ClusterMap =
        {
            new ClusterInfo("RI", "Engineering", "Civil Engineer", "Programmer", "Architect"),
            new ClusterInfo("IA", "Arts & Design", "Fashion Designer", "Graphic Artist", "Writer"),
            new ClusterInfo("SEC", "Business & Finance", "Accountant", "Financial Analyst", "Entrepreneur"),
            new ClusterInfo("AS", "Performing Arts", "Musician", "Athlete", "Entertainer"),
            new ClusterInfo("-", "Varied Interests"),
            new ClusterInfo("IS", "Research", "Computer Scientist", "Zoologist", "Epidemiologist"),
            new ClusterInfo("SC", "Social Services", "Lawyer", "Teacher", "Counselor"),
            new ClusterInfo("-", "Varied Interests"),
            new ClusterInfo("IAS", "Healthcare", "Doctor", "Nurse", "Pharmacist")
        };

        [Header("Labels")]
        [SerializeField] private TMP_Text txt_EndTitle;
        [SerializeField] private TMP_Text txt_Summary;
        [SerializeField] private TMP_Text txt_RiasecNote;

        [Header("Buttons")]
        [SerializeField] private Button btn_PlayAgain;
        [SerializeField] private Button btn_MainMenu;
        private bool _listenersBound;

        void Start()
        {
            ResolveButtonReferences();
            BindButtonListeners();

            PopulateSummary();
        }

        void OnDestroy()
        {
            if (btn_PlayAgain != null)
                btn_PlayAgain.onClick.RemoveListener(OnPlayAgain);

            if (btn_MainMenu != null)
                btn_MainMenu.onClick.RemoveListener(OnMainMenu);

            _listenersBound = false;
        }

        private void ResolveButtonReferences()
        {
            if (btn_PlayAgain == null)
            {
                GameObject playAgainObj = GameObject.Find("BTN_PlayAgain");
                if (playAgainObj != null) btn_PlayAgain = playAgainObj.GetComponent<Button>();
            }

            if (btn_MainMenu == null)
            {
                GameObject mainMenuObj = GameObject.Find("BTN_BackToMenu");
                if (mainMenuObj == null) mainMenuObj = GameObject.Find("BTN_MainMenu");
                if (mainMenuObj != null) btn_MainMenu = mainMenuObj.GetComponent<Button>();
            }
        }

        private void BindButtonListeners()
        {
            if (_listenersBound) return;

            if (btn_PlayAgain != null)
            {
                btn_PlayAgain.onClick.RemoveListener(OnPlayAgain);
                btn_PlayAgain.onClick.AddListener(OnPlayAgain);
            }
            else
            {
                Debug.LogWarning("[EndSceneUI] PlayAgain button reference is missing.");
            }

            if (btn_MainMenu != null)
            {
                btn_MainMenu.onClick.RemoveListener(OnMainMenu);
                btn_MainMenu.onClick.AddListener(OnMainMenu);
            }
            else
            {
                Debug.LogWarning("[EndSceneUI] MainMenu button reference is missing.");
            }

            _listenersBound = true;
        }

        void PopulateSummary()
        {
            if (txt_EndTitle != null)
                txt_EndTitle.text = "Your Adventure Result";

            if (txt_Summary != null)
            {
                vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
                int predictedCluster = telemetry != null ? telemetry.PredictedCluster : -1;
                if (predictedCluster < 0 || predictedCluster >= ClusterMap.Length)
                {
                    txt_Summary.text = "Result unavailable";
                }
                else
                {
                    ClusterInfo clusterInfo = ClusterMap[predictedCluster];
                    txt_Summary.text =
                        $"Cluster: {predictedCluster}\n" +
                        $"Holland Code: {clusterInfo.HollandCode}\n" +
                        $"Career Family: {clusterInfo.Label}";
                }
            }

            if (txt_RiasecNote != null)
            {
                vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
                int predictedCluster = telemetry != null ? telemetry.PredictedCluster : -1;
                if (predictedCluster < 0 || predictedCluster >= ClusterMap.Length)
                {
                    txt_RiasecNote.text = string.Empty;
                }
                else
                {
                    ClusterInfo clusterInfo = ClusterMap[predictedCluster];
                    txt_RiasecNote.text = clusterInfo.ExampleCareers != null && clusterInfo.ExampleCareers.Length > 0
                        ? $"Example Careers: {string.Join(", ", clusterInfo.ExampleCareers)}"
                        : string.Empty;
                }
            }
        }

        void OnPlayAgain()
        {
            TelemetryManager.Instance?.TagButtonClick("PlayAgain");
            SceneLoader.GoToPlay();
        }

        void OnMainMenu()
        {
            TelemetryManager.Instance?.TagButtonClick("MainMenu");
            SceneLoader.GoToMainMenu();
        }
    }
}
