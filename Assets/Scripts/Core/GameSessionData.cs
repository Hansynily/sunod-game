using System;
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
        public static int predictedCluster = -1;
        public static int predictedCareerCluster = -1;
        public static string predictedCareerResult = string.Empty;
        public static string predictedCareerFamily = string.Empty;
        public static string predictedClusterLabel = string.Empty;
        public static string predictedClusterHollandCode = string.Empty;
        public static string predictedSource = string.Empty;
        public static string predictedModelVersion = string.Empty;
        public static string[] predictedClusterExampleCareers = Array.Empty<string>();

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
            ClearPredictionTelemetry();
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
            ClearPredictionTelemetry();
        }

        public static void ClearPredictionTelemetry()
        {
            predictedCluster = -1;
            predictedCareerCluster = -1;
            predictedCareerResult = string.Empty;
            predictedCareerFamily = string.Empty;
            predictedClusterLabel = string.Empty;
            predictedClusterHollandCode = string.Empty;
            predictedSource = string.Empty;
            predictedModelVersion = string.Empty;
            predictedClusterExampleCareers = Array.Empty<string>();
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

        public static void ApplySessionMetrics(
            string sessionId,
            DateTime sessionStartUtc,
            float totalTimeSeconds,
            int roundsAttemptedValue,
            int roundsClearedValue,
            int totalStarsValue)
        {
            session_id = sessionId ?? string.Empty;
            started_at = sessionStartUtc == default ? string.Empty : sessionStartUtc.ToString("o");
            total_time_seconds = Math.Max(0f, totalTimeSeconds);
            rounds_attempted = Math.Max(0, roundsAttemptedValue);
            rounds_cleared = Math.Max(0, roundsClearedValue);
            total_stars = Math.Max(0, totalStarsValue);
        }

        public static void ApplyClusterPredictionTelemetry(PredictionResponsePayload prediction)
        {
            if (prediction == null)
            {
                ClearPredictionTelemetry();
                return;
            }

            ApplyClusterPredictionCore(
                prediction.predicted_cluster,
                prediction.career_cluster,
                prediction.career_result,
                prediction.career_family,
                prediction.cluster_label,
                prediction.cluster_holland_code,
                prediction.source,
                prediction.model_version,
                prediction.cluster_example_careers
            );
        }

        public static void ApplyClusterPredictionTelemetry(SessionClusterTelemetryOut telemetry)
        {
            if (telemetry == null)
            {
                ClearPredictionTelemetry();
                return;
            }

            ApplyClusterPredictionCore(
                telemetry.predicted_cluster,
                telemetry.career_cluster,
                telemetry.career_result,
                telemetry.career_family,
                telemetry.cluster_label,
                telemetry.holland_code,
                telemetry.source,
                telemetry.model_version,
                telemetry.cluster_example_careers
            );
        }

        private static void ApplyClusterPredictionCore(
            int cluster,
            int careerCluster,
            string careerResultValue,
            string careerFamilyValue,
            string clusterLabelValue,
            string clusterHollandCodeValue,
            string sourceValue,
            string modelVersionValue,
            string[] exampleCareers)
        {
            predictedCluster = cluster;
            predictedCareerCluster = careerCluster;
            predictedCareerResult = careerResultValue ?? string.Empty;
            predictedCareerFamily = careerFamilyValue ?? string.Empty;
            predictedClusterLabel = clusterLabelValue ?? string.Empty;
            predictedClusterHollandCode = clusterHollandCodeValue ?? string.Empty;
            predictedSource = sourceValue ?? string.Empty;
            predictedModelVersion = modelVersionValue ?? string.Empty;
            predictedClusterExampleCareers = exampleCareers ?? Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(careerResultValue))
                careerResult = careerResultValue;
            else if (!string.IsNullOrWhiteSpace(clusterLabelValue))
                careerResult = clusterLabelValue;

            if (!string.IsNullOrWhiteSpace(careerFamilyValue))
                careerFamily = careerFamilyValue;

            if (!string.IsNullOrWhiteSpace(sourceValue))
                result_source = sourceValue;
        }
    }
}
