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
///  - keepsakes (bought with stars, or given by town events) - only the ones in your pockets
///    work; none touch a quest's scored stars or scoring,
///    (pockets, helpers and cosmetics are bought at the same stall),
///  - each loop rolls a threat (typhoon / flood / earthquake / fire) that colours the dreams,
///    the townsfolk's talk, the wrecked home and the ending,
///  - people remember you a little from loop 2 on.
/// This class holds the last copy fetched from the server plus the threat / keepsake text.
/// </summary>
public static partial class vc_LoopMeta
{
    public static LoopMetaResponse Current { get; private set; }
    public static bool Loaded => Current != null;

    public static int Loop => Current != null ? Mathf.Max(1, Current.current_loop) : 1;
    public static string Threat => Current != null && !string.IsNullOrEmpty(Current.threat) ? Current.threat : "";
    /// <summary>Loop 2 or later: people remember, the journal is open.</summary>
    public static bool IsRepeatLoop => Loop >= 2;

    public static event Action Changed;

    /// <summary>A loop record from the server: cached for offline play, then this run's stall
    /// actions that haven't reached the server yet are laid back on top (vc_LoopMeta.Offline).</summary>
    public static void Set(LoopMetaResponse meta)
    {
        Current = meta;
        SaveCache(meta);
        ApplyLocalMarkers();
        Changed?.Invoke();
    }

    public static void Clear() => Current = null;

    /// <summary>Fetches the player's loop record. Calls back either way (null on failure).</summary>
    public static void Fetch(Action<LoopMetaResponse> done = null)
    {
        TelemetryManager tm = TelemetryManager.Instance;
        if (tm == null) { LoadCacheIfEmpty(); done?.Invoke(Current); return; }
        // offline: keep the copy we have (or the last one cached on this device)
        tm.GetMyLoop(meta => { Set(meta); done?.Invoke(meta); }, _ => { LoadCacheIfEmpty(); done?.Invoke(Current); });
    }

    public static bool IsKept(vc_SkillData skill) =>
        skill != null && Current != null && Current.kept_skills != null && Current.kept_skills.Contains(skill.skillName);

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

    // ── Keepsakes ─────────────────────────────────────────────────────────
    // 12 from townspeople (passing their quest unlocks it in the stall, bought with stars) and
    // 3 from town events (free). Owned forever, but only the ones in your pockets this loop
    // work and show (2 pockets to start, more at the stall). Each giver keepsake can be
    // upgraded once (Lv2) after helping its giver again, their preferred way, in a later loop.
    // None helps one RIASEC letter more than another or changes a quest's scored stars.
    // Server: sunod-game-server app/loop_meta.py.

    public class Keepsake
    {
        public string id, name, giver, code, effect, lv2Effect, rememberLine, lv2Line;
        public bool fromEvent;   // free, never sold
    }

    public static readonly Keepsake[] Keepsakes =
    {
        new Keepsake { id = "compass", name = "Old Compass", giver = "Nico", code = "I6",
            effect = "Hint arrow shows up faster.",
            lv2Effect = "Also points toward today's town event.",
            rememberLine = "Is that my compass? Weird.",
            lv2Line = "My compass... it's pointing somewhere new. Follow it!" },
        new Keepsake { id = "sneakers", name = "Old Sneakers", giver = "Jun", code = "E2",
            effect = "Walk faster in town.",
            lv2Effect = "Walk faster still, with a dust trail.",
            rememberLine = "Hey, those are my sneakers!",
            lv2Line = "Whoa, you broke them in. They kick up dust now!" },
        new Keepsake { id = "ribbon", name = "Lucky Ribbon", giver = "Paolo", code = "A6",
            effect = "+1 skill pick in each new town.",
            lv2Effect = "+2 skill picks in each new town.",
            rememberLine = "You have my ribbon?",
            lv2Line = "My ribbon looks brighter on you. Weird, right?" },
        new Keepsake { id = "hammer", name = "Papa's Hammer", giver = "Mang Ben", code = "R2",
            effect = "+1 press on every quest.",
            lv2Effect = "+2 presses on every quest.",
            rememberLine = "That's my father's hammer. How do you have it?",
            lv2Line = "You hold it like Papa did. Steady hands." },
        new Keepsake { id = "lantern", name = "Lola's Lantern", giver = "Lola Ising", code = "S8",
            effect = "+1 skill pick each night.",
            lv2Effect = "The night pick shows 4 cards.",
            rememberLine = "That lantern... it's mine, apo.",
            lv2Line = "It burns brighter with you, apo. Like it knows the way." },
        new Keepsake { id = "locket", name = "Memory Locket", giver = "Aling Nena", code = "C5",
            effect = "Remember 4 skills.",
            lv2Effect = "Remembered skills start with +2 XP.",
            rememberLine = "My locket? How did you get that?",
            lv2Line = "There's a new picture inside. I never put it there." },
        new Keepsake { id = "notebook", name = "Field Notebook", giver = "Prof. Lim", code = "I8",
            effect = "Requests show the prep skill.",
            lv2Effect = "Requests also show the preferred skill.",
            rememberLine = "That's my field notebook. My handwriting... and yours?",
            lv2Line = "You've filled in pages I haven't written yet." },
        new Keepsake { id = "jacket", name = "Vintage Jacket", giver = "Ate Joan", code = "E6",
            effect = "Preferred-way thank-yous give 2x bonus stars.",
            lv2Effect = "Preferred-way thank-yous give 3x bonus stars.",
            rememberLine = "Wait, that's the jacket from my rack! It suits you.",
            lv2Line = "That jacket's seen a few lifetimes, huh? It shows." },
        new Keepsake { id = "whistle", name = "Mang Erning's Whistle", giver = "Mang Erning", code = "R5",
            effect = "+1 request each morning.",
            lv2Effect = "+2 requests each morning.",
            rememberLine = "That's my whistle!",
            lv2Line = "Blow it once. Half the street comes running now." },
        new Keepsake { id = "nametag", name = "Name Tag", giver = "Ms. Rivera", code = "S1",
            effect = "Forgotten skills keep 1/2 their XP.",
            lv2Effect = "Forgotten skills keep 3/4 their XP.",
            rememberLine = "That name tag... I wrote your name on it. When?",
            lv2Line = "Your name's still on it. Some things you don't forget." },
        new Keepsake { id = "presspass", name = "Press Pass", giver = "Kevin", code = "A5",
            effect = "+1 XP to the skill you pick each night.",
            lv2Effect = "+2 XP to the skill you pick each night.",
            rememberLine = "That's our press pass! Who signed it off to you?",
            lv2Line = "Front-row access, huh? You've seen the big stories." },
        new Keepsake { id = "coinpurse", name = "Coin Purse", giver = "Ms. Uy", code = "C7",
            effect = "+1 bank star per quest passed.",
            lv2Effect = "+2 bank stars per quest passed.",
            rememberLine = "My coin purse? I counted it this morning. Still here... and there?",
            lv2Line = "It's heavier than I remember. You've been saving." },

        // town events (free, never sold)
        new Keepsake { id = "banderitas", name = "Banderitas", giver = "the Fiesta", fromEvent = true,
            effect = "Start each new town with 1 extra energy.",
            rememberLine = "Banderitas! Already? The fiesta isn't for days.",
            lv2Effect = "" },
        new Keepsake { id = "shell", name = "Lucky Shell", giver = "a town event", fromEvent = true,
            effect = "See tomorrow's requests at night.",
            rememberLine = "That shell hums when you're near. Did the sea give you that?",
            lv2Effect = "" },
        new Keepsake { id = "luckycoin", name = "Lucky Coin", giver = "all six town events", fromEvent = true,
            effect = "Once a loop, one quest gives double bank stars.",
            rememberLine = "An old coin from every corner of town. You've been everywhere, huh?",
            lv2Effect = "" },
    };

    public static Keepsake KeepsakeById(string id) => System.Array.Find(Keepsakes, k => k.id == id);

    public static Keepsake KeepsakeByCode(string code) =>
        string.IsNullOrEmpty(code) ? null : System.Array.Find(Keepsakes, k => !k.fromEvent && string.Equals(k.code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>The keepsake's icon (Lv2 has its own upgraded drawing when there is one).</summary>
    public static Texture2D KeepsakeIcon(string id, bool lv2 = false)
    {
        Texture2D t = lv2 ? Resources.Load<Texture2D>("Loop/memento_" + id + "_lv2") : null;
        return t != null ? t : Resources.Load<Texture2D>("Loop/memento_" + id);
    }

    public static bool Owns(string id) =>
        Current != null && Current.owned_mementos != null && Current.owned_mementos.Contains(id);

    /// <summary>Keepsakes in your pockets this loop (only these work and show).</summary>
    public static List<string> Carried =>
        Current != null && Current.carried_mementos != null ? Current.carried_mementos : new List<string>();

    /// <summary>A keepsake that works right now: owned and in your pockets this loop.</summary>
    public static bool HasMemento(string id) => Owns(id) && Carried.Contains(id);

    public static bool IsLv2(string id) =>
        Current != null && Current.lv2_owned != null && Current.lv2_owned.Contains(id);

    /// <summary>0 = not carried, 1 = carried, 2 = carried at Lv2.</summary>
    public static int Level(string id) => !HasMemento(id) ? 0 : IsLv2(id) ? 2 : 1;

    public static int Pockets => Current != null ? Mathf.Clamp(Current.pockets, 2, 4) : 2;

    public static bool HelpedLastLoop(string questId) =>
        Current != null && Current.last_passed != null && Current.last_passed.Contains(questId);

    public static bool FailedLastLoop(string questId) =>
        Current != null && Current.last_failed != null && Current.last_failed.Contains(questId);

    public static bool IsUnlocked(string id) =>
        Current != null && Current.unlocked_mementos != null && Current.unlocked_mementos.Contains(id);

    public static bool Lv2Unlocked(string id) =>
        Current != null && Current.lv2_unlocked != null && Current.lv2_unlocked.Contains(id) && !IsLv2(id);

    /// <summary>What a giver says from loop 2 on when you carry their keepsake.</summary>
    public static string RememberLine(Keepsake k) =>
        k == null ? "" : IsLv2(k.id) && !string.IsNullOrEmpty(k.lv2Line) ? k.lv2Line : k.rememberLine;

    /// <summary>
    /// Passing this quest unlocks its keepsake in the shop (first time only). Applied right
    /// away; the server keeps it from the run's records.
    /// </summary>
    public static bool TryUnlock(string questCode, out Keepsake unlocked)
    {
        unlocked = KeepsakeByCode(questCode);
        if (unlocked == null || Current == null || IsUnlocked(unlocked.id) || Owns(unlocked.id)) { unlocked = null; return false; }
        Current.unlocked_mementos.Add(unlocked.id);
        Changed?.Invoke();
        return true;
    }

    /// <summary>An event host noticing an event keepsake you carry (loop 2+), or "".</summary>
    public static string EventHostLine(string eventId, int seed)
    {
        if (!IsRepeatLoop) return "";
        var lines = new List<string>();
        if (HasMemento("shell")) lines.Add("That shell... I gave one just like it to someone, once. Or I will?");
        if (HasMemento("luckycoin")) lines.Add("Is that the old town coin? You must have been at every gathering. Every single one.");
        if (HasMemento("banderitas")) lines.Add("You carry the fiesta with you, huh? Those banderitas look a hundred years old.");
        if (lines.Count == 0) return "";
        return lines[((seed % lines.Count) + lines.Count) % lines.Count];
    }

    // Unlocks the player has seen in the shop (per account, on this device).
    private static string SeenKey()
    {
        var s = SunodGame.Core.SessionState.Instance;
        return "sunod.shop_seen." + (s != null && !string.IsNullOrWhiteSpace(s.Username) ? s.Username : "guest");
    }

    private static IEnumerable<string> ShopNews()
    {
        if (Current == null) yield break;
        foreach (string id in Current.unlocked_mementos)
            if (!Current.owned_mementos.Contains(id)) yield return id;
        foreach (string id in Current.lv2_unlocked)
            if (!IsLv2(id)) yield return "lv2:" + id;
    }

    /// <summary>Something unlocked but not bought, that the player hasn't seen in the shop yet.</summary>
    public static bool HasNewInShop()
    {
        if (Current == null) return false;
        string seen = "";
        try { seen = "," + PlayerPrefs.GetString(SeenKey(), "") + ","; } catch { }
        foreach (string id in ShopNews())
            if (!seen.Contains("," + id + ",")) return true;
        return false;
    }

    public static void MarkShopSeen()
    {
        if (Current == null) return;
        try { PlayerPrefs.SetString(SeenKey(), string.Join(",", ShopNews())); PlayerPrefs.Save(); } catch { }
        Changed?.Invoke();
    }

    public static int Price(string id)
    {
        if (Current?.shop != null)
            foreach (var item in Current.shop) if (item.id == id) return item.cost;
        return 0;
    }

    public static int Lv2Price(string id)
    {
        if (Current?.shop != null)
            foreach (var item in Current.shop) if (item.id == id) return item.lv2_cost;
        return 0;
    }

    /// <summary>Remembered skills this loop (skill ids).</summary>
    public static List<string> KeptSkills =>
        Current != null && Current.kept_skills != null ? Current.kept_skills : new List<string>();

    // ── The stall's other goods ───────────────────────────────────────────

    public class ShopGood
    {
        public string id, type, name, blurb;
    }

    public static readonly ShopGood[] Helpers =
    {
        new ShopGood { id = "reroll", type = "helper", name = "Night-pick Reroll", blurb = "Deal the night pick's cards again. Once." },
        new ShopGood { id = "luckycard", type = "helper", name = "Lucky Card", blurb = "Your next night pick offers one extra card (up to 4)." },
        new ShopGood { id = "studynotes", type = "helper", name = "Study Notes", blurb = "+3 XP to one skill you choose." },
    };

    public static readonly ShopGood[] Cosmetics =
    {
        new ShopGood { id = "outfit_elem", type = "outfit", name = "Elementary Uniform", blurb = "White polo, green shorts or skirt." },
        new ShopGood { id = "outfit_hs", type = "outfit", name = "High School Uniform", blurb = "Free once you reach High School." },
        new ShopGood { id = "outfit_college", type = "outfit", name = "College Uniform", blurb = "Free once you reach College." },
        new ShopGood { id = "outfit_fiesta", type = "outfit", name = "Fiesta Best", blurb = "Barong or Filipiniana, for the plaza." },
        new ShopGood { id = "outfit_raincoat", type = "outfit", name = "Yellow Raincoat", blurb = "Ready for any storm." },
        new ShopGood { id = "outfit_farmer", type = "outfit", name = "Camisa de Chino", blurb = "Light, loose, made for the fields." },
        new ShopGood { id = "hat_salakot", type = "hat", name = "Salakot", blurb = "Wide and woven. Shade anywhere." },
        new ShopGood { id = "hat_hardhat", type = "hat", name = "Hard Hat", blurb = "Safety first, always." },
        new ShopGood { id = "hat_banig", type = "hat", name = "Banig Hat", blurb = "Woven from a sleeping mat's pattern." },
        new ShopGood { id = "hat_sampaguita", type = "hat", name = "Sampaguita Crown", blurb = "Smells like the fiesta." },
        new ShopGood { id = "hat_cap", type = "hat", name = "Barangay Cap", blurb = "Free at the town assembly. Now yours." },
        new ShopGood { id = "trail_petals", type = "trail", name = "Petal Trail", blurb = "Bougainvillea petals where you walk." },
        new ShopGood { id = "trail_leaves", type = "trail", name = "Leaf Trail", blurb = "Little green leaves tumble behind you." },
        new ShopGood { id = "trail_sparkles", type = "trail", name = "Morning Sparkles", blurb = "Dew-bright sparkles in your steps." },
        new ShopGood { id = "trail_bubbles", type = "trail", name = "Soap Bubbles", blurb = "Laundry-day bubbles drift behind you." },
        new ShopGood { id = "pet_cat", type = "pet", name = "The Town Cat", blurb = "Muning follows you around town." },
        new ShopGood { id = "home_garden", type = "home", name = "Garden Plot", blurb = "Pechay and tomatoes by your door." },
        new ShopGood { id = "home_capiz", type = "home", name = "Capiz Windows", blurb = "Shell windows that glow in the sun." },
        new ShopGood { id = "home_hammock", type = "home", name = "Hammock", blurb = "Strung between two posts. Siesta time." },
        new ShopGood { id = "home_sarisari", type = "home", name = "Sari-sari Sign", blurb = "Your home becomes a little store." },
    };

    public static ShopGood GoodById(string id) =>
        System.Array.Find(Helpers, g => g.id == id) ?? System.Array.Find(Cosmetics, g => g.id == id);

    public static Texture2D GoodIcon(string id) => Resources.Load<Texture2D>("Shop/icon_" + id);

    public static bool HoldsHelper(string id) =>
        Current != null && Current.helpers_held != null && Current.helpers_held.Contains(id)
        && (Current.helpers_used_loop == null || !Current.helpers_used_loop.Contains(id));

    public static bool OwnsCosmetic(string id) =>
        IsStageUniform(id) ? UniformUnlocked(id)
        : Current != null && Current.cosmetics_owned != null && Current.cosmetics_owned.Contains(id);

    // ── school uniforms: free, unlocked by reaching that school in this loop ──
    // (a new loop is a new childhood: they lock again until you get there)

    public static string UniformStage(string id) =>
        id == "outfit_hs" ? "highschool" : id == "outfit_college" ? "college" : null;

    public static bool IsStageUniform(string id) => UniformStage(id) != null;

    public static bool UniformUnlocked(string id)
    {
        string stage = UniformStage(id);
        return stage != null && vc_TownFlow.IsQuestDone("in:" + stage);   // saved when you first enter that town
    }

    public static string Equipped(string slot)
    {
        LoopEquipped e = Current?.equipped;
        if (e == null) return "";
        return slot switch { "outfit" => e.outfit ?? "", "hat" => e.hat ?? "", "trail" => e.trail ?? "", "pet" => e.pet ?? "", _ => "" };
    }

    public static bool HomeAddOn(string id) =>
        Current?.equipped?.home != null && Current.equipped.home.Contains(id);

}
