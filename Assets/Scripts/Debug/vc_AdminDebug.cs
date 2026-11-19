using System;
using System.Collections.Generic;
using UnityEngine;
using SunodGame.Core;

/// <summary>
/// Debug tools for admin accounts and no-account sessions, in every build (unlike vc_DebugMenu, which is editor /
/// development only). Switched on in the main menu Settings; never saved, so it starts off
/// on every launch and every login (it is tied to the login's access token).
///
/// With it on:
///  - Quest Lab (main menu): pick any of the 48 quests and play it right away. Lab plays are a
///    sandbox: nothing is saved or sent (run save, quest records, run results, stall), so the
///    admin's own run and the research data stay clean,
///  - a Restart button next to "Back to town" when a quest ends (passed or failed),
///  - a small strip in quests: Pass, Fail, Restart, Freeze timer.
/// </summary>
public static class vc_AdminDebug
{
    private static bool _on;
    private static string _token;

    private static SessionState Session => SessionState.Instance;
    /// <summary>Who is playing right now (account + login); changes on every login or logout.</summary>
    private static string Token => Session == null ? "|" : (Session.Username ?? "") + "|" + (Session.AccessToken ?? "");

    /// <summary>An admin account, or a no-account ("Adventurer") session used for testing.</summary>
    public static bool CanUse => Session == null || !Session.IsLoggedIn || Session.IsAdmin;

    /// <summary>The Settings switch. Off by default, and off again after any login or logout.</summary>
    public static bool Enabled
    {
        get => _on && CanUse && _token != null && _token == Token;
        set
        {
            _on = value && CanUse;
            _token = _on ? Token : null;
            if (!_on) EndLab();
            Changed?.Invoke();
        }
    }

    public static event Action Changed;

    // ── Quest Lab ─────────────────────────────────────────────────────────

    [Serializable]
    public class LabQuest
    {
        public string town;       // scene, e.g. Town_Elementary
        public string stage;      // Elementary / High School / College
        public string questId;
        public string code;       // question code, e.g. R6
        public string npc;
        public string quest;      // quest name
    }

    [Serializable]
    private class LabList { public List<LabQuest> quests = new List<LabQuest>(); }

    /// <summary>All quests, from Resources/Debug/quest_lab.json (SUNOD > Debug > Build Quest Lab List).</summary>
    public static List<LabQuest> AllQuests()
    {
        TextAsset ta = Resources.Load<TextAsset>("Debug/quest_lab");
        if (ta == null) return new List<LabQuest>();
        try { return JsonUtility.FromJson<LabList>(ta.text)?.quests ?? new List<LabQuest>(); }
        catch { return new List<LabQuest>(); }
    }

    /// <summary>A lab play is running: nothing is saved or sent.</summary>
    public static bool LabActive { get; private set; }
    public static LabQuest LabTarget { get; private set; }

    // lab options (kept for the app session)
    public static bool LabAllSkills;
    public static int LabSkillLevel = 1;     // 1-3: every owned skill starts at this level
    public static bool LabFreezeTimer;

    /// <summary>Opens the quest's town as a sandbox and starts the quest (vc_TownStage.LabStart).</summary>
    public static void StartLab(LabQuest q)
    {
        if (!Enabled || q == null || string.IsNullOrEmpty(q.town)) return;
        ClearRunLeftovers();
        // the inventory and skill buttons survive scene loads: start clean, like a new run,
        // so the starting kit lands in the skill buttons again
        SceneLoader.ResetRunState();
        LabActive = true;
        LabTarget = q;
        vc_SessionTelemetry.Instance?.StartSession();
        vc_SaveManager.Save(new vc_SaveManager.SaveData { riasecScores = new int[6] });
        vc_TownFlow.PendingStartTown = q.town;
        vc_TownFlow.PendingCompletedQuestIds = new List<string>();
        SceneLoader.LoadByName(SceneLoader.SCENE_GAME_TOWN);
    }

    /// <summary>Back at the main menu (or the switch turned off): the lab's leftovers go away,
    /// so Continue loads the real run from the server.</summary>
    public static void EndLab()
    {
        if (!LabActive) return;
        LabActive = false;
        LabTarget = null;
        ClearRunLeftovers();
    }

    private static void ClearRunLeftovers()
    {
        vc_SessionTelemetry.Instance?.CancelCurrentSession();
        vc_SaveManager.DeleteSave();
        vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
        vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
        vc_SkillLevels.PendingRestore = null;
        vc_SkillLevels.Clear();
        vc_TownFlow.PendingCompletedQuestIds = null;
    }

    /// <summary>XP for a lab level: Lv1 0, Lv2 15, Lv3 40 (vc_SkillLevels).</summary>
    public static int LabXp => LabSkillLevel >= 3 ? vc_SkillLevels.Lv3Xp : LabSkillLevel == 2 ? vc_SkillLevels.Lv2Xp : 0;
}
