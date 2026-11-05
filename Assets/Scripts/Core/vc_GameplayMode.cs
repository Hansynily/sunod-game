using UnityEngine;
using SunodGame.Core;

/// <summary>
/// "Gameplay: Old / New" (switch in the main menu Settings, for admins and no-account sessions).
///   Old = the defense build: the original quest scripts (vc_ParadeDrumQuest, vc_CatCureQuest).
///   New = the minigame versions (vc_ParadeDrumMinigame, vc_CatExamMinigame).
/// The other quests are the same in both. Signed-in non-admin accounts are always Old, whatever is
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

    /// <summary>Admins, and no-account ("Adventurer") sessions used for testing, can switch.</summary>
    public static bool CanChoose
    {
        get
        {
            var s = SessionState.Instance;
            return s == null || !s.IsLoggedIn || s.IsAdmin;
        }
    }

    /// <summary>The mode in effect: the chosen mode if allowed, else always Old.</summary>
    public static string Current => CanChoose ? Chosen : Old;

    public static bool IsNew => Current == New;
}
