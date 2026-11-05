using UnityEngine;
using SunodGame.Core;

/// <summary>
/// "Gameplay: Old / New" (admin-only switch in the main menu Settings).
///   Old = the defense build: the original quest scripts (vc_ParadeDrumQuest, vc_CatCureQuest).
///   New = the minigame versions (vc_ParadeDrumMinigame, vc_CatExamMinigame).
/// The other quests are the same in both. Non-admin accounts are always Old, whatever is
/// saved on the device. A quest room with a minigame picks its script from this when the
/// quest starts (vc_QuestRoom), and every quest record is tagged with the mode.
/// </summary>
public static class vc_GameplayMode
{
    public const string Old = "old";
    public const string New = "new";

    private const string PrefKey = "sunod.gameplay_mode";

    /// <summary>What the admin chose on this device (ignored for non-admins).</summary>
    public static string Chosen
    {
        get { try { return PlayerPrefs.GetString(PrefKey, Old) == New ? New : Old; } catch { return Old; } }
        set { try { PlayerPrefs.SetString(PrefKey, value == New ? New : Old); PlayerPrefs.Save(); } catch { } }
    }

    public static bool CanChoose => SessionState.Instance != null && SessionState.Instance.IsAdmin;

    /// <summary>The mode in effect: the admin's choice, else always Old.</summary>
    public static string Current => CanChoose ? Chosen : Old;

    public static bool IsNew => Current == New;
}
