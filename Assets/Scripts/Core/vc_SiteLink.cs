using UnityEngine;
using SunodGame.Telemetry;

/// <summary>
/// Link to the SUNOD server's website (the server the game is talking to). Shown on the End
/// screen and in the main menu Settings. Replaces the old survey link + QR code.
/// </summary>
public static class vc_SiteLink
{
    private const string Fallback = "https://sunod-server.onrender.com";

    public static string Url
    {
        get
        {
            TelemetryManager tm = TelemetryManager.Instance;
            string url = tm != null && !string.IsNullOrWhiteSpace(tm.BaseUrl) ? tm.BaseUrl : Fallback;
            return url.TrimEnd('/');
        }
    }

    /// <summary>The address without "https://", for the button label.</summary>
    public static string DisplayUrl
    {
        get
        {
            string url = Url;
            int scheme = url.IndexOf("://", System.StringComparison.Ordinal);
            return scheme >= 0 ? url.Substring(scheme + 3) : url;
        }
    }

    public static void Open() => Application.OpenURL(Url);
}
