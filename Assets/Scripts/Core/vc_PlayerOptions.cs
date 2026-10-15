using System;
using UnityEngine;

/// <summary>
/// Player-facing options shared by the main menu Settings overlay and the pause menu.
/// SFX volume itself lives in vc_Sfx.Volume; this class adds the rest and one change event
/// so open screens can refresh (e.g. skill names switching language).
/// </summary>
public static class vc_PlayerOptions
{
    private const string BaybayinKey = "sunod.baybayin_names";

    /// <summary>Fired after any option changes.</summary>
    public static event Action Changed;

    /// <summary>
    /// Show the Filipino (Baybayin-era) skill names, e.g. "AYOS", instead of the English
    /// default, e.g. "REPAIR". Off by default.
    /// </summary>
    public static bool UseBaybayinNames
    {
        get
        {
            try { return PlayerPrefs.GetInt(BaybayinKey, 0) == 1; }
            catch { return false; }
        }
        set
        {
            try { PlayerPrefs.SetInt(BaybayinKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { }
            Changed?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => SunodGame.Core.vc_Sfx.Volume;
        set
        {
            SunodGame.Core.vc_Sfx.Volume = Mathf.Clamp01(value);
            Changed?.Invoke();
        }
    }

    /// <summary>The name to show for a skill, per the language option.</summary>
    public static string SkillName(vc_SkillData skill)
    {
        if (skill == null) return string.Empty;
        return skill.DisplayName;
    }
}
