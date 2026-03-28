using System.Collections.Generic;
using SunodGame.Models;

namespace SunodGame.Core
{
    // Shared active demo session state for the challenge-session runtime.
    public static class GameSessionData
    {
        public const int TotalRoundsPerSession = 6;
        public const int MaxStarsPerRound = 3;
        public const string RfModelResultSource = "RF Model";
        public const string BackendRubricFallbackResultSource = "Backend Rubric Fallback";
        public const string LocalRubricFallbackResultSource = "Local Rubric Fallback";
        public const string LocalPrototypeResultSource = "Local Prototype";
        public const string UndeterminedResult = "Undetermined";

        public static string session_id = string.Empty;
        public static string started_at = string.Empty;
        public static float total_time_seconds;
        public static List<ChallengeRoundResult> round_results = new();
        public static int rounds_attempted;
        public static int rounds_cleared;
        public static int total_stars;
        public static string result_source = string.Empty;

        public static string hollandCode = "";
        public static string careerResult = "";
        public static string careerFamily = "";
        public static string modelVersion = "";
        public static string backendMessage = "";
        public static RiasecScoresDto riasecScores;
        public static RunSummaryTelemetryOut runSummaryTelemetry;

        static GameSessionData()
        {
            Reset();
        }

        public static void Reset()
        {
            session_id = string.Empty;
            started_at = string.Empty;
            total_time_seconds = 0f;
            round_results = new List<ChallengeRoundResult>();
            rounds_attempted = 0;
            rounds_cleared = 0;
            total_stars = 0;
            result_source = string.Empty;

            hollandCode = string.Empty;
            careerResult = string.Empty;
            careerFamily = string.Empty;
            modelVersion = string.Empty;
            backendMessage = string.Empty;
            riasecScores = null;
            runSummaryTelemetry = null;
        }

        public static void ClearRunSummaryTelemetry()
        {
            result_source = string.Empty;
            hollandCode = string.Empty;
            careerResult = string.Empty;
            careerFamily = string.Empty;
            modelVersion = string.Empty;
            backendMessage = string.Empty;
            riasecScores = null;
            runSummaryTelemetry = null;
        }

        public static void ApplyRunSummaryTelemetry(RunSummaryTelemetryOut summary)
        {
            if (summary == null)
            {
                ClearRunSummaryTelemetry();
                return;
            }

            runSummaryTelemetry = summary;
            riasecScores = summary.riasec_scores;
            hollandCode = summary.holland_code ?? string.Empty;
            careerFamily = summary.career_family ?? string.Empty;
            careerResult = summary.career_result ?? string.Empty;
            modelVersion = summary.model_version ?? string.Empty;
            backendMessage = summary.message ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(summary.source))
                result_source = summary.source;
        }
    }
}
