using System;
using UnityEngine;
using SunodGame.Core;
using SunodGame.Models;

/// <summary>
/// Offline admin session for the College Fair booth phone: play full runs (fair or not) with
/// no server at all, logged in as an admin, so Fair mode and Debug tools work when the venue
/// wifi dies.
///
/// Device gate: only a device where an ADMIN account once logged in online can do this. That
/// login caches the username, display name and player id (PlayerPrefs, never the password or
/// the token). "Play offline as &lt;username&gt;" only exists while that cache does. Logging out
/// keeps it; Settings > "Forget offline admin" removes it.
///
/// During an offline session NOTHING goes to the server: every request in AuthManager,
/// TelemetryManager and vc_SessionTelemetry asks <see cref="Block"/> first and fails right
/// away, nothing is queued for later, and no "pending" flag is left behind, so the session can
/// never sync onto the real account. Results come from the phone (vc_FairMode.ApplyLocalResult).
/// With no offline session, every check here is false and the game behaves as before.
/// </summary>
public static class vc_OfflineSession
{
    private const string OkKey = "sunod.offline_admin_ok";
    private const string UserKey = "sunod.offline_admin.username";
    private const string NameKey = "sunod.offline_admin.name";
    private const string PlayerKey = "sunod.offline_admin.player_id";

    /// <summary>
    /// The College Fair demo login: typing these on the normal login form starts the
    /// Adventurer session with Fair mode on, without contacting the server at all
    /// (UI/LoginRegisterUIToolkit). Both case-insensitive and trimmed (phone keyboards).
    /// </summary>
    private const string DemoUsername = "hollowtide";
    private const string DemoPassword = "sunod2026";

    public static bool IsDemoLogin(string username, string password) =>
        username != null && password != null
        && string.Equals(username.Trim(), DemoUsername, StringComparison.OrdinalIgnoreCase)
        && string.Equals(password.Trim(), DemoPassword, StringComparison.OrdinalIgnoreCase);   // phone keyboards auto-capitalize / add a space

    /// <summary>This app launch is an offline admin session.</summary>
    public static bool Active { get; private set; }

    /// <summary>Requests stopped during offline sessions this launch (tests assert on it).</summary>
    public static int BlockedRequests { get; private set; }

    public static event Action Changed;

    // ── the device gate ───────────────────────────────────────────────────

    public static string CachedUsername
    {
        get { try { return PlayerPrefs.GetString(UserKey, ""); } catch { return ""; } }
    }

    public static bool HasCachedAdmin
    {
        get
        {
            try { return PlayerPrefs.GetInt(OkKey, 0) == 1 && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(UserKey, "")); }
            catch { return false; }
        }
    }

    /// <summary>An admin logged in online: remember who, for "Play offline" (no password, no token).</summary>
    public static void RememberAdmin(string username, string displayName, string playerId)
    {
        if (Active || string.IsNullOrWhiteSpace(username)) return;
        try
        {
            PlayerPrefs.SetInt(OkKey, 1);
            PlayerPrefs.SetString(UserKey, username.Trim());
            if (!string.IsNullOrWhiteSpace(displayName)) PlayerPrefs.SetString(NameKey, displayName.Trim());
            if (!string.IsNullOrWhiteSpace(playerId)) PlayerPrefs.SetString(PlayerKey, playerId.Trim());
            PlayerPrefs.Save();
        }
        catch { }
        Changed?.Invoke();
    }

    public static void Forget()
    {
        try
        {
            PlayerPrefs.DeleteKey(OkKey); PlayerPrefs.DeleteKey(UserKey);
            PlayerPrefs.DeleteKey(NameKey); PlayerPrefs.DeleteKey(PlayerKey);
            PlayerPrefs.Save();
        }
        catch { }
        Changed?.Invoke();
    }

    // ── the session ───────────────────────────────────────────────────────

    /// <summary>"Play offline as ...": a local admin session. False when the device has no cached admin.</summary>
    public static bool Start() => Start(false);

    /// <summary>
    /// The College Fair visitor session: the existing no-account "Adventurer" session (no username,
    /// straight to the main menu), the same one the Editor uses for testing. No real name is ever
    /// used. Admin switches (Fair mode, Debug tools) work because there is no account
    /// (vc_AdminDebug.CanUse). Nothing is sent to the server while it's active.
    /// </summary>
    /// <param name="fromDevPanel">The hidden DEV panel (5 taps on the logo) can always start it; the
    /// login-screen button only appears on a device where an admin once logged in online.</param>
    public static bool Start(bool fromDevPanel)
    {
        if ((!HasCachedAdmin && !fromDevPanel) || SessionState.Instance == null) return false;
        SessionState.Instance.ClearUser();   // no account: the Adventurer session
        Active = true;
        vc_LoopMeta.Set(FreshLoop());
        Debug.Log("[Offline] Adventurer session (College Fair). Nothing will be sent to the server.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>Logout (or a fresh online login): the offline session is over.</summary>
    public static void End()
    {
        if (!Active) return;
        Active = false;
        vc_LoopMeta.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Every network request asks this first. True = offline session: don't send it (the caller
    /// fails it right away with "Offline." and queues nothing).
    /// </summary>
    public static bool Block(string what)
    {
        if (!Active) return false;
        BlockedRequests++;
        Debug.Log($"[Offline] Not sent (offline session): {what}");
        return true;
    }

    /// <summary>A blank loop-1 record for offline play (the device's cached loop record belongs to the real account).</summary>
    public static LoopMetaResponse FreshLoop()
    {
        string[] threats = { "typhoon", "flood", "earthquake", "fire" };
        return new LoopMetaResponse
        {
            current_loop = 1,
            loops_completed = 0,
            current_finished = false,
            threat = threats[UnityEngine.Random.Range(0, threats.Length)],
            memory_slots = 3,
            pockets = 2,
            max_pockets = 4,
        };
    }

    /// <summary>The run ended offline: mark the loop finished locally so Go Back can be shown (never saved).</summary>
    public static void FinishLoopLocally()
    {
        if (!Active) return;
        LoopMetaResponse meta = vc_LoopMeta.Current ?? FreshLoop();
        meta.current_finished = true;
        meta.loops_completed = Mathf.Max(meta.loops_completed, meta.current_loop);
        meta.last_owned_skills.Clear();
        if (vc_PlayerInventory.Instance != null)
            foreach (vc_SkillData s in vc_PlayerInventory.Instance.GatheredSkills)
                if (s != null) meta.last_owned_skills.Add(s.skillName);
        meta.last_skill_xp = vc_SkillLevels.ToEntries();
        vc_LoopMeta.Set(meta);
    }

    /// <summary>Runs a coroutine on a throwaway object that survives scene loads (static callers).</summary>
    public static void Run(System.Collections.IEnumerator routine)
    {
        var go = new GameObject("[Offline Runner]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<vc_OfflineRunner>().Begin(routine);
    }
}
