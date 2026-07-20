using System;
using System.Collections.Generic;

// Option B: 1 quest = 1 RIASEC questionnaire item.
// Each quest is assigned a question code (R1..R8, I1..I8, A1..A8, S1..S8, E1..E8, C1..C8) and
// produces a 1-5 item score. This adapter places each quest's item score into its question slot,
// so the 48-feature vector sent to /api/predict matches the shape the model was trained on.
public static class vc_RiasecAdapter
{
    public const int FeatureCount = 48;
    public const float NeutralScore = 3f;

    // Canonical slot order - MUST match the model's training columns. Do not reorder.
    private static readonly string[] LetterOrder = { "R", "I", "A", "S", "E", "C" };

    public sealed class BuildResult
    {
        public float[] Features;                                 // always length 48, every value within 1..5
        public bool IsComplete;                                  // true only when all 48 slots got a real record
        public List<string> MissingCodes = new List<string>();   // question codes with no record (slot order)
        public List<string> IgnoredRecords = new List<string>(); // records dropped (empty/unknown question code)
    }

    // Builds the 48-feature vector from quest records.
    // requireCompleteRun only affects the IsComplete flag the caller checks; Features is always a
    // valid 48-vector (missing slots filled with the neutral score) so nothing downstream crashes.
    public static BuildResult BuildModelInput(IReadOnlyList<vc_SessionTelemetry.QuestRecord> records, bool requireCompleteRun)
    {
        Dictionary<string, int> slotMap = BuildSlotMap();
        BuildResult result = new BuildResult { Features = new float[FeatureCount] };
        bool[] filled = new bool[FeatureCount];

        if (records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                vc_SessionTelemetry.QuestRecord record = records[i];
                if (record == null) continue;

                string code = NormalizeCode(record.questionCode);
                if (string.IsNullOrEmpty(code) || !slotMap.TryGetValue(code, out int slot))
                {
                    result.IgnoredRecords.Add(string.IsNullOrEmpty(record.questId) ? "(no id)" : record.questId);
                    continue;
                }

                // Same code recorded twice (retry): the latest record wins.
                result.Features[slot] = ClampScore(record.itemScore);
                filled[slot] = true;
            }
        }

        foreach (KeyValuePair<string, int> entry in slotMap)
            if (!filled[entry.Value]) result.MissingCodes.Add(entry.Key);
        result.MissingCodes.Sort((a, b) => slotMap[a].CompareTo(slotMap[b]));

        result.IsComplete = result.MissingCodes.Count == 0;

        // Fill unfilled slots with a neutral score so Features is always usable.
        for (int s = 0; s < FeatureCount; s++)
            if (!filled[s]) result.Features[s] = NeutralScore;

        _ = requireCompleteRun; // caller decides what to do with IsComplete; kept for signature clarity.
        return result;
    }

    private static Dictionary<string, int> BuildSlotMap()
    {
        Dictionary<string, int> map = new Dictionary<string, int>(FeatureCount, StringComparer.Ordinal);
        int slot = 0;
        foreach (string letter in LetterOrder)
            for (int index = 1; index <= 8; index++)
                map[$"{letter}{index}"] = slot++;
        return map;
    }

    private static string NormalizeCode(string raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();
    }

    private static float ClampScore(int score)
    {
        return Math.Clamp(score, 1, 5);
    }
}
