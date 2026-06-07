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
