using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The current run's save, kept in memory only. The real save lives on the server
/// (player_run_state, one per account): every change is pushed there as a checkpoint
/// (vc_QuestRoom.SaveProgressIds -> TelemetryManager.PutMyRunState), and Continue always
/// loads it back from the server. Nothing is written to the device, so a run started on one
/// phone continues on another, at the same stage and day, after logging in.
/// </summary>
public static class vc_SaveManager
{
    [System.Serializable]
    public class SaveData
    {
        public string currentQuestId;
        public int[] riasecScores = new int[6]; // R, I, A, S, E, C
        public List<string> unlockedSkills = new List<string>();
        public bool tutorialComplete;

        // What gets pushed to the server on each checkpoint (Continue/Reset read it back).
        public List<string> completedQuestIds = new List<string>();
        public int totalStars;
    }

    private static SaveData _current;

    // Older builds kept a copy of the save on the device. It's no longer read; remove it so
    // a stale local copy can never be mistaken for the player's progress.
    private static string LegacySavePath => Application.persistentDataPath + "/sunod_save.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RemoveLegacyFile()
    {
        _current = null;
        try { if (File.Exists(LegacySavePath)) File.Delete(LegacySavePath); }
        catch (System.Exception e) { Debug.LogWarning($"[SaveManager] Could not remove the old local save: {e.Message}"); }
    }

    public static void Save(SaveData data) => _current = data;

    public static SaveData Load() => _current;

    public static bool HasSave() => _current != null;

    public static void DeleteSave() => _current = null;
}
