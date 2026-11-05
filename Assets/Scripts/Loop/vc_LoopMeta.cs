using System;
using System.Collections.Generic;
using UnityEngine;
using SunodGame.Models;
using SunodGame.Telemetry;

/// <summary>
/// Loops: what carries over from one run to the next (the server keeps it, see
/// sunod-game-server/app/loop_meta.py). The disaster hits at the end of every run and the
/// player is sent back to childhood, keeping their memories:
///  - remembered skills start the next loop already in their kind's slot,
///  - keepsakes (bought with Sparks) change things around town only, never the quest timer,
///    stars or scoring,
///  - each loop rolls a threat (typhoon / flood / earthquake / fire) that colours the dreams,
///    the townsfolk's talk, the wrecked home and the ending,
///  - people remember you a little from loop 2 on.
/// This class holds the last copy fetched from the server plus the threat / keepsake text.
/// </summary>
public static class vc_LoopMeta
{
    public static LoopMetaResponse Current { get; private set; }
    public static bool Loaded => Current != null;

    public static int Loop => Current != null ? Mathf.Max(1, Current.current_loop) : 1;
    public static string Threat => Current != null && !string.IsNullOrEmpty(Current.threat) ? Current.threat : "";
    /// <summary>Loop 2 or later: people remember, the journal is open.</summary>
    public static bool IsRepeatLoop => Loop >= 2;

    public static event Action Changed;

    public static void Set(LoopMetaResponse meta)
    {
        Current = meta;
        Changed?.Invoke();
    }

    public static void Clear() => Current = null;

    /// <summary>Fetches the player's loop record. Calls back either way (null on failure).</summary>
    public static void Fetch(Action<LoopMetaResponse> done = null)
    {
        TelemetryManager tm = TelemetryManager.Instance;
        if (tm == null) { done?.Invoke(Current); return; }
        tm.GetMyLoop(meta => { Set(meta); done?.Invoke(meta); }, _ => done?.Invoke(Current));
    }

    public static bool IsKept(vc_SkillData skill) =>
        skill != null && Current != null && Current.kept_skills != null && Current.kept_skills.Contains(skill.skillName);

    /// <summary>A keepsake the player has (every keepsake they own comes along, every loop).</summary>
    public static bool HasMemento(string id) =>
        Current != null && Current.owned_mementos != null && Current.owned_mementos.Contains(id);
    public static bool HelpedLastLoop(string questId) =>
        Current != null && Current.last_passed != null && Current.last_passed.Contains(questId);

    public static bool FailedLastLoop(string questId) =>
        Current != null && Current.last_failed != null && Current.last_failed.Contains(questId);

    // ── Threats ───────────────────────────────────────────────────────────

    public static readonly string[] Threats = { "typhoon", "flood", "earthquake", "fire" };

    public static string ThreatName(string threat) => threat switch
    {
        "typhoon" => "Typhoon",
        "flood" => "Flood",
        "earthquake" => "Earthquake",
        "fire" => "Fire",
        _ => "Unknown",
    };

    /// <summary>One extra line for the dream after each stage (0 = Elementary, 1 = High School, 2 = College).</summary>
    public static string DreamLine(string threat, int stage)
    {
        if (!DreamLines.TryGetValue(threat ?? "", out string[] lines)) return "";
        return lines[Mathf.Clamp(stage, 0, lines.Length - 1)];
    }

    private static readonly Dictionary<string, string[]> DreamLines = new Dictionary<string, string[]>
    {
        { "typhoon", new[] {
            "In the dream, the wind screams and the roofs peel away like paper.",
            "Rain falls sideways in the dream. The river climbs into the streets.",
            "The sky turns grey-green. The storm is almost here.",
        } },
        { "flood", new[] {
            "In the dream, brown water rises past the windows.",
            "The bridge is gone in the dream. People wait on the rooftops.",
            "It hasn't stopped raining for days. The river is coming.",
        } },
        { "earthquake", new[] {
            "In the dream, the ground rolls like the sea. The school bell rings by itself.",
            "Cracks race up the walls in the dream, fast as lightning.",
            "A deep rumble under the town. The birds already know.",
        } },
        { "fire", new[] {
            "In the dream, orange light fills the sky. Smoke where the market used to be.",
            "Embers drift over the rooftops like fireflies. Too many of them.",
            "The air smells of smoke. Something is already burning.",
        } },
    };

    /// <summary>Townsfolk noticing the warning signs (any loop). One line, picked by seed.</summary>
    public static string ThreatChatter(string threat, int seed)
    {
        if (!Chatter.TryGetValue(threat ?? "", out string[] lines) || lines.Length == 0) return "";
        return lines[((seed % lines.Length) + lines.Length) % lines.Length];
    }

    private static readonly Dictionary<string, string[]> Chatter = new Dictionary<string, string[]>
    {
        { "typhoon", new[] {
            "The radio says a low-pressure area is forming out at sea. Probably nothing.",
            "My lola's knees hurt today. She says that means a big storm is coming.",
            "The birds flew inland this morning. All of them. Strange, no?",
        } },
        { "flood", new[] {
            "The river's higher than I've ever seen it in summer.",
            "The drains by the market are clogged again. If it rains hard, we're in trouble.",
            "Upstream, they say the old dam has a crack. Just a small one. For now.",
        } },
        { "earthquake", new[] {
            "Did you feel that little shake last night? No? Just me?",
            "The well water tastes funny lately. Lolo says that's a bad sign.",
            "There's a new crack in the school wall. It wasn't there last week.",
        } },
        { "fire", new[] {
            "Someone keeps burning trash behind the canteen. One day the wind will catch it.",
            "Those old wires on the poles spark when it's windy. Somebody should fix them.",
            "It's so dry this year. Even the grass crunches.",
        } },
    };

    // ── People remember you (loop 2 and later) ────────────────────────────

    /// <summary>The first time a quest giver asks this loop: a hint of déjà vu.</summary>
    public static string MemoryOpener(string questId, int seed)
    {
        string[] pool = HelpedLastLoop(questId) ? RememberHelped
            : FailedLastLoop(questId) ? RememberFailed
            : RememberVague;
        return pool[((seed % pool.Length) + pool.Length) % pool.Length];
    }

    private static readonly string[] RememberHelped =
    {
        "...Have we met? I feel like you helped me once. Before. That's impossible, right?",
        "Funny. I knew you'd come. I dreamt you already fixed this.",
        "You again! ...Sorry, I don't know why I said that. Have we met?",
    };

    private static readonly string[] RememberFailed =
    {
        "Have we done this before? It feels like we tried once and ran out of time.",
        "Strange... I keep thinking this didn't go well last time. What last time?",
    };

    private static readonly string[] RememberVague =
    {
        "Have we met before? You feel familiar somehow.",
        "Strange... I feel like I know you from somewhere.",
        "Do you ever get the feeling you've lived a day before? I do, right now.",
    };

    /// <summary>Roamers and other townsfolk from loop 2 on (now and then instead of their usual line).</summary>
    public static string DejaVuLine(int seed)
    {
        string[] pool = DejaVu;
        return pool[((seed % pool.Length) + pool.Length) % pool.Length];
    }

    private static readonly string[] DejaVu =
    {
        "You again? ...Sorry. I don't know why I said that.",
        "I had the strangest dream about you last night. You were older.",
        "Everything today feels like it already happened once.",
        "You look like someone who's seen how this story ends.",
    };

    // ── Keepsakes: one per townsperson below (one per RIASEC kind) ────────
    // Passing that person's quest unlocks it in the shop; it's bought with banked stars and
    // stays every loop after. Effects only change things around town (server: app/loop_meta.py).

    public class Keepsake
    {
        public string id, name, giver, code, effect, rememberLine;
    }

    public static readonly Keepsake[] Keepsakes =
    {
        new Keepsake { id = "compass", name = "Old Compass", giver = "Nico", code = "I6",
            effect = "Hint arrow shows up faster.",
            rememberLine = "Is that my compass? Weird." },
        new Keepsake { id = "ribbon", name = "Lucky Ribbon", giver = "Paolo", code = "A6",
            effect = "+1 skill pick in each new town.",
            rememberLine = "You have my ribbon?" },
        new Keepsake { id = "sneakers", name = "Old Sneakers", giver = "Jun", code = "E2",
            effect = "Walk faster in town.",
            rememberLine = "Hey, those are my sneakers!" },
        new Keepsake { id = "lantern", name = "Lola's Lantern", giver = "Lola Ising", code = "S8",
            effect = "+1 skill pick each night.",
            rememberLine = "That lantern... it's mine, apo." },
        new Keepsake { id = "locket", name = "Memory Locket", giver = "Aling Nena", code = "C5",
            effect = "Remembered skills increased to 4.",
            rememberLine = "My locket? How did you get that?" },
        new Keepsake { id = "whistle", name = "Mang Erning's Whistle", giver = "Mang Erning", code = "R5",
            effect = "+1 request each morning.",
            rememberLine = "That's my whistle!" },
    };

    public static Keepsake KeepsakeById(string id) => System.Array.Find(Keepsakes, k => k.id == id);

    public static Keepsake KeepsakeByCode(string code) =>
        string.IsNullOrEmpty(code) ? null : System.Array.Find(Keepsakes, k => string.Equals(k.code, code, StringComparison.OrdinalIgnoreCase));

    public static Texture2D KeepsakeIcon(string id) => Resources.Load<Texture2D>("Loop/memento_" + id);

    public static bool IsUnlocked(string id) =>
        Current != null && Current.unlocked_mementos != null && Current.unlocked_mementos.Contains(id);

    /// <summary>
    /// Passing this quest unlocks its keepsake in the shop (first time only). Applied right
    /// away; the server keeps it from the run's records.
    /// </summary>
    public static bool TryUnlock(string questCode, out Keepsake unlocked)
    {
        unlocked = KeepsakeByCode(questCode);
        if (unlocked == null || Current == null || IsUnlocked(unlocked.id) || HasMemento(unlocked.id)) { unlocked = null; return false; }
        Current.unlocked_mementos.Add(unlocked.id);
        Changed?.Invoke();
        return true;
    }

    // Unlocks the player has seen in the shop (per account, on this device).
    private static string SeenKey()
    {
        var s = SunodGame.Core.SessionState.Instance;
        return "sunod.shop_seen." + (s != null && !string.IsNullOrWhiteSpace(s.Username) ? s.Username : "guest");
    }

    /// <summary>Something unlocked but not bought, that the player hasn't seen in the shop yet.</summary>
    public static bool HasNewInShop()
    {
        if (Current == null) return false;
        string seen = "";
        try { seen = "," + PlayerPrefs.GetString(SeenKey(), "") + ","; } catch { }
        foreach (string id in Current.unlocked_mementos)
            if (!Current.owned_mementos.Contains(id) && !seen.Contains("," + id + ",")) return true;
        return false;
    }

    public static void MarkShopSeen()
    {
        if (Current == null) return;
        try { PlayerPrefs.SetString(SeenKey(), string.Join(",", Current.unlocked_mementos)); PlayerPrefs.Save(); } catch { }
        Changed?.Invoke();
    }

    public static int Price(string id)
    {
        if (Current?.shop != null)
            foreach (var item in Current.shop) if (item.id == id) return item.cost;
        return 0;
    }

    /// <summary>Remembered skills this loop (skill ids).</summary>
    public static System.Collections.Generic.List<string> KeptSkills =>
        Current != null && Current.kept_skills != null ? Current.kept_skills : new System.Collections.Generic.List<string>();
}