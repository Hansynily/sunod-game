using UnityEngine;

/// <summary>
/// Player survey link (QR + tinyurl). Shown on the End screen after a full run, and in the
/// main menu Settings once the player has finished a run at least once. The unlock is
/// per device and never cleared (New Game / Reset / Logout keep it).
/// </summary>
public static class vc_Survey
{
    public const string Url        = "https://tinyurl.com/sunodgamesurvey";
    public const string DisplayUrl = "tinyurl.com/sunodgamesurvey";

    private const string UnlockKey = "sunod.survey_unlocked";

    public static bool IsUnlocked
    {
        get
        {
            try { return PlayerPrefs.GetInt(UnlockKey, 0) == 1; }
            catch { return false; }
        }
    }

    public static void Unlock()
    {
        try { PlayerPrefs.SetInt(UnlockKey, 1); PlayerPrefs.Save(); } catch { }
    }

    public static void Open() => Application.OpenURL(Url);
}
