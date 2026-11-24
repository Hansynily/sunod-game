using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

/// <summary>
/// College Fair mode: one booth phone, a short run per visitor. 3 towns, 1 day each; each
/// town offers 3 requests (3 different RIASEC letters, drawn at random per run with every
/// letter offered once or twice across the run) and the visitor plays 2, so 6 quests, then
/// the disaster, the result (always worked out on the phone) and a pitch for the full game.
///
/// The Settings switch (<see cref="Enabled"/>, admins only, off on every launch) only decides
/// whether the NEXT New Game is a fair run. The run itself carries the decision as a progress
/// marker (<see cref="RunMarker"/>, saved with the run like the day markers), so Continue keeps
/// the same rules and toggling mid-run changes nothing. Everything in the game checks
/// <see cref="ThisRun"/>, never the live switch. With fair mode off nothing here does anything.
/// </summary>
public static class vc_FairMode
{
    private const string OldPrefKey = "sunod.fair_mode";   // no longer used: fair mode is never saved

    /// <summary>Progress marker that makes a run a fair run (server: run state / loop journal).</summary>
    public const string RunMarker = "fair:1";

    public const int OfferedPerTown = 3;
    public const int PlayedPerTown = 2;
    public const int ExtraPresses = 2;
    public const float HintArrowDelay = 0f;   // fair runs: the arrow to the nearest open request shows at once
    public const int TotalQuests = PlayedPerTown * 3;
    public const int FullGameQuests = 48;

    /// <summary>Progress marker for one drawn offer: "fo:&lt;stage id&gt;:&lt;quest id&gt;".</summary>
    public const string OfferPrefix = "fo:";

    /// <summary>
    /// Never offered in a fair run: R3, I4, C7, A7 (Lv3 recommended), S7 (art bug), S6 and S8
    /// (long walks), plus any quest the Quest Lab timing check dropped.
    /// </summary>
    public static readonly string[] Excluded =
    {
        "c_r3_generator", "L2_CatMedicine", "c_c7_teller", "c_a7_stunt",
        "e_s7_story", "L1_LostFriend", "h_s8_lola",
    };

    private static readonly string[] StageIds = { "elementary", "highschool", "college" };

    private static string StageOfTown(string town) => town switch
    {
        "Town_Elementary" => "elementary",
        "Town_HighSchool" => "highschool",
        "Town_College" => "college",
        _ => null,
    };

    /// <summary>Fair runs started before the random draw (no "fo:" markers) keep these.</summary>
    public static readonly Dictionary<string, string[]> LegacyRequests = new Dictionary<string, string[]>
    {
        { "elementary", new[] { "e_i6_fish", "e_a6_drum", "e_e2_snacks" } },          // I6 Nico, A6 Paolo, E2 Jun
        { "highschool", new[] { "r4_radio", "L3_FallenSparrow", "h_c5_harvest" } },  // R4 Kuya Dan, S2 Liza, C5 Aling Nena
        { "college", new[] { "c_s1_career", "i5_mushrooms", "c_c1_payday" } },        // S1 Ms. Rivera, I5 Dr. Cruz, C1 Mr. Go
    };

    public static event Action Changed;

    /// <summary>
    /// TEMPORARY (2026-11-24, the developer's call): in the no-account Adventurer session (demo
    /// login, DEV "Play as Adventurer", the Editor's no-login start) fair mode starts ON. Logged-in
    /// accounts are never affected: for them it stays off on every launch. Set to false after the fair.
    /// </summary>
    public const bool OnForAdventurer = true;

    private static bool _enabled;                       // logged-in accounts (admin switch)
    private static bool _adventurer = OnForAdventurer;  // the Adventurer session

    private static bool IsAdventurer => SessionState.Instance == null || !SessionState.Instance.IsLoggedIn;

    /// <summary>
    /// The Settings switch. For logged-in accounts it's off on every launch: an admin turns it on
    /// for the booth each time the app starts, so it can never be left on by accident. The
    /// Adventurer session keeps its own value (<see cref="OnForAdventurer"/>). A run started with
    /// it on stays a fair run through Continue, via <see cref="RunMarker"/>.
    /// </summary>
    public static bool Enabled
    {
        get => IsAdventurer ? _adventurer : _enabled;
        set
        {
            if (IsAdventurer) _adventurer = value; else _enabled = value;
            Changed?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OffOnLaunch()
    {
        _enabled = false;
        _adventurer = OnForAdventurer;
        // older builds saved the switch on the device: clear it
        try { if (PlayerPrefs.HasKey(OldPrefKey)) { PlayerPrefs.DeleteKey(OldPrefKey); PlayerPrefs.Save(); } } catch { }
    }

    /// <summary>Is the run being played a fair run (locked at New Game)?</summary>
    public static bool ThisRun => vc_TownFlow.IsQuestDone(RunMarker);

    /// <summary>New Game: lock fair mode into the run if the switch is on, and draw its offers.</summary>
    public static void LockNewRun()
    {
        if (!Enabled) return;
        vc_TownFlow.MarkQuestDone(RunMarker);
        foreach (var kv in DrawOffers(new System.Random()))
            foreach (string id in kv.Value)
                vc_TownFlow.MarkQuestDone(OfferPrefix + kv.Key + ":" + id);
    }

    /// <summary>The fair markers of this run (saved with each town's entry checkpoint, so Continue keeps them).</summary>
    public static IEnumerable<string> RunMarkers()
    {
        foreach (string id in vc_TownFlow.CompletedIds)
            if (id == RunMarker || id.StartsWith(OfferPrefix)) yield return id;
    }

    /// <summary>This run's 3 offers in a town (the draw saved in the run; older fair runs: the fixed lists).</summary>
    public static string[] RequestsFor(string stageId)
    {
        if (stageId == null) return new string[0];
        string prefix = OfferPrefix + stageId + ":";
        var ids = vc_TownFlow.CompletedIds.Where(m => m.StartsWith(prefix)).Select(m => m.Substring(prefix.Length))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Length > 0) return ids;
        return LegacyRequests.TryGetValue(stageId, out string[] old) ? old : new string[0];
    }

    /// <summary>
    /// The random, balanced draw: each town gets 3 different letters, every RIASEC letter is
    /// offered once or twice across the 9 slots (3 random letters twice), and each slot is a
    /// random quest of that letter from that town (minus <see cref="Excluded"/>), never the same
    /// quest twice. Shuffles and retries until it holds.
    /// </summary>
    public static Dictionary<string, string[]> DrawOffers(System.Random rng)
    {
        var pool = new Dictionary<string, Dictionary<char, List<string>>>();
        foreach (vc_AdminDebug.LabQuest q in vc_AdminDebug.AllQuests())
        {
            string stage = StageOfTown(q.town);
            if (stage == null || string.IsNullOrEmpty(q.code) || Array.IndexOf(Excluded, q.questId) >= 0) continue;
            char letter = char.ToUpperInvariant(q.code[0]);
            if (!pool.TryGetValue(stage, out var byLetter)) pool[stage] = byLetter = new Dictionary<char, List<string>>();
            if (!byLetter.TryGetValue(letter, out var list)) byLetter[letter] = list = new List<string>();
            list.Add(q.questId);
        }

        for (int attempt = 0; attempt < 500; attempt++)
        {
            // 6 letters + 3 random repeats = 9 slots, shuffled into 3 towns of 3
            var letters = Order.ToList();
            letters.AddRange(Order.OrderBy(_ => rng.Next()).Take(3));
            letters = letters.OrderBy(_ => rng.Next()).ToList();
            var draw = new Dictionary<string, string[]>();
            bool ok = true;
            for (int t = 0; t < StageIds.Length && ok; t++)
            {
                var slots = letters.Skip(t * OfferedPerTown).Take(OfferedPerTown).ToList();
                if (slots.Distinct().Count() != OfferedPerTown) { ok = false; break; }
                var picked = new List<string>();
                foreach (char l in slots)
                {
                    if (!pool.TryGetValue(StageIds[t], out var byLetter) || !byLetter.TryGetValue(l, out var quests) || quests.Count == 0)
                    { ok = false; break; }
                    picked.Add(quests[rng.Next(quests.Count)]);   // towns don't share quests, so never twice
                }
                draw[StageIds[t]] = picked.ToArray();
            }
            if (ok) return draw;
        }
        Debug.LogWarning("[FairMode] No balanced draw found; using the fixed lists.");
        return new Dictionary<string, string[]>(LegacyRequests);
    }

    /// <summary>The question code (e.g. "R6") of a quest id, from Resources/Debug/quest_lab.json.</summary>
    public static string CodeOf(string questId)
    {
        if (_codes == null)
        {
            _codes = new Dictionary<string, string>();
            foreach (vc_AdminDebug.LabQuest q in vc_AdminDebug.AllQuests())
                if (!string.IsNullOrEmpty(q.questId)) _codes[q.questId] = q.code ?? "";
        }
        return questId != null && _codes.TryGetValue(questId, out string c) ? c : "";
    }
    private static Dictionary<string, string> _codes;

    /// <summary>The threat for the ending when loop 1 has none (offline): never skip the disaster.</summary>
    public static string ThreatOrDefault(string threat) =>
        !string.IsNullOrEmpty(threat) ? threat : "typhoon";

    // ── the end screen ────────────────────────────────────────────────────

    /// <summary>The letters of the quests the visitor played, in the order they played them.</summary>
    public static string ChosenLetters()
    {
        var telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null) return "";
        var letters = new List<string>();
        var seen = new HashSet<string>();
        foreach (vc_SessionTelemetry.QuestRecord r in telemetry.GetAllRecords())
        {
            if (r == null || string.IsNullOrEmpty(r.questId) || !seen.Add(r.questId)) continue;
            string l = (r.primaryRiasec ?? "").Trim().ToUpperInvariant();
            if (l.Length == 1 && "RIASEC".Contains(l)) letters.Add(l);
        }
        return string.Join(" ", letters);
    }

    // ── the fair result, always worked out on the phone ──────────────────
    // Letter scores: each played quest gives stars x2 + solved x3 to its letter, with its 0-5
    // timer stars scaled to 0-3 (no rounding). In a finished town (2 played) the offered quest
    // the visitor skipped takes 2 off its letter. Ties on top: the fastest passed quest wins,
    // then RIASEC order. Cluster: the server's holland_match_cluster rule (top letter's
    // clusters, then the second letter, then a code starting with the top letter).
    // Keep CLUSTERS in sync with app/cluster_mapping.py.

    private static readonly string Order = "RIASEC";

    private class Cluster
    {
        public int id; public string code, label; public string[] careers;
    }

    private static readonly Cluster[] Clusters =
    {
        new Cluster { id = 0, code = "IA", label = "Research", careers = new[] { "Computer Scientist", "Biologist", "Chemist" } },
        new Cluster { id = 2, code = "RIC", label = "Engineering", careers = new[] { "Architect", "Civil Engineer", "Web Developer" } },
        new Cluster { id = 3, code = "AS", label = "Arts & Design", careers = new[] { "Graphic Artist", "Writer", "Musician" } },
        new Cluster { id = 5, code = "SEC", label = "Business & Finance", careers = new[] { "Accountant", "Financial Analyst", "Entrepreneur" } },
        new Cluster { id = 6, code = "S", label = "Social Services", careers = new[] { "Teacher", "Lawyer", "Counselor" } },
        new Cluster { id = 7, code = "SIA", label = "Healthcare", careers = new[] { "Doctor", "Nurse", "Pharmacist" } },
    };

    public const float SkipPenalty = 2f;

    /// <summary>One played quest, as the scoring sees it.</summary>
    public struct Played
    {
        public string questId; public char letter; public bool passed; public int stars5, stars3; public float seconds;
    }

    /// <summary>The quests this run played (latest record per quest, like the run summary).</summary>
    public static List<Played> PlayedQuests()
    {
        var list = new List<Played>();
        var telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null) return list;
        var latest = new Dictionary<string, vc_SessionTelemetry.QuestRecord>();
        foreach (vc_SessionTelemetry.QuestRecord r in telemetry.GetAllRecords())
            if (r != null && !string.IsNullOrEmpty(r.questId)) latest[r.questId] = r;
        foreach (vc_SessionTelemetry.QuestRecord r in latest.Values)
        {
            string l = (r.primaryRiasec ?? "").Trim().ToUpperInvariant();
            if (l.Length != 1 || Order.IndexOf(l[0]) < 0) continue;
            list.Add(new Played { questId = r.questId, letter = l[0], passed = r.completed, stars5 = r.stars5,
                                  stars3 = r.starsEarned, seconds = r.timeSpentSeconds });
        }
        return list;
    }

    /// <summary>Letter scores (rule above) from the played quests and this run's offers.</summary>
    public static Dictionary<char, float> LocalScores() => Score(PlayedQuests(), StageIds.ToDictionary(s => s, RequestsFor));

    /// <summary>The scoring itself (pure: played quests + the offers per town).</summary>
    public static Dictionary<char, float> Score(List<Played> played, Dictionary<string, string[]> offers)
    {
        var scores = Order.ToDictionary(c => c, _ => 0f);
        var playedIds = new HashSet<string>();
        foreach (Played p in played)
        {
            playedIds.Add(p.questId);
            float stars = p.stars5 >= 0 ? Mathf.Clamp(p.stars5, 0, 5) * 3f / 5f : Mathf.Clamp(p.stars3, 0, 3);
            scores[p.letter] += stars * 2f + (p.passed ? 3f : 0f);
        }
        // a finished town: the offer the visitor skipped counts against its letter
        foreach (var kv in offers)
        {
            if (kv.Value == null || kv.Value.Count(playedIds.Contains) < PlayedPerTown) continue;
            foreach (string id in kv.Value)
            {
                if (playedIds.Contains(id)) continue;
                string code = CodeOf(id);
                if (code.Length > 0 && scores.ContainsKey(char.ToUpperInvariant(code[0])))
                    scores[char.ToUpperInvariant(code[0])] -= SkipPenalty;
            }
        }
        return scores;
    }

    /// <summary>Letters best first: score, then the fastest passed quest, then RIASEC order.</summary>
    public static List<char> Rank(Dictionary<char, float> s, List<Played> played)
    {
        float Fastest(char l)
        {
            float best = float.MaxValue;
            foreach (Played p in played) if (p.letter == l && p.passed && p.seconds > 0f) best = Mathf.Min(best, p.seconds);
            return best;
        }
        var list = Order.ToList();
        list.Sort((a, b) =>
        {
            if (Mathf.Abs(s[a] - s[b]) > 1e-6f) return s[b].CompareTo(s[a]);
            int t = Fastest(a).CompareTo(Fastest(b));
            return t != 0 ? t : Order.IndexOf(a).CompareTo(Order.IndexOf(b));
        });
        return list;
    }

    public static int MatchCluster(Dictionary<char, float> s) => MatchCluster(s, PlayedQuests());

    /// <summary>Same rule as the server's holland_match_cluster (top letter's clusters, ties: second letter, then a code starting with the top letter), on the tie-broken ranking.</summary>
    public static int MatchCluster(Dictionary<char, float> s, List<Played> played)
    {
        var ranked = Rank(s, played);
        char top = ranked[0], second = ranked[1];
        var candidates = Clusters.Where(c => c.code.IndexOf(top) >= 0).ToList();
        if (candidates.Count == 0) candidates = Clusters.ToList();
        float Avg(Cluster c) => c.code.Sum(ch => s[ch]) / c.code.Length;
        float best = candidates.Max(Avg);
        return candidates.Where(c => best - Avg(c) <= 1e-6f)
            .OrderBy(c => c.code.IndexOf(second) >= 0 ? 0 : 1)
            .ThenBy(c => c.code[0] == top ? 0 : 1)
            .ThenBy(c => Array.IndexOf(Clusters, c))
            .First().id;
    }

    /// <summary>The result the visitor sees in a fair run (and in any offline run): worked out on the phone.</summary>
    public static void ApplyLocalResult()
    {
        List<Played> played = PlayedQuests();
        Dictionary<char, float> s = LocalScores();
        Cluster c = Array.Find(Clusters, x => x.id == MatchCluster(s, played));
        float max = Mathf.Max(1f, s.Values.Max());
        var dto = new RiasecScoresDto
        {
            r = Mathf.Max(0, Mathf.RoundToInt(s['R'] / max * 10)), i = Mathf.Max(0, Mathf.RoundToInt(s['I'] / max * 10)),
            a = Mathf.Max(0, Mathf.RoundToInt(s['A'] / max * 10)), s = Mathf.Max(0, Mathf.RoundToInt(s['S'] / max * 10)),
            e = Mathf.Max(0, Mathf.RoundToInt(s['E'] / max * 10)), c = Mathf.Max(0, Mathf.RoundToInt(s['C'] / max * 10)),
        };
        GameSessionData.ApplyRunSummaryTelemetry(new RunSummaryTelemetryOut
        {
            success = true, message = "On-phone result (fair mode).",
            source = "Fair Local Fallback", riasec_scores = dto, holland_code = c.code,
            career_family = c.label, career_result = c.label, model_version = "fair_local_v1",
        });
        GameSessionData.ApplyClusterPredictionTelemetry(new PredictionResponsePayload
        {
            predicted_cluster = c.id, career_cluster = -1, career_result = c.label, cluster_label = c.label,
            career_family = c.label, cluster_holland_code = c.code, cluster_example_careers = c.careers,
            source = "fair_local_fallback", model_version = "fair_local_v1",
        });
    }

    // ── next visitor ──────────────────────────────────────────────────────

    /// <summary>
    /// Wipes the run and the loops (same reset as New Game, server-side, waited for), so the
    /// next visitor starts clean at loop 1 with no keepsakes. Then the main menu. No logout.
    /// </summary>
    public static IEnumerator ResetForNextVisitor()
    {
        vc_SaveManager.DeleteSave();
        vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
        vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
        vc_SkillLevels.PendingRestore = null;
        PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);
        PlayerPrefs.Save();
        vc_TownFlow.ResetRun();
        vc_SkillLevels.Clear();
        GameSessionData.Reset();
        vc_SessionTelemetry.Instance?.CancelCurrentSession();
        SessionState.Instance?.CancelRun();

        TelemetryManager tm = TelemetryManager.Instance;
        if (vc_OfflineSession.Active)
        {
            // offline admin session: a local reset only (nothing on the server to reset)
            vc_LoopMeta.Set(vc_OfflineSession.FreshLoop());
        }
        else if (tm != null)
        {
            bool done = false;
            tm.ResetMyRunState(
                _ => tm.ResetMyLoop(meta => { vc_LoopMeta.Set(meta); done = true; }, _ => { vc_LoopMeta.Clear(); done = true; }),
                _ => tm.ResetMyLoop(meta => { vc_LoopMeta.Set(meta); done = true; }, _ => { vc_LoopMeta.Clear(); done = true; }));
            for (float t = 0f; !done && t < 8f; t += Time.unscaledDeltaTime) yield return null;
        }
        else vc_LoopMeta.Clear();

        Time.timeScale = 1f;
        SceneLoader.GoToMainMenu();
    }
}
