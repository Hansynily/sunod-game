#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// TRAILER ONLY (Editor, Play mode). Delete the whole Assets/Scripts/Editor/Trailer folder when done.
/// Frame-locked capture: Time.captureFramerate = 60, and every frame the finished screen (camera +
/// UI Toolkit HUD) is copied into a 1920x1080 RenderTexture, read back from the GPU asynchronously
/// and piped straight into ffmpeg (NVENC, near-lossless). No frame files are written.
/// </summary>
public static class vc_TrailerCapture
{
    public const int W = 1920, H = 1080, Fps = 60;
    public static string Ffmpeg = @"C:\Users\HANY-DEV\tools\ffmpeg\ffmpeg-9.1-essentials_build\bin\ffmpeg.exe";
    public static string OutDir = @"F:\Users\HANY\Desktop\Thesis\SUNOD\Project\Trailer\clips";
    public static bool FlipY = false;  // the D3D screen grab already comes out top-down

    public static bool Recording { get; private set; }
    public static int Frames { get; private set; }
    public static string LastFile { get; private set; }
    public static string LastError { get; private set; }

    private static Process _proc;
    private static Stream _stdin;
    private static Thread _writer;
    private static BlockingCollection<byte[]> _queue;
    private static RenderTexture _rt;
    private static int _pending;
    private static bool _stopAsked;
    private static int _maxFrames;

    // ── game view ─────────────────────────────────────────────────────────

    /// <summary>Game view at a fixed 1920x1080 (added as a custom size if missing).</summary>
    public static string SetGameView1080()
    {
        Assembly asm = typeof(EditorWindow).Assembly;
        Type gvsType = asm.GetType("UnityEditor.GameViewSizes");
        object sizes = typeof(ScriptableSingleton<>).MakeGenericType(gvsType).GetProperty("instance").GetValue(null);
        object groupType = gvsType.GetProperty("currentGroupType").GetValue(sizes);
        object group = gvsType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type groupT = group.GetType();
        int total = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            object s = groupT.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            Type st = s.GetType();
            int w = (int)st.GetProperty("width").GetValue(s), h = (int)st.GetProperty("height").GetValue(s);
            string kind = st.GetProperty("sizeType").GetValue(s).ToString();
            if (w == W && h == H && kind == "FixedResolution") { index = i; break; }
        }
        if (index < 0)
        {
            Type sizeT = asm.GetType("UnityEditor.GameViewSize");
            Type kindT = asm.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeT, Enum.Parse(kindT, "FixedResolution"), W, H, "Trailer 1080p");
            groupT.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }
        Type gvT = asm.GetType("UnityEditor.GameView");
        EditorWindow gv = EditorWindow.GetWindow(gvT, false, null, false);
        gvT.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
           .Invoke(gv, new object[] { index, null });
        return $"index {index}, screen now {Screen.width}x{Screen.height}";
    }

    // ── recording ─────────────────────────────────────────────────────────

    /// <summary>Start recording into OutDir/name.mp4. maxSeconds stops it on its own (0 = until Stop()).</summary>
    public static string Start(string name, float maxSeconds = 0f, int qp = 12)
    {
        if (Recording) return "already recording " + LastFile;
        if (Screen.width != W || Screen.height != H) return $"screen is {Screen.width}x{Screen.height}, call SetGameView1080 first";
        Directory.CreateDirectory(OutDir);
        LastFile = Path.Combine(OutDir, name + ".mp4");
        LastError = null;
        string vf = FlipY ? "-vf vflip " : "";
        var psi = new ProcessStartInfo(Ffmpeg,
            $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {W}x{H} -r {Fps} -i - {vf}" +
            $"-c:v h264_nvenc -preset p7 -tune hq -rc constqp -qp {qp} -profile:v high444p -pix_fmt yuv444p -g 30 \"{LastFile}\"")
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        _proc = Process.Start(psi);
        _proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) LastError = e.Data; };
        _proc.BeginErrorReadLine();
        _stdin = _proc.StandardInput.BaseStream;
        _queue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 90);
        _writer = new Thread(() =>
        {
            try { foreach (byte[] f in _queue.GetConsumingEnumerable()) _stdin.Write(f, 0, f.Length); }
            catch (Exception ex) { LastError = ex.Message; }
        }) { IsBackground = true };
        _writer.Start();
        if (_rt == null) { _rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32); _rt.Create(); }
        Frames = 0; _pending = 0; _stopAsked = false;
        _maxFrames = maxSeconds > 0 ? Mathf.RoundToInt(maxSeconds * Fps) : int.MaxValue;
        Time.captureFramerate = Fps;
        Application.runInBackground = true;
        Recording = true;
        vc_OfflineSession.Run(Loop());   // a DontDestroyOnLoad runner hosts the per-frame grab
        return "recording " + LastFile;
    }

    public static void Stop() { _stopAsked = true; }

    private static IEnumerator Loop()
    {
        var eof = new WaitForEndOfFrame();
        while (!_stopAsked && Frames < _maxFrames)
        {
            yield return eof;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
            Interlocked.Increment(ref _pending);
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, req =>
            {
                Interlocked.Decrement(ref _pending);
                if (req.hasError) { LastError = "readback error"; return; }
                if (!_queue.IsAddingCompleted) _queue.Add(req.GetData<byte>().ToArray());
            });
            Frames++;
            // keep memory bounded: wait for the GPU / encoder when too far ahead
            if (_pending > 6) AsyncGPUReadback.WaitAllRequests();
        }
        AsyncGPUReadback.WaitAllRequests();
        _queue.CompleteAdding();
        _writer.Join();
        _stdin.Flush(); _stdin.Close();
        _proc.WaitForExit(60000);
        Time.captureFramerate = 0;
        Recording = false;
        Debug.Log($"[Trailer] Saved {Frames} frames ({Frames / (float)Fps:0.00}s) to {LastFile}" + (LastError != null ? " | " + LastError : ""));
    }

    public static string Status() => (Recording ? "REC " : "idle ") + Frames + " frames, queue " + (_queue != null ? _queue.Count : 0) + (LastError != null ? " | err " + LastError : "");
}
#endif
