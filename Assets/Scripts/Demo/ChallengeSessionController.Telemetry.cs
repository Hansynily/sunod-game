using System.Collections.Generic;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        private void SubmitRunSummaryAndLoadEndScene()
        {
            TelemetryManager telemetry = TelemetryManager.Instance;
            if (telemetry == null)
            {
                Debug.LogWarning("[ChallengeSession] TelemetryManager not found. Using local rubric fallback.");
                CompleteWithLocalFallback("TelemetryManager not found. Backend request was skipped.");
                return;
            }

            RunSummaryTelemetryPayload payload = BuildRunSummaryPayload();
            telemetry.SubmitRunComplete(
                payload,
                onSuccess: HandleRunSummarySuccess,
                onError: HandleRunSummaryFailure
            );
        }

        private RunSummaryTelemetryPayload BuildRunSummaryPayload()
        {
            SessionState session = SessionState.Instance;
            string fallbackUsername = session != null && !string.IsNullOrWhiteSpace(session.Username)
                ? session.Username
                : "DemoPlayer";

            var payload = new RunSummaryTelemetryPayload
            {
                player_id = session != null ? session.PlayerId : SystemInfo.deviceUniqueIdentifier,
                username = fallbackUsername,
                session_id = GameSessionData.session_id,
                scene_version = "single_room_v1",
                total_time_spent_seconds = GameSessionData.total_time_seconds,
                rounds = new List<ChallengeRoundTelemetryPayload>()
            };

            if (GameSessionData.round_results == null)
                return payload;

            for (int i = 0; i < GameSessionData.round_results.Count; i++)
                payload.rounds.Add(BuildRoundPayload(GameSessionData.round_results[i]));

            return payload;
        }

        private static ChallengeRoundTelemetryPayload BuildRoundPayload(ChallengeRoundResult result)
        {
            return new ChallengeRoundTelemetryPayload
            {
                challenge_id = result.challenge_id,
                primary_riasec = result.primary_riasec.ToString(),
                solved = result.solved,
                stars_earned = result.stars_earned,
                retry_count = result.retry_count,
                time_spent_seconds = result.time_spent_seconds,
                skill_use_r = result.skill_use_r,
                skill_use_i = result.skill_use_i,
                skill_use_a = result.skill_use_a,
                skill_use_s = result.skill_use_s,
                skill_use_e = result.skill_use_e,
                skill_use_c = result.skill_use_c
            };
        }

        private void HandleRunSummarySuccess(RunSummaryTelemetryOut response)
        {
            if (response == null || !response.success)
            {
                HandleRunSummaryFailure("Run-complete response was empty or unsuccessful.");
                return;
            }

            GameSessionData.ApplyRunSummaryTelemetry(response);
            SceneLoader.GoToEnd();
        }

        private void HandleRunSummaryFailure(string error)
        {
            CompleteWithLocalFallback(error ?? "Run-complete telemetry failed.");
        }

        private void CompleteWithLocalFallback(string failureReason)
        {
            if (LocalRubricFallback.TryApplyToCurrentSession(failureReason, out string scoringError))
            {
                Debug.LogWarning($"[ChallengeSession] {GameSessionData.backendMessage}");
            }
            else
            {
                Debug.LogError(
                    $"[ChallengeSession] Local rubric fallback failed. Falling back to Local Prototype. Error: {scoringError}"
                );
                ApplyEmergencyPrototypeResult(failureReason, scoringError);
            }

            SceneLoader.GoToEnd();
        }
    }
}
