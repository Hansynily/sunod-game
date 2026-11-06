/// <summary>
/// Bonus stars: stars earned outside quests (town events, morning letters, an NPC's preferred
/// way). Stars are SUNOD's one currency. Each grant is saved as a marker "bs:&lt;source&gt;:&lt;n&gt;"
/// in the run's progress ids, so it survives Continue, counts once, and the server adds it to
/// the loop's star bank (sunod-game-server app/loop_meta.py bonus_stars_from_ids).
/// Bonus stars never touch a quest's stars, the timer or scoring.
/// </summary>
public static class vc_BonusStars
{
    public const string Prefix = "bs:";

    /// <summary>Was this source already paid (this run)?</summary>
    public static bool Granted(string source)
    {
        foreach (string id in vc_TownFlow.CompletedIds)
            if (id.StartsWith(Prefix + source + ":")) return true;
        return false;
    }

    /// <summary>Pays n stars once per source and saves it. Returns false if already paid.</summary>
    public static bool Grant(string source, int n, string sceneName)
    {
        if (string.IsNullOrEmpty(source) || n <= 0 || Granted(source)) return false;
        string marker = $"{Prefix}{source}:{n}";
        vc_TownFlow.MarkQuestDone(marker);
        vc_QuestRoom.SaveProgressIds(new[] { marker }, sceneName);
        return true;
    }

    /// <summary>Bonus stars earned so far this run.</summary>
    public static int Total
    {
        get
        {
            int total = 0;
            foreach (string id in vc_TownFlow.CompletedIds)
            {
                if (!id.StartsWith(Prefix)) continue;
                int colon = id.LastIndexOf(':');
                if (colon > 0 && int.TryParse(id.Substring(colon + 1), out int n)) total += n;
            }
            return total;
        }
    }
}
