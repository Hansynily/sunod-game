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
/// town offers 3 fixed requests (3 different RIASEC letters) and the visitor plays 2, so 6
/// quests, then the disaster, the result and a pitch for the full game.
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
    public const float HintArrowDelay = 5f;
    public const int TotalQuests = PlayedPerTown * 3;
    public const int FullGameQuests = 48;

    /// <summary>The fixed requests per town (quest ids, Resources/Debug/quest_lab.json).</summary>
    public static readonly Dictionary<string, string[]> Requests = new Dictionary<string, string[]>
    {
        { "elementary", new[] { "e_i6_fish", "e_a6_drum", "e_e2_snacks" } },          // I6 Nico, A6 Paolo, E2 Jun
        { "highschool", new[] { "r4_radio", "L3_FallenSparrow", "h_c5_harvest" } },  // R4 Kuya Dan, S2 Liza, C5 Aling Nena
        { "college", new[] { "c_s1_career", "i5_mushrooms", "c_c1_payday" } },        // S1 Ms. Rivera, I5 Dr. Cruz, C1 Mr. Go
    };

    public static event Action Changed;

    private static bool _enabled;

    /// <summary>
    /// The Settings switch. Off on every launch: an admin turns it on for the booth each time the
    /// app starts, so it can never be left on by accident (a run started with it on stays a fair
    /// run through Continue, via <see cref="RunMarker"/>).
    /// </summary>
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Changed?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OffOnLaunch()
    {
        _enabled = false;
        // older builds saved the switch on the device: clear it
        try { if (PlayerPrefs.HasKey(OldPrefKey)) { PlayerPrefs.DeleteKey(OldPrefKey); PlayerPrefs.Save(); } } catch { }
    }

    /// <summary>Is the run being played a fair run (locked at New Game)?</summary>
    public static bool ThisRun => vc_TownFlow.IsQuestDone(RunMarker);

    /// <summary>New Game: lock fair mode into the run if the switch is on.</summary>
    public static void LockNewRun()
    {
        if (Enabled) vc_TownFlow.MarkQuestDone(RunMarker);
    }

    public static string[] RequestsFor(string stageId) =>
        stageId != null && Requests.TryGetValue(stageId, out string[] ids) ? ids : new string[0];

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

    // ── backup result on the phone (server unreachable) ───────────────────
    // Mirrors the server: fair letter scores = stars x2 + solved x3 per quest letter
    // (routers/telemetry.py _rubric_raw_scores), then cluster_runtime.holland_match_cluster.
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

    /// <summary>Letter scores from the quest records (fair rule: quests only).</summary>
    public static Dictionary<char, float> LocalScores()
    {
        var scores = Order.ToDictionary(c => c, _ => 0f);
        var telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null) return scores;
        // latest record per quest wins, like the run summary
        var latest = new Dictionary<string, vc_SessionTelemetry.QuestRecord>();
        foreach (vc_SessionTelemetry.QuestRecord r in telemetry.GetAllRecords())
            if (r != null && !string.IsNullOrEmpty(r.questId)) latest[r.questId] = r;
        foreach (vc_SessionTelemetry.QuestRecord r in latest.Values)
        {
            string l = (r.primaryRiasec ?? "").Trim().ToUpperInvariant();
            if (l.Length != 1 || !scores.ContainsKey(l[0])) continue;
            scores[l[0]] += Mathf.Clamp(r.starsEarned, 0, 3) * 2f + (r.completed ? 3f : 0f);
        }
        return scores;
    }

    /// <summary>Same rule as the server's holland_match_cluster (top letter's clusters, ties: second letter, then a code starting with the top letter).</summary>
    public static int MatchCluster(Dictionary<char, float> s)
    {
        var ranked = Order.OrderBy(c => -s[c]).ThenBy(c => Order.IndexOf(c)).ToList();
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

    /// <summary>The server didn't answer: put an on-phone result in place so the end screen never shows an error.</summary>
    public static void ApplyLocalResult()
    {
        Dictionary<char, float> s = LocalScores();
        Cluster c = Array.Find(Clusters, x => x.id == MatchCluster(s));
        float max = Mathf.Max(1f, s.Values.Max());
        var dto = new RiasecScoresDto
        {
            r = Mathf.RoundToInt(s['R'] / max * 10), i = Mathf.RoundToInt(s['I'] / max * 10),
            a = Mathf.RoundToInt(s['A'] / max * 10), s = Mathf.RoundToInt(s['S'] / max * 10),
            e = Mathf.RoundToInt(s['E'] / max * 10), c = Mathf.RoundToInt(s['C'] / max * 10),
        };
        GameSessionData.ApplyRunSummaryTelemetry(new RunSummaryTelemetryOut
        {
            success = true, message = "On-phone result (fair mode, server unreachable).",
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
