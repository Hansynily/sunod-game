using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders a quest room prefab to a PNG (as it looks in game, after vc_RoomDressing), for
/// reviewing room art without playing. Editor only; touches no assets.
/// vc_RoomSnapshot.Render("Room_Forest_WatchTheRabbits", "C:/tmp/rabbits.png")
/// </summary>
public static class vc_RoomSnapshot
{
    public static string Render(string prefabName, string outPath, int pxPerUnit = 32)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Rooms/{prefabName}.prefab");
        if (prefab == null) return "no prefab " + prefabName;
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            room.name = prefabName;
            vc_RoomDressing.Dress(room);
            Bounds b = new Bounds(room.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.bounds.size.x > 60f || r.bounds.size.y > 60f) continue;   // backdrops
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var camGo = new GameObject("SnapCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = b.extents.y + 0.5f;
            cam.transform.position = new Vector3(b.center.x, b.center.y, -20f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(28, 23, 38, 255);
            int h = Mathf.Clamp(Mathf.RoundToInt(cam.orthographicSize * 2f * pxPerUnit), 64, 2048);
            int w = Mathf.Clamp(Mathf.RoundToInt((b.extents.x + 0.5f) * 2f * pxPerUnit), 64, 2048);
            cam.aspect = (float)w / h;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            return $"{prefabName} -> {outPath} ({w}x{h})";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
