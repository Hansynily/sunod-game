using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SunodGame.Cutscene.Editor
{
    /// <summary>
    /// Pulls Han's cutscene SVG layers from the art source folder (kept outside Assets/,
    /// alongside the project) into the imported project folder. Run again whenever Han
    /// adds or updates a layer - existing files are overwritten in place so GUIDs and
    /// every reference to them (Cutscene_Intro.asset, etc.) survive the update.
    /// </summary>
    public static class vc_CutsceneSync
    {
        [MenuItem("SUNOD/Sync Cutscene SVGs")]
        public static void Sync()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string sourceRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "Cutscenes"));
            string destRoot = Path.Combine(Application.dataPath, "Art", "Cutscenes");

            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"[vc_CutsceneSync] Source folder not found: {sourceRoot}");
                return;
            }

            Directory.CreateDirectory(destRoot);

            var added = new List<string>();
            var updated = new List<string>();

            foreach (string sourceDir in Directory.GetDirectories(sourceRoot, "Cutscene*_*"))
            {
                string folderName = Path.GetFileName(sourceDir);
                string destFolderName = folderName.StartsWith("Cutscene")
                    ? "Cut" + folderName.Substring("Cutscene".Length)
                    : folderName;
                string destDir = Path.Combine(destRoot, destFolderName);
                Directory.CreateDirectory(destDir);

                foreach (string file in Directory.GetFiles(sourceDir, "*.svg"))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith("_Preview.svg") || name == "DialogBox_9Slice.svg") continue;

                    string destFile = Path.Combine(destDir, name);
                    bool existed = File.Exists(destFile);
                    File.Copy(file, destFile, overwrite: true);

                    string label = destFolderName + "/" + name;
                    if (existed) updated.Add(label); else added.Add(label);
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"[vc_CutsceneSync] Synced. Added {added.Count}, updated {updated.Count}.\n" +
                      $"Added: {(added.Count > 0 ? string.Join(", ", added) : "(none)")}\n" +
                      $"Updated: {(updated.Count > 0 ? string.Join(", ", updated) : "(none)")}");
        }
    }
}
