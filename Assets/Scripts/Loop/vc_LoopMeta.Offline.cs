using System;
using System.Collections.Generic;
using UnityEngine;
using SunodGame.Models;

/// <summary>
/// The stall works offline. Buying, using a helper and wearing a cosmetic happen right away
/// on this copy of the loop record, and each one is saved as a marker in the run's progress
/// ids, the same way bonus stars are ("sb:&lt;item&gt;" buy, "su:&lt;helper&gt;" use,
/// "se:&lt;n&gt;:&lt;slot&gt;:&lt;item&gt;" wear / show). The markers go out with the run save, and the server
/// applies each one once, with the same checks (sunod-game-server app/loop_meta.py
/// apply_stall_markers). Stars the stall can spend are worked out here from the bank and this
/// run's own records, so they stay right without a connection.
/// The last loop record is cached on the device (per account) for a game started offline.
/// </summary>
public static partial class vc_LoopMeta
{
    public const string BuyPrefix = "sb:", UsePrefix = "su:", EquipPrefix = "se:";

    // ── cache (for a game started without a connection) ───────────────────

    private static string CacheKey()
    {
        var s = SunodGame.Core.SessionState.Instance;
        return "sunod.loop_cache." + (s != null && !string.IsNullOrWhiteSpace(s.Username) ? s.Username : "guest");
    }

    private static void SaveCache(LoopMetaResponse meta)
    {
        if (meta == null) return;
        try { PlayerPrefs.SetString(CacheKey(), JsonUtility.ToJson(meta)); PlayerPrefs.Save(); } catch { }
    }

    private static void LoadCacheIfEmpty()
    {
        if (Current != null) return;
        try
        {
            string json = PlayerPrefs.GetString(CacheKey(), "");
            if (string.IsNullOrEmpty(json)) return;
            Current = JsonUtility.FromJson<LoopMetaResponse>(json);
            ApplyLocalMarkers();
            Changed?.Invoke();
        }
        catch { }
    }

    // ── this run's stall markers ──────────────────────────────────────────

    /// <summary>Markers only belong to the run being played (a town is open).</summary>
    private static bool InRun => vc_TownStage.Current != null;

    private static IEnumerable<string> RunIds()
    {
        if (!InRun) yield break;
        foreach (string id in vc_TownFlow.CompletedIds) yield return id;
    }

    private static bool HasMarker(string marker)
    {
        foreach (string id in RunIds()) if (id == marker) return true;
        return false;
    }

    private static void SaveMarker(string marker)
    {
        vc_TownFlow.MarkQuestDone(marker);
        string scene = vc_TownStage.Current != null ? vc_TownStage.Current.gameObject.scene.name : null;
        vc_QuestRoom.SaveProgressIds(new[] { marker }, scene);
    }

    /// <summary>Lays this run's stall markers over a fresh loop record (idempotent).</summary>
    private static void ApplyLocalMarkers()
    {
        if (Current == null || !InRun) return;
        foreach (string m in RunIds())
        {
            if (m.StartsWith(BuyPrefix)) ApplyBuy(m.Substring(BuyPrefix.Length));
            else if (m.StartsWith(UsePrefix)) ApplyUse(m.Substring(UsePrefix.Length));
            else if (m.StartsWith(EquipPrefix))
            {
                string[] parts = m.Split(new[] { ':' }, 4);
                if (parts.Length == 4) ApplyEquip(parts[2], parts[3]);
            }
        }
    }

    // ── stars ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stars the stall can spend right now: the bank, plus this run's stars (quest stars, bonus
    /// stars, the Coin Purse), minus what the stall took this run. Same sum as the server's
    /// available_stars. Outside a town it's the server's own figure.
    /// </summary>
    public static int Available
    {
        get
        {
            if (Current == null) return 0;
            if (!InRun || Current.current_finished) return Current.stars;
            int run = 0;
            var passed = new HashSet<string>();
            var records = vc_SessionTelemetry.Instance != null ? vc_SessionTelemetry.Instance.BuildRunStateRecords() : new List<RunStateQuestRecordDto>();
            foreach (RunStateQuestRecordDto r in records)
            {
                if (r == null || !r.completed) continue;
                int s5 = r.stars5 >= 0 ? r.stars5 : Mathf.RoundToInt(r.stars * 5f / 3f);
                run += Mathf.Max(0, s5);
                if (!string.IsNullOrEmpty(r.quest_id)) passed.Add(r.quest_id);
            }
            run += vc_BonusStars.Total;
            run += passed.Count * Level("coinpurse");
            int spent = 0;
            foreach (string m in RunIds()) spent += MarkerCost(m);
            return Current.bank_stars + run - spent;
        }
    }

    /// <summary>Debug (vc_DebugMenu): the stall shows exactly this many stars. Only this device's
    /// copy changes; the server's bank is left alone, so a refresh from the server undoes it.</summary>
    public static void DebugSetStars(int stars)
    {
        if (Current == null) Set(new LoopMetaResponse());
        Current.bank_stars += Mathf.Max(0, stars) - Available;
        Current.stars = Mathf.Max(0, stars);
        Changed?.Invoke();
    }

    private static int MarkerCost(string marker)
    {
        if (!marker.StartsWith(BuyPrefix)) return 0;
        string item = marker.Substring(BuyPrefix.Length);
        if (item.StartsWith("lv2:")) return Lv2Price(item.Substring(4));
        return Price(item);
    }

    // ── buy ───────────────────────────────────────────────────────────────

    /// <summary>The plaza board's Requests tab and the Journal are a stall upgrade.</summary>
    public static bool BoardUnlocked => Current != null && Current.board_unlocked;

    /// <summary>The stall id for the next pocket ("pocket3" / "pocket4"), or null when all are bought.</summary>
    public static string NextPocketId => Pockets >= 4 ? null : "pocket" + (Pockets + 1);

    /// <summary>Why this can't be bought now, or null if it can.</summary>
    public static string CantBuy(string item)
    {
        if (Current == null) return "Not ready yet.";
        if (!InRun) return "Open the stall in town.";
        if (vc_AdminDebug.LabActive) return "Closed in the Quest Lab.";
        int cost = item.StartsWith("lv2:") ? Lv2Price(item.Substring(4)) : Price(item);
        if (KeepsakeById(item) is Keepsake k)
        {
            if (k.fromEvent) return "Not for sale.";
            if (Owns(item)) return "Owned.";
            if (!IsUnlocked(item)) return "Locked.";
        }
        else if (item.StartsWith("lv2:"))
        {
            string m = item.Substring(4);
            if (IsLv2(m)) return "Owned.";
            if (!Owns(m) || !Lv2Unlocked(m)) return "Locked.";
        }
        else if (item.StartsWith("pocket"))
        {
            if (item != NextPocketId) return "Get the other pocket first.";
        }
        else if (item == "board")
        {
            if (BoardUnlocked) return "Owned.";
        }
        else if (Array.Exists(Helpers, g => g.id == item))
        {
            if (Current.helpers_held.Contains(item)) return "You already have one.";
            if (Current.helpers_bought_loop.Contains(item)) return "One per loop.";
        }
        else if (Array.Exists(Cosmetics, g => g.id == item))
        {
            if (OwnsCosmetic(item)) return "Owned.";
        }
        else return "Unknown item.";
        if (cost <= 0) return "Unknown item.";
        if (Available < cost) return "Not enough stars.";
        return null;
    }

    /// <summary>Buys at the stall, offline: applied now, saved with the run. Null = bought.</summary>
    public static string Buy(string item)
    {
        string why = CantBuy(item);
        if (why != null) return why;
        string marker = BuyPrefix + item;
        if (HasMarker(marker)) return "Already bought.";
        SaveMarker(marker);
        ApplyBuy(item);
        Changed?.Invoke();
        return null;
    }

    private static void ApplyBuy(string item)
    {
        if (Current == null) return;
        if (KeepsakeById(item) != null)
        {
            if (!Current.owned_mementos.Contains(item)) Current.owned_mementos.Add(item);
            if (!Current.unlocked_mementos.Contains(item)) Current.unlocked_mementos.Add(item);
            FillPocket(item);
        }
        else if (item.StartsWith("lv2:"))
        {
            string m = item.Substring(4);
            if (!Current.lv2_owned.Contains(m)) Current.lv2_owned.Add(m);
        }
        else if (item.StartsWith("pocket") && int.TryParse(item.Substring(6), out int n))
        {
            if (Current.pockets < n)
            {
                Current.pockets = n;
                // a new pocket holds a keepsake that was left at home
                foreach (string m in Current.owned_mementos)
                    if (!Current.carried_mementos.Contains(m)) { FillPocket(m); break; }
            }
        }
        else if (item == "board")
        {
            Current.board_unlocked = true;
        }
        else if (Array.Exists(Helpers, g => g.id == item))
        {
            if (!Current.helpers_bought_loop.Contains(item)) Current.helpers_bought_loop.Add(item);
            if (!Current.helpers_held.Contains(item) && !Current.helpers_used_loop.Contains(item)) Current.helpers_held.Add(item);
        }
        else if (!Current.cosmetics_owned.Contains(item)) Current.cosmetics_owned.Add(item);
    }

    private static void FillPocket(string id)
    {
        if (Current.carried_mementos.Contains(id) || Current.carried_mementos.Count >= Pockets) return;
        Current.carried_mementos.Add(id);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    /// <summary>Uses a one-use helper, offline (recorded on the loop through the run save).</summary>
    public static void UseHelper(string id, Action<bool> done)
    {
        if (!HoldsHelper(id) || !InRun) { done?.Invoke(false); return; }
        SaveMarker(UsePrefix + id);
        ApplyUse(id);
        Changed?.Invoke();
        done?.Invoke(true);
    }

    private static void ApplyUse(string id)
    {
        if (Current == null) return;
        Current.helpers_held.Remove(id);
        if (!Current.helpers_used_loop.Contains(id)) Current.helpers_used_loop.Add(id);
    }

    // ── wearing / showing ─────────────────────────────────────────────────

    /// <summary>Wears an owned cosmetic (empty = take it off); a home add-on toggles. Offline.</summary>
    public static bool Equip(string slot, string item)
    {
        if (Current == null || !InRun) return false;
        item = item ?? "";
        if (!string.IsNullOrEmpty(item) && !OwnsCosmetic(item)) return false;
        // a home add-on is saved as on/off, not a toggle: re-applying the run's markers over a
        // fresh copy of the loop record must land on the same result every time
        if (slot == "home")
        {
            if (string.IsNullOrEmpty(item)) return false;
            slot = HomeAddOn(item) ? "homeoff" : "homeon";
        }
        int n = 1;
        foreach (string m in RunIds()) if (m.StartsWith(EquipPrefix)) n++;
        SaveMarker($"{EquipPrefix}{n}:{slot}:{item}");
        ApplyEquip(slot, item);
        Changed?.Invoke();
        return true;
    }

    private static void ApplyEquip(string slot, string item)
    {
        if (Current == null) return;
        if (Current.equipped == null) Current.equipped = new LoopEquipped();
        LoopEquipped e = Current.equipped;
        switch (slot)
        {
            case "outfit": e.outfit = item; break;
            case "hat": e.hat = item; break;
            case "trail": e.trail = item; break;
            case "pet": e.pet = item; break;
            case "homeon":
                if (!string.IsNullOrEmpty(item) && !e.home.Contains(item)) e.home.Add(item);
                break;
            case "homeoff":
                e.home.Remove(item);
                break;
        }
    }

    // ── unlocks the server works out from the run, mirrored here for offline play ──

    /// <summary>Friendship (Lv2): the giver was helped their preferred way, and their keepsake
    /// was owned before this loop (not bought this run).</summary>
    public static void NotePreferredSolve(string questCode)
    {
        Keepsake k = KeepsakeByCode(questCode);
        if (k == null || Current == null || !Owns(k.id) || IsLv2(k.id) || Current.lv2_unlocked.Contains(k.id)) return;
        if (HasMarker(BuyPrefix + k.id)) return;
        Current.lv2_unlocked.Add(k.id);
        Changed?.Invoke();
    }

    /// <summary>A town event joined (RIASEC letter) or a fiesta seen: the free event keepsakes.</summary>
    public static void NoteEvent(string letter, bool fiesta)
    {
        if (Current == null) return;
        if (!string.IsNullOrEmpty(letter) && !Current.events_joined.Contains(letter)) Current.events_joined.Add(letter);
        if (fiesta) Current.fiesta_seen = true;
        var give = new List<string>();
        if (Current.fiesta_seen && !Owns("banderitas")) give.Add("banderitas");
        if (Current.events_joined.Count > 0 && !Owns("shell")) give.Add("shell");
        if (Current.events_joined.Count >= 6 && !Owns("luckycoin")) give.Add("luckycoin");
        foreach (string id in give)
        {
            Current.owned_mementos.Add(id);
            FillPocket(id);
        }
        Changed?.Invoke();
    }
}
