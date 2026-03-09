using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    public class EndSceneUI : MonoBehaviour
    {
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
                txt_EndTitle.text = "Run Complete";

            if (txt_Summary != null)
            {
                SessionState session = SessionState.Instance;
                int elapsed = session != null ? session.GetElapsedSeconds() : 0;
                string username = (session != null && session.IsLoggedIn) ? session.Username : "Demo Player";
                string questId = (session == null || string.IsNullOrWhiteSpace(session.CurrentQuestId))
                    ? "CatQuest"
                    : session.CurrentQuestId;

                txt_Summary.text =
                    $"Player: {username}\n" +
                    $"Quest:  {questId}\n" +
                    $"Time:   {elapsed / 60:D2}:{elapsed % 60:D2}";
            }

            if (txt_RiasecNote == null) return;

            bool hasSkillData = false;
            if (GameSessionData.skillUseCount != null)
            {
                for (int i = 0; i < GameSessionData.skillUseCount.Length; i++)
                {
                    if (GameSessionData.skillUseCount[i] > 0)
                    {
                        hasSkillData = true;
                        break;
                    }
                }
            }

            if (!hasSkillData)
            {
                txt_RiasecNote.text = "No career data recorded for this run.";
                return;
            }

            if (!GameSessionData.usedBackendResult ||
                string.IsNullOrWhiteSpace(GameSessionData.hollandCode) ||
                string.IsNullOrWhiteSpace(GameSessionData.careerResult))
            {
                CareerResultResolver.ResolveAndStore(GameSessionData.skillUseCount, GameSessionData.firstUseOrder);
            }

            string sourceLine = GameSessionData.usedBackendResult
                ? "Source: Backend"
                : "Source: Local Fallback";

            string backendLine = string.IsNullOrWhiteSpace(GameSessionData.backendMessage)
                ? string.Empty
                : $"\nBackend: {GameSessionData.backendMessage}";

            txt_RiasecNote.text =
                $"Career Recommendation: {GameSessionData.careerResult}\n" +
                $"Holland Code: {GameSessionData.hollandCode}\n" +
                $"{sourceLine}{backendLine}";
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
