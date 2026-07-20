using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class vc_SaveManager
{
    [System.Serializable]
    public class SaveData
    {
        public string currentQuestId;
        public int[] riasecScores = new int[6]; // R, I, A, S, E, C
        public List<string> unlockedSkills = new List<string>();
        public bool tutorialComplete;

        // Local staging buffer for the server-side run-state (Continue/Reset). This file
        // stays as a device-local cache; the server (player_run_state) is the source of
        // truth read by the main menu - these fields are what get pushed on each checkpoint.
        public List<string> completedQuestIds = new List<string>();
        public int totalStars;
    }

    private static string SavePath => Application.persistentDataPath + "/sunod_save.json";

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(SavePath, json);
    }

    public static SaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        string json = File.ReadAllText(SavePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool HasSave() => File.Exists(SavePath);

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}
