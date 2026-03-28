using System;
using System.Collections.Generic;
using SunodGame.Models;
using UnityEngine;

namespace SunodGame.Core
{
    public static class LocalRubricFallback
    {
        private static readonly char[] DimensionLetters = { 'R', 'I', 'A', 'S', 'E', 'C' };

        public static bool TryApplyToCurrentSession(string backendFailureReason, out string error)
        {
            if (!TryCreateSummary(GameSessionData.round_results, backendFailureReason, out RunSummaryTelemetryOut summary, out error))
                return false;

            GameSessionData.ApplyRunSummaryTelemetry(summary);
            return true;
        }

        public static bool TryCreateSummary(
            IEnumerable<ChallengeRoundResult> rounds,
            string backendFailureReason,
            out RunSummaryTelemetryOut summary,
            out string error
        )
        {
            summary = null;
            error = string.Empty;

            try
            {
                int[] skillUseTotals = new int[DimensionLetters.Length];
                int[] tagStarTotals = new int[DimensionLetters.Length];
                BuildTotals(rounds, skillUseTotals, tagStarTotals);

                RiasecScoresDto integerScores = BuildIntegerScores(skillUseTotals, tagStarTotals);
                bool isUndetermined = IsUndetermined(integerScores);
                string hollandCode = DeriveHollandCode(integerScores, skillUseTotals);
                string resolvedCareerFamily = DeriveCareerFamily(integerScores);

                summary = new RunSummaryTelemetryOut
                {
                    success = true,
                    message = BuildFallbackMessage(backendFailureReason, isUndetermined),
                    source = GameSessionData.LocalRubricFallbackResultSource,
                    riasec_scores = integerScores,
                    holland_code = hollandCode,
                    career_family = resolvedCareerFamily,
                    career_result = resolvedCareerFamily,
                    model_version = "local_rubric_v1"
                };

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool HasMeaningfulScores(RiasecScoresDto scores)
        {
            return scores != null &&
                   (scores.r > 0 ||
                    scores.i > 0 ||
                    scores.a > 0 ||
                    scores.s > 0 ||
                    scores.e > 0 ||
                    scores.c > 0);
        }

        public static bool IsUndetermined(RiasecScoresDto scores)
        {
            return !HasMeaningfulScores(scores);
        }

        private static void BuildTotals(
            IEnumerable<ChallengeRoundResult> rounds,
            int[] skillUseTotals,
            int[] tagStarTotals
        )
        {
            if (rounds == null)
                return;

            foreach (ChallengeRoundResult roundResult in rounds)
            {
                if (roundResult == null)
                    continue;

                skillUseTotals[0] += Mathf.Max(0, roundResult.skill_use_r);
                skillUseTotals[1] += Mathf.Max(0, roundResult.skill_use_i);
                skillUseTotals[2] += Mathf.Max(0, roundResult.skill_use_a);
                skillUseTotals[3] += Mathf.Max(0, roundResult.skill_use_s);
                skillUseTotals[4] += Mathf.Max(0, roundResult.skill_use_e);
                skillUseTotals[5] += Mathf.Max(0, roundResult.skill_use_c);

                int tagIndex = (int)roundResult.primary_riasec;
                if (tagIndex < 0 || tagIndex >= tagStarTotals.Length)
                    continue;

                tagStarTotals[tagIndex] += Mathf.Max(0, roundResult.stars_earned);
            }
        }

        private static RiasecScoresDto BuildIntegerScores(int[] skillUseTotals, int[] tagStarTotals)
        {
            int[] rawScores = new int[DimensionLetters.Length];
            int totalRawScore = 0;

            for (int i = 0; i < DimensionLetters.Length; i++)
            {
                rawScores[i] = (2 * skillUseTotals[i]) + (3 * tagStarTotals[i]);
                totalRawScore += rawScores[i];
            }

            if (totalRawScore <= 0)
                return CreateZeroScores();

            int[] normalizedScores = new int[DimensionLetters.Length];
            for (int i = 0; i < DimensionLetters.Length; i++)
            {
                float scaledScore = ((float)rawScores[i] / totalRawScore) * 10f;
                normalizedScores[i] = Mathf.Clamp(RoundHalfUp(scaledScore), 0, 10);
            }

            return new RiasecScoresDto
            {
                r = normalizedScores[0],
                i = normalizedScores[1],
                a = normalizedScores[2],
                s = normalizedScores[3],
                e = normalizedScores[4],
                c = normalizedScores[5]
            };
        }

        private static RiasecScoresDto CreateZeroScores()
        {
            return new RiasecScoresDto
            {
                r = 0,
                i = 0,
                a = 0,
                s = 0,
                e = 0,
                c = 0
            };
        }

        private static int RoundHalfUp(float value)
        {
            return Mathf.FloorToInt(Mathf.Max(0f, value) + 0.5f);
        }

        private static string DeriveHollandCode(RiasecScoresDto scores, int[] skillUseTotals)
        {
            if (IsUndetermined(scores))
                return GameSessionData.UndeterminedResult;

            List<int> orderedIndices = new List<int> { 0, 1, 2, 3, 4, 5 };
            orderedIndices.Sort((left, right) =>
            {
                int scoreComparison = GetScoreByIndex(scores, right).CompareTo(GetScoreByIndex(scores, left));
                if (scoreComparison != 0)
                    return scoreComparison;

                int skillUseComparison = skillUseTotals[right].CompareTo(skillUseTotals[left]);
                if (skillUseComparison != 0)
                    return skillUseComparison;

                return left.CompareTo(right);
            });

            return $"{DimensionLetters[orderedIndices[0]]}{DimensionLetters[orderedIndices[1]]}{DimensionLetters[orderedIndices[2]]}";
        }

        private static string DeriveCareerFamily(RiasecScoresDto scores)
        {
            if (IsUndetermined(scores))
                return GameSessionData.UndeterminedResult;

            float technicalOperations = (0.60f * scores.r) + (0.40f * scores.c);
            float researchAnalysis = (0.75f * scores.i) + (0.25f * scores.c);
            float creativeMedia = (0.75f * scores.a) + (0.25f * scores.e);
            float peopleLeadership = (0.65f * scores.s) + (0.35f * scores.e);

            string bestFamily = "Technical & Operations";
            float bestScore = technicalOperations;

            if (researchAnalysis > bestScore)
            {
                bestFamily = "Research & Analysis";
                bestScore = researchAnalysis;
            }

            if (creativeMedia > bestScore)
            {
                bestFamily = "Creative & Media";
                bestScore = creativeMedia;
            }

            if (peopleLeadership > bestScore)
                bestFamily = "People & Leadership";

            return bestFamily;
        }

        private static int GetScoreByIndex(RiasecScoresDto scores, int index)
        {
            return index switch
            {
                0 => scores.r,
                1 => scores.i,
                2 => scores.a,
                3 => scores.s,
                4 => scores.e,
                5 => scores.c,
                _ => 0
            };
        }

        private static string BuildFallbackMessage(string backendFailureReason, bool isUndetermined)
        {
            string trimmedReason = string.IsNullOrWhiteSpace(backendFailureReason)
                ? string.Empty
                : backendFailureReason.Trim();

            string message = string.IsNullOrWhiteSpace(trimmedReason)
                ? "Backend unavailable. Local rubric fallback was used."
                : $"Backend unavailable ({trimmedReason}). Local rubric fallback was used.";

            if (isUndetermined)
                message += " Not enough gameplay data was produced to determine a result.";

            return message;
        }
    }
}
