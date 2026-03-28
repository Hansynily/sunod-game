using System.Collections.Generic;
using SunodGame.Core;
using SunodGame.Models;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        private void StoreSessionSummary()
        {
            GameSessionData.session_id = _sessionId;
            GameSessionData.started_at = _startedAtIso;
            GameSessionData.total_time_seconds = Mathf.Max(0f, Time.unscaledTime - _sessionStartTime);
            GameSessionData.round_results = CloneResults(_roundResults);
            GameSessionData.rounds_attempted = _roundResults.Count;
            GameSessionData.rounds_cleared = 0;
            GameSessionData.total_stars = 0;
            GameSessionData.result_source = string.Empty;

            for (int i = 0; i < _roundResults.Count; i++)
            {
                if (_roundResults[i].solved)
                    GameSessionData.rounds_cleared++;

                GameSessionData.total_stars += Mathf.Clamp(_roundResults[i].stars_earned, 0, GameSessionData.MaxStarsPerRound);
            }

            GameSessionData.ClearRunSummaryTelemetry();
        }

        private static void ApplyEmergencyPrototypeResult(string failureReason, string scoringError)
        {
            GameSessionData.ClearRunSummaryTelemetry();
            GameSessionData.result_source = GameSessionData.LocalPrototypeResultSource;
            GameSessionData.riasecScores = new RiasecScoresDto();
            GameSessionData.hollandCode = GameSessionData.UndeterminedResult;
            GameSessionData.careerFamily = GameSessionData.UndeterminedResult;
            GameSessionData.careerResult = GameSessionData.UndeterminedResult;
            GameSessionData.backendMessage = BuildEmergencyPrototypeMessage(failureReason, scoringError);
        }

        private static string BuildEmergencyPrototypeMessage(string failureReason, string scoringError)
        {
            string backendFailure = string.IsNullOrWhiteSpace(failureReason)
                ? "Unknown backend failure."
                : failureReason.Trim();

            string localFailure = string.IsNullOrWhiteSpace(scoringError)
                ? "Unknown local scoring failure."
                : scoringError.Trim();

            return $"Backend unavailable ({backendFailure}). Local rubric fallback also failed ({localFailure}).";
        }

        private ChallengeRoundResult BuildRoundResult(ChallengeDefinition definition, bool solved, int starsEarned, int retryCount)
        {
            return new ChallengeRoundResult
            {
                challenge_id = definition.challenge_id,
                primary_riasec = definition.primary_riasec,
                solved = solved,
                stars_earned = Mathf.Clamp(starsEarned, 0, GameSessionData.MaxStarsPerRound),
                retry_count = Mathf.Max(0, retryCount),
                time_spent_seconds = Mathf.Max(0f, Time.unscaledTime - _roundStartTime),
                skill_use_r = _activeRoundSkillUse[0],
                skill_use_i = _activeRoundSkillUse[1],
                skill_use_a = _activeRoundSkillUse[2],
                skill_use_s = _activeRoundSkillUse[3],
                skill_use_e = _activeRoundSkillUse[4],
                skill_use_c = _activeRoundSkillUse[5]
            };
        }

        private static List<ChallengeRoundResult> CloneResults(List<ChallengeRoundResult> source)
        {
            var cloned = new List<ChallengeRoundResult>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                ChallengeRoundResult result = source[i];
                cloned.Add(new ChallengeRoundResult
                {
                    challenge_id = result.challenge_id,
                    primary_riasec = result.primary_riasec,
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
                });
            }

            return cloned;
        }
    }
}
