using System;
using System.Collections.Generic;

// Converts gameplay telemetry into the fixed 48-feature questionnaire-like layout
// expected by the thesis model. This is a designed behavioral proxy layer, not a
// learned transformation, so each slot below is intentionally traceable to a single
// interpretable gameplay signal.
public static class vc_RiasecAdapter
{
    private static readonly string[] LetterOrder = { "R", "I", "A", "S", "E", "C" };
    private const int FeaturesPerLetter = 8;

    public static float[] BuildModelInput(vc_SessionTelemetry.SessionSummary summary, IReadOnlyList<vc_SessionTelemetry.QuestRecord> records)
    {
        float[] modelInput = new float[LetterOrder.Length * FeaturesPerLetter];
        if (records == null)
        {
            return modelInput;
        }

        Dictionary<string, int> totalUsageByLetter = BuildTotalUsageByLetter(summary, records);
        int dominantUsage = GetHighestValue(totalUsageByLetter);

        for (int letterIndex = 0; letterIndex < LetterOrder.Length; letterIndex++)
        {
            string letter = LetterOrder[letterIndex];
            int featureOffset = letterIndex * FeaturesPerLetter;

            int questsWithMaxUsage = 0;
            int questsWithAnyUsage = 0;
            int starsInPrimaryQuests = 0;
            int primaryQuestCount = 0;
            int completedPrimaryQuestCount = 0;
            float totalEfficiency = 0f;
            HashSet<int> levelsWithUsage = new HashSet<int>();

            for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                vc_SessionTelemetry.QuestRecord record = records[recordIndex];
                if (record == null)
                {
                    continue;
                }

                int usageForLetter = GetUsageForLetter(record.skillUsageCounts, letter);
                if (usageForLetter > 0)
                {
                    questsWithAnyUsage++;

                    int levelNumber = ExtractLevelNumber(record.questId);
                    if (levelNumber > 0)
                    {
                        levelsWithUsage.Add(levelNumber);
                    }
                }

                // Tied highest letters all count as "most-used" for that quest.
                if (usageForLetter > 0 && usageForLetter == GetHighestUsageForQuest(record.skillUsageCounts))
                {
                    questsWithMaxUsage++;
                }

                if (!string.Equals(NormalizeLetter(record.primaryRiasec), letter, StringComparison.Ordinal))
                {
                    continue;
                }

                primaryQuestCount++;
                starsInPrimaryQuests += Math.Clamp(record.starsEarned, 0, 3);

                if (record.completed)
                {
                    completedPrimaryQuestCount++;
                }

                totalEfficiency += GetQuestEfficiency(record);
            }

            bool isDominantLetter = dominantUsage > 0 && totalUsageByLetter[letter] == dominantUsage;
            float completionRate = primaryQuestCount > 0 ? (float)completedPrimaryQuestCount / primaryQuestCount : 0f;
            float averageEfficiency = primaryQuestCount > 0 ? totalEfficiency / primaryQuestCount : 0f;
            float levelConsistency = levelsWithUsage.Count > 0 ? (levelsWithUsage.Count / 3f) * 5f : 0f;

            modelInput[featureOffset + 0] = ClampToFive(totalUsageByLetter[letter]);
            modelInput[featureOffset + 1] = ClampToFive(questsWithMaxUsage);
            modelInput[featureOffset + 2] = ClampToFive(questsWithAnyUsage);
            modelInput[featureOffset + 3] = ClampToFive(starsInPrimaryQuests);
            modelInput[featureOffset + 4] = ClampToFive(completionRate * 5f);
            modelInput[featureOffset + 5] = ClampToFive(averageEfficiency * 5f);
            modelInput[featureOffset + 6] = isDominantLetter ? 5f : 0f;
            modelInput[featureOffset + 7] = ClampToFive(levelConsistency);
        }

        return modelInput;
    }

    private static Dictionary<string, int> BuildTotalUsageByLetter(vc_SessionTelemetry.SessionSummary summary, IReadOnlyList<vc_SessionTelemetry.QuestRecord> records)
    {
        Dictionary<string, int> totals = CreateLetterIntMap();
        if (summary != null && summary.totalSkillUsageCounts != null)
        {
            foreach (string letter in LetterOrder)
            {
                totals[letter] = Math.Max(0, GetUsageForLetter(summary.totalSkillUsageCounts, letter));
            }

            return totals;
        }

        for (int i = 0; i < records.Count; i++)
        {
            vc_SessionTelemetry.QuestRecord record = records[i];
            if (record == null)
            {
                continue;
            }

            foreach (string letter in LetterOrder)
            {
                totals[letter] += GetUsageForLetter(record.skillUsageCounts, letter);
            }
        }

        return totals;
    }

    private static int GetHighestUsageForQuest(IDictionary<string, int> skillUsageCounts)
    {
        int highest = 0;
        if (skillUsageCounts == null)
        {
            return highest;
        }

        foreach (string letter in LetterOrder)
        {
            highest = Math.Max(highest, GetUsageForLetter(skillUsageCounts, letter));
        }

        return highest;
    }

    private static float GetQuestEfficiency(vc_SessionTelemetry.QuestRecord record)
    {
        if (record == null)
        {
            return 0f;
        }

        float totalTime = Math.Max(0f, record.timeSpentSeconds) + Math.Max(0f, record.finalTimeRemainingSeconds);
        if (totalTime <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(record.finalTimeRemainingSeconds / totalTime, 0f, 1f);
    }

    private static int ExtractLevelNumber(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId) || questId.Length < 2 || char.ToUpperInvariant(questId[0]) != 'L')
        {
            return 0;
        }

        int digitIndex = 1;
        int levelNumber = 0;
        while (digitIndex < questId.Length && char.IsDigit(questId[digitIndex]))
        {
            levelNumber = (levelNumber * 10) + (questId[digitIndex] - '0');
            digitIndex++;
        }

        return levelNumber;
    }

    private static int GetHighestValue(Dictionary<string, int> valuesByLetter)
    {
        int highest = 0;
        foreach (string letter in LetterOrder)
        {
            highest = Math.Max(highest, valuesByLetter[letter]);
        }

        return highest;
    }

    private static int GetUsageForLetter(IDictionary<string, int> usageCounts, string letter)
    {
        if (usageCounts == null)
        {
            return 0;
        }

        string normalized = NormalizeLetter(letter);
        if (string.IsNullOrEmpty(normalized))
        {
            return 0;
        }

        return usageCounts.TryGetValue(normalized, out int count) ? Math.Max(0, count) : 0;
    }

    private static string NormalizeLetter(string rawLetter)
    {
        if (string.IsNullOrWhiteSpace(rawLetter))
        {
            return string.Empty;
        }

        string normalized = rawLetter.Trim().ToUpperInvariant();
        for (int i = 0; i < LetterOrder.Length; i++)
        {
            if (LetterOrder[i] == normalized)
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, int> CreateLetterIntMap()
    {
        return new Dictionary<string, int>
        {
            { "R", 0 },
            { "I", 0 },
            { "A", 0 },
            { "S", 0 },
            { "E", 0 },
            { "C", 0 }
        };
    }

    private static float ClampToFive(float value)
    {
        return Math.Clamp(value, 0f, 5f);
    }
}
