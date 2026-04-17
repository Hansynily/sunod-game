using System;
using System.Collections.Generic;
using System.Text;
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
            bool hasRunSummary = HasUsableRunSummary();
            bool hasClusterFallback = TryGetClusterInfo(GetPredictedCluster(), out ClusterInfo clusterInfo);

            if (txt_EndTitle != null)
            {
                txt_EndTitle.text = hasRunSummary
                    ? BuildEndTitle(clusterInfo)
                    : hasClusterFallback
                        ? $"You leaned toward {clusterInfo.Label}"
                        : "Your result";
            }

            if (txt_Summary != null)
            {
                if (hasRunSummary)
                {
                    txt_Summary.text = BuildSummaryBlock();
                }
                else
                {
                    txt_Summary.text =
                        hasClusterFallback
                            ? $"Career Result: {clusterInfo.Label}\n" +
                              $"Holland Code: {clusterInfo.HollandCode}\n" +
                              "Quests Cleared: n/a\n" +
                              "Stars Earned: n/a\n" +
                              "Time Spent: n/a\n" +
                              "Result Source: Cluster Model"
                            : "Result unavailable\nNo final result data was recorded for this run.";
                }
            }

            if (txt_RiasecNote != null)
            {
                if (hasRunSummary)
                {
                    txt_RiasecNote.text = BuildRiasecExplanation(clusterInfo);
                }
                else
                {
                    txt_RiasecNote.text = hasClusterFallback && clusterInfo.ExampleCareers != null && clusterInfo.ExampleCareers.Length > 0
                        ? $"Example Careers: {string.Join(", ", clusterInfo.ExampleCareers)}"
                        : string.Empty;
                }
            }
        }

        private string BuildEndTitle(ClusterInfo clusterInfo)
        {
            if (IsUnmappedCareerResult(GameSessionData.careerResult))
            {
                string neutralTitle = BuildNeutralClusterTitle();
                if (!string.IsNullOrWhiteSpace(neutralTitle))
                    return $"You leaned toward {neutralTitle}";
            }

            if (!string.IsNullOrWhiteSpace(GameSessionData.careerResult))
                return $"You leaned toward {GameSessionData.careerResult}";

            if (!string.IsNullOrWhiteSpace(GameSessionData.careerFamily))
                return $"You leaned toward {GameSessionData.careerFamily}";

            if (!string.IsNullOrWhiteSpace(clusterInfo.Label))
                return $"You leaned toward {clusterInfo.Label}";

            return "Your result";
        }

        private string BuildSummaryBlock()
        {
            StringBuilder builder = new StringBuilder();
            string careerResult = !string.IsNullOrWhiteSpace(GameSessionData.careerResult)
                ? GameSessionData.careerResult
                : (!string.IsNullOrWhiteSpace(GameSessionData.careerFamily)
                    ? GameSessionData.careerFamily
                    : "Unavailable");

            builder.AppendLine($"Career Result: {careerResult}");
            builder.AppendLine($"Holland Code: {SafeText(GameSessionData.hollandCode, "n/a")}");
            builder.AppendLine($"Quests Cleared: {GameSessionData.rounds_cleared}/{GameSessionData.rounds_attempted}");
            builder.AppendLine($"Stars Earned: {GameSessionData.total_stars}");
            builder.AppendLine($"Time Spent: {Mathf.RoundToInt(GameSessionData.total_time_seconds)}s");
            builder.AppendLine($"Result Source: {SafeText(GameSessionData.result_source, "Unavailable")}");

            if (IsFallbackSource(GameSessionData.result_source) && !string.IsNullOrWhiteSpace(GameSessionData.backendMessage))
            {
                builder.AppendLine($"Note: {GetFirstLine(GameSessionData.backendMessage)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static bool IsUnmappedCareerResult(string value)
        {
            return string.Equals(
                value?.Trim(),
                "Specific career not yet mapped",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildNeutralClusterTitle()
        {
            if (GameSessionData.predictedCluster < 0)
                return string.Empty;

            if (GameSessionData.predictedCareerCluster >= 0)
                return $"Career Cluster {GameSessionData.predictedCareerCluster} in Cluster {GameSessionData.predictedCluster}";

            return $"Cluster {GameSessionData.predictedCluster}";
        }

        private string BuildRiasecExplanation(ClusterInfo clusterInfo)
        {
            string[] topLetters = GetTopRiasecLetters(GameSessionData.riasecScores, 3);
            if (topLetters.Length == 0)
            {
                if (IsFallbackSource(GameSessionData.result_source) && !string.IsNullOrWhiteSpace(GameSessionData.backendMessage))
                    return $"No RIASEC breakdown was recorded.\n{GetFirstLine(GameSessionData.backendMessage)}";

                return "No RIASEC breakdown was recorded for this run.";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("Top pattern: ");
            builder.Append(string.Join(", ", topLetters));
            builder.Append(". ");
            builder.Append(BuildPatternMeaning(topLetters));

            string[] exampleCareers = GameSessionData.predictedClusterExampleCareers;
            if (exampleCareers == null || exampleCareers.Length == 0)
            {
                exampleCareers = clusterInfo.ExampleCareers;
            }

            if (exampleCareers != null && exampleCareers.Length > 0)
            {
                builder.Append("\n");
                builder.Append("Example careers: ");
                builder.Append(string.Join(", ", exampleCareers));
            }

            if (IsFallbackSource(GameSessionData.result_source) && !string.IsNullOrWhiteSpace(GameSessionData.backendMessage))
            {
                builder.Append("\n");
                builder.Append("Note: ");
                builder.Append(GetFirstLine(GameSessionData.backendMessage));
            }

            return builder.ToString();
        }

        private static string BuildPatternMeaning(string[] topLetters)
        {
            if (topLetters == null || topLetters.Length == 0)
                return "This run did not produce a usable pattern summary.";

            string first = GetPatternPhrase(topLetters[0]);
            if (topLetters.Length == 1)
                return $"That points toward {first}.";

            string second = GetPatternPhrase(topLetters[1]);
            if (topLetters.Length == 2)
                return $"That points toward {first} and {second}.";

            string third = GetPatternPhrase(topLetters[2]);
            return $"That points toward {first}, {second}, and {third}.";
        }

        private static string GetPatternPhrase(string letter)
        {
            return letter switch
            {
                "R" => "hands-on problem solving",
                "I" => "analysis and observation",
                "A" => "creative expression",
                "S" => "helping and cooperation",
                "E" => "leading and persuasion",
                "C" => "structure and organization",
                _ => "a balanced skill mix"
            };
        }

        private static string[] GetTopRiasecLetters(RiasecScoresDto scores, int maxCount)
        {
            if (scores == null)
                return System.Array.Empty<string>();

            var entries = new List<KeyValuePair<string, int>>
            {
                new("R", scores.r),
                new("I", scores.i),
                new("A", scores.a),
                new("S", scores.s),
                new("E", scores.e),
                new("C", scores.c)
            };

            entries.Sort((left, right) => right.Value.CompareTo(left.Value));

            List<string> topLetters = new List<string>();
            for (int i = 0; i < entries.Count && topLetters.Count < Mathf.Max(1, maxCount); i++)
            {
                if (entries[i].Value <= 0)
                    continue;

                topLetters.Add(entries[i].Key);
            }

            return topLetters.ToArray();
        }

        private static int GetPredictedCluster()
        {
            vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
            return telemetry != null ? telemetry.PredictedCluster : -1;
        }

        private static bool HasUsableRunSummary()
        {
            return !string.IsNullOrWhiteSpace(GameSessionData.result_source)
                || !string.IsNullOrWhiteSpace(GameSessionData.careerFamily)
                || !string.IsNullOrWhiteSpace(GameSessionData.careerResult)
                || !string.IsNullOrWhiteSpace(GameSessionData.hollandCode)
                || GameSessionData.total_stars > 0
                || GameSessionData.rounds_attempted > 0
                || GameSessionData.riasecScores != null;
        }

        private static bool IsFallbackSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            return source == GameSessionData.BackendRubricFallbackResultSource
                || source == GameSessionData.LocalRubricFallbackResultSource
                || source == GameSessionData.LocalPrototypeResultSource;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string GetFirstLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            int lineBreakIndex = trimmed.IndexOfAny(new[] { '\r', '\n' });
            return lineBreakIndex >= 0 ? trimmed.Substring(0, lineBreakIndex).Trim() : trimmed;
        }

        private static bool TryGetClusterInfo(int predictedCluster, out ClusterInfo clusterInfo)
        {
            if (predictedCluster < 0 || predictedCluster >= ClusterMap.Length)
            {
                clusterInfo = default;
                return false;
            }

            clusterInfo = ClusterMap[predictedCluster];
            return true;
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
