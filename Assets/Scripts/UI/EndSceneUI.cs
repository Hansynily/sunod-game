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
                txt_EndTitle.text = "Session Complete";

            if (txt_Summary != null)
            {
                SessionState session = SessionState.Instance;
                string username = (session != null && session.IsLoggedIn) ? session.Username : "Demo Player";
                int roundsCompleted = Mathf.Max(GameSessionData.rounds_attempted, GameSessionData.round_results?.Count ?? 0);
                int roundsCleared = Mathf.Max(0, GameSessionData.rounds_cleared);
                int totalStars = Mathf.Clamp(
                    GameSessionData.total_stars,
                    0,
                    GameSessionData.TotalRoundsPerSession * GameSessionData.MaxStarsPerRound
                );

                txt_Summary.text =
                    $"Player: {username}\n" +
                    $"Rounds Completed: {roundsCompleted} / {GameSessionData.TotalRoundsPerSession}\n" +
                    $"Rounds Cleared: {roundsCleared} / {GameSessionData.TotalRoundsPerSession}\n" +
                    $"Stars: {totalStars} / {GameSessionData.TotalRoundsPerSession * GameSessionData.MaxStarsPerRound}\n" +
                    $"Time: {FormatDuration(GameSessionData.total_time_seconds)}";
            }

            if (txt_RiasecNote == null) return;

            RiasecScoresDto riasecScores = GameSessionData.riasecScores;
            string sourceLabel = NormalizeSourceLabel(GameSessionData.result_source);
            bool hasMeaningfulScores = LocalRubricFallback.HasMeaningfulScores(riasecScores);
            bool hasMeaningfulStoredOutcome =
                IsMeaningfulResultValue(GameSessionData.hollandCode) ||
                IsMeaningfulResultValue(GameSessionData.careerFamily) ||
                IsMeaningfulResultValue(GameSessionData.careerResult);
            bool isUndetermined = !hasMeaningfulScores && !hasMeaningfulStoredOutcome;

            string hollandCode = isUndetermined
                ? GameSessionData.UndeterminedResult
                : NormalizeResultValue(GameSessionData.hollandCode);

            string preferredCareerFamily = !string.IsNullOrWhiteSpace(GameSessionData.careerFamily)
                ? GameSessionData.careerFamily
                : GameSessionData.careerResult;
            string careerFamily = isUndetermined
                ? GameSessionData.UndeterminedResult
                : NormalizeResultValue(preferredCareerFamily);

            string perRoundResults = string.Empty;
            if (GameSessionData.round_results != null && GameSessionData.round_results.Count > 0)
            {
                for (int i = 0; i < GameSessionData.round_results.Count; i++)
                {
                    ChallengeRoundResult result = GameSessionData.round_results[i];
                    string solvedLabel = result.solved ? "Solved" : "Failed";
                    perRoundResults += $"{i + 1}. {result.challenge_id} | {result.stars_earned} star{(result.stars_earned == 1 ? string.Empty : "s")} | {solvedLabel}";

                    if (i < GameSessionData.round_results.Count - 1)
                        perRoundResults += "\n";
                }
            }
            else
            {
                perRoundResults = "No challenge session data recorded for this run.";
            }

            txt_RiasecNote.text =
                $"Result Source: {sourceLabel}\n" +
                $"Holland Code: {hollandCode}\n" +
                $"Career Family: {careerFamily}\n\n" +
                $"{BuildUndeterminedNote(isUndetermined)}" +
                "RIASEC Scores\n" +
                $"{FormatRiasecScores(riasecScores)}\n\n" +
                "Per-Round Results\n" +
                perRoundResults;
        }

        private static string FormatDuration(float totalSeconds)
        {
            int seconds = Mathf.Max(0, Mathf.RoundToInt(totalSeconds));
            return $"{seconds / 60:D2}:{seconds % 60:D2}";
        }

        private static string BuildUndeterminedNote(bool isUndetermined)
        {
            return isUndetermined
                ? "Not enough gameplay data was produced to determine a result.\n\n"
                : string.Empty;
        }

        private static string FormatRiasecScores(RiasecScoresDto scores)
        {
            return
                $"R: {GetScore(scores, score => score.r)}\n" +
                $"I: {GetScore(scores, score => score.i)}\n" +
                $"A: {GetScore(scores, score => score.a)}\n" +
                $"S: {GetScore(scores, score => score.s)}\n" +
                $"E: {GetScore(scores, score => score.e)}\n" +
                $"C: {GetScore(scores, score => score.c)}";
        }

        private static int GetScore(RiasecScoresDto scores, System.Func<RiasecScoresDto, int> selector)
        {
            return scores == null ? 0 : Mathf.Clamp(selector(scores), 0, 10);
        }

        private static bool IsMeaningfulResultValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.Equals(
                       value.Trim(),
                       GameSessionData.UndeterminedResult,
                       System.StringComparison.OrdinalIgnoreCase
                   );
        }

        private static string NormalizeResultValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GameSessionData.UndeterminedResult;

            return value.Trim();
        }

        private static string NormalizeSourceLabel(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return GameSessionData.LocalPrototypeResultSource;

            if (string.Equals(source, GameSessionData.RfModelResultSource, System.StringComparison.OrdinalIgnoreCase))
                return GameSessionData.RfModelResultSource;

            if (string.Equals(source, GameSessionData.BackendRubricFallbackResultSource, System.StringComparison.OrdinalIgnoreCase))
                return GameSessionData.BackendRubricFallbackResultSource;

            if (string.Equals(source, GameSessionData.LocalRubricFallbackResultSource, System.StringComparison.OrdinalIgnoreCase))
                return GameSessionData.LocalRubricFallbackResultSource;

            if (string.Equals(source, GameSessionData.LocalPrototypeResultSource, System.StringComparison.OrdinalIgnoreCase))
                return GameSessionData.LocalPrototypeResultSource;

            return source.Trim();
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
