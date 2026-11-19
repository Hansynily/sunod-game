using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the storybook townsfolk (Tools/art/npc/townsfolk.py) on the quest givers already placed
/// in the three town scenes, without rebuilding the towns: each vc_TownNPC gets the idle frames
/// Art/Town/NPC/npc_&lt;code&gt;_f0..5 (its question code in lower case) and its body sprite.
/// Menu: SUNOD > Towns > Apply NPC Looks.
/// </summary>
public static class vc_ApplyNpcLooks
{
    private const string NpcDir = "Assets/Art/Town/NPC";
    private static readonly string[] Towns =
    {
        "Assets/Scenes/Towns/Town_Elementary.unity",
        "Assets/Scenes/Towns/Town_HighSchool.unity",
        "Assets/Scenes/Towns/Town_College.unity",
    };

    [MenuItem("SUNOD/Towns/Apply NPC Looks")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Apply());
    }

    /// <summary>Applies the looks to every town scene and saves them. Returns a report.</summary>
    public static string Apply()
    {
        string report = "";
        string start = EditorSceneManager.GetActiveScene().path;
        foreach (string path in Towns)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int done = 0, missing = 0;
            foreach (var npc in Object.FindObjectsByType<vc_TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(npc.questCode)) continue;
                string look = npc.questCode.ToLowerInvariant();
                var frames = new Sprite[6];
                bool ok = true;
                for (int f = 0; f < 6; f++)
                {
                    frames[f] = AssetDatabase.LoadAssetAtPath<Sprite>($"{NpcDir}/npc_{look}_f{f}.png");
                    if (frames[f] == null) ok = false;
                }
                if (!ok) { missing++; Debug.LogWarning($"[vc_ApplyNpcLooks] no frames for {look} ({npc.npcName})"); continue; }
                Undo.RecordObject(npc, "Apply NPC look");
                npc.idleFrames = frames;
                EditorUtility.SetDirty(npc);
                if (npc.body != null)
                {
                    Undo.RecordObject(npc.body, "Apply NPC look");
                    npc.body.sprite = frames[0];
                    EditorUtility.SetDirty(npc.body);
                }
                done++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += $"{System.IO.Path.GetFileNameWithoutExtension(path)}: {done} NPCs" + (missing > 0 ? $", {missing} missing" : "") + "\n";
        }
        if (!string.IsNullOrEmpty(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        return report.TrimEnd();
    }
}
