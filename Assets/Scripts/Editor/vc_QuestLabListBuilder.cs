using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SUNOD > Debug > Build Quest Lab List: reads every quest giver in the three town scenes and
/// writes Assets/Resources/Debug/quest_lab.json, the list the admin Quest Lab shows
/// (vc_AdminDebug). Run it again after a town's quests change. Opens the towns additively and
/// closes them again without saving; nothing in the scenes changes.
/// </summary>
public static class vc_QuestLabListBuilder
{
    private static readonly (string scene, string stage)[] Towns =
    {
        ("Town_Elementary", "Elementary"), ("Town_HighSchool", "High School"), ("Town_College", "College"),
    };

    [MenuItem("SUNOD/Debug/Build Quest Lab List")]
    public static void Build() => Debug.Log("[QuestLab] " + BuildAndReport());

    public static string BuildAndReport()
    {
        var list = new QuestLabJson();
        foreach (var (scene, stage) in Towns)
        {
            string path = $"Assets/Scenes/Towns/{scene}.unity";
            Scene opened = SceneManager.GetSceneByPath(path);
            bool wasOpen = opened.IsValid() && opened.isLoaded;
            if (!wasOpen) opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (GameObject rootGo in opened.GetRootGameObjects())
                foreach (vc_TownNPC npc in rootGo.GetComponentsInChildren<vc_TownNPC>(true))
                {
                    if (npc.questPrefab == null) continue;
                    vc_QuestRoom room = npc.questPrefab.GetComponentInChildren<vc_QuestRoom>(true);
                    list.quests.Add(new vc_AdminDebug.LabQuest
                    {
                        town = scene,
                        stage = stage,
                        questId = npc.QuestId,
                        code = npc.questCode,
                        npc = npc.npcName,
                        quest = room != null && !string.IsNullOrEmpty(room.QuestName) ? room.QuestName : npc.questPrefab.name,
                    });
                }
            if (!wasOpen) EditorSceneManager.CloseScene(opened, true);
        }
        list.quests.Sort((a, b) => a.town == b.town ? string.CompareOrdinal(a.code, b.code) : 0);

        string dir = "Assets/Resources/Debug";
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "quest_lab.json"), JsonUtility.ToJson(list, true));
        AssetDatabase.Refresh();
        return $"{list.quests.Count} quests written to {dir}/quest_lab.json";
    }

    [System.Serializable]
    private class QuestLabJson { public List<vc_AdminDebug.LabQuest> quests = new List<vc_AdminDebug.LabQuest>(); }
}
