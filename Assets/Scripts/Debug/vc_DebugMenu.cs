#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SunodGame.Core;

/// <summary>
/// Cheat / debug menu for testing. Editor and Development builds only - it is compiled out
/// of release builds. Open it with the small DEBUG button at the top of the screen, or F1.
///
/// Quest:  force-complete or fail the running quest, add/remove time, freeze the timer.
/// Stage:  complete the whole stage, jump to the next open quest NPC, switch towns,
///         reset town progress; in the corridor version, jump to the next floor.
/// Player: give every skill, speed boost, noclip.
/// Game:   time scale, open the End Run screen, fake a full run (same as F8), close dialogs.
///
/// Drawn with IMGUI so it needs no scene setup and works in every scene.
/// </summary>
public class vc_DebugMenu : MonoBehaviour
{
    private static vc_DebugMenu _instance;

    private bool _open;
    private Vector2 _scroll;
    private float _speedMultiplier = 1f;
    private float _baseSpeed = -1f;
    private bool _noclip;
    private float _fps;
    private string _status = "";
    private float _statusUntil;

    private static readonly string[] Floors = { "Level1_Scene", "Level2_Scene", "Level3_Scene" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[vc_DebugMenu]");
        _instance = go.AddComponent<vc_DebugMenu>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) _open = !_open;
    }

    // ── drawing ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale, h = Screen.height / scale;

        GUI.skin.button.fontSize = 13;
        GUI.skin.label.fontSize = 13;
        GUI.skin.box.fontSize = 13;

        // Left edge, mid-height: clear of the quest panel, progress banner and pause button.
        if (GUI.Button(new Rect(2f, h * 0.42f, 64f, 22f), _open ? "CLOSE" : "DEBUG"))
            _open = !_open;

        if (!_open) return;

        float pw = Mathf.Min(320f, w - 20f), ph = h - 40f;
        GUILayout.BeginArea(new Rect(w / 2f - pw / 2f, 28f, pw, ph), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label($"Scene: {SceneManager.GetActiveScene().name}   FPS: {_fps:0}");
        if (Time.unscaledTime < _statusUntil) GUILayout.Label("> " + _status);

        DrawQuestSection();
        DrawStageSection();
        DrawPlayerSection();
        DrawGameSection();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void Header(string text)
    {
        GUILayout.Space(6f);
        GUILayout.Label($"<b>{text}</b>", new GUIStyle(GUI.skin.label) { richText = true });
    }

    private void DrawQuestSection()
    {
        Header("QUEST");
        vc_QuestRoom room = FindRunningQuest();
        vc_QuestTimer timer = vc_QuestTimer.Instance;
        GUILayout.Label(room != null
            ? $"Running: {room.QuestName} ({room.QuestionCode})  {(timer != null ? timer.GetTimeRemaining().ToString("0.0") + "s" : "")}"
            : "No quest running.");

        GUI.enabled = room != null;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Complete (pass)")) { room.OnQuestComplete(); Say($"Completed {room.QuestName}"); }
        if (GUILayout.Button("Fail")) { timer?.DebugForceFail(); Say("Quest failed"); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+10s")) timer?.DebugAddTime(10f);
        if (GUILayout.Button("-10s")) timer?.DebugAddTime(-10f);
        GUI.enabled = timer != null;
        if (timer != null && GUILayout.Button(timer.DebugFrozen ? "Unfreeze timer" : "Freeze timer"))
            timer.DebugFrozen = !timer.DebugFrozen;
        GUILayout.EndHorizontal();
        GUI.enabled = true;
    }

    private void DrawStageSection()
    {
        vc_TownStage town = vc_TownStage.Current;
        if (town != null)
        {
            Header($"STAGE - {town.stageTitle}  ({town.AttemptedCount}/{town.BuiltQuestCount} helped)");
            GUI.enabled = !town.InQuest;
            if (GUILayout.Button("Complete this stage (all quests)")) { town.DebugCompleteStage(); Say("Stage completed"); }

            vc_TownNPC next = town.DebugNextOpenNpc();
            if (GUILayout.Button(next != null ? $"Go to next quest: {next.npcName} ({next.questCode})" : "Go to next quest (none left)") && next != null)
            {
                town.DebugTeleportTo(next);
                _open = false;
            }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < vc_TownFlow.TownScenes.Length; i++)
            {
                string label = vc_TownFlow.TownScenes[i].Replace("Town_", "");
                if (GUILayout.Button(label) && vc_TownFlow.Instance != null)
                {
                    vc_DialogPanel.Instance?.Hide();
                    vc_TownFlow.Instance.LoadTown(vc_TownFlow.TownScenes[i]);
                    Say("Loading " + label);
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reset town progress")) { town.DebugResetProgress(); Say("Progress reset"); }
            if (GUILayout.Button($"End day {town.Day} now (summary + dream)")) { town.DebugEndDay(); _open = false; }
            if (GUILayout.Button("Play stage-end dream") && vc_TownFlow.Instance != null)
            {
                var stages = vc_TownFlow.Instance.Stages;
                int idx = Mathf.Max(0, vc_TownFlow.Instance.CurrentStageIndex);
                if (idx < stages.Length) vc_RunProgressUI.Instance?.PlayDream(stages[idx].title, stages[idx].dreamAfter, idx, null);
                _open = false;
            }
            GUI.enabled = true;
            return;
        }

        vc_FloorLoader floors = vc_FloorLoader.Instance;
        if (floors != null && floors.isActiveAndEnabled)
        {
            Header($"FLOOR - {floors.CurrentFloorScene}");
            int idx = System.Array.IndexOf(Floors, floors.CurrentFloorScene);
            GUI.enabled = idx >= 0 && idx + 1 < Floors.Length;
            if (GUILayout.Button("Next floor")) { floors.LoadFloor(Floors[idx + 1]); Say("Loading " + Floors[idx + 1]); }
            GUI.enabled = true;
        }
    }

    private void DrawPlayerSection()
    {
        Header("PLAYER");
        PlayerController player = FindFirstObjectByType<PlayerController>();
        GUI.enabled = player != null;

        if (GUILayout.Button("Give every skill")) Say($"Added {GiveAllSkills()} skills");

        GUILayout.BeginHorizontal();
        foreach (float m in new[] { 1f, 2f, 3f })
            if (GUILayout.Button(Mathf.Approximately(_speedMultiplier, m) ? $"[Speed x{m}]" : $"Speed x{m}"))
                SetSpeed(player, m);
        GUILayout.EndHorizontal();

        if (GUILayout.Button(_noclip ? "Noclip: ON (quests won't trigger)" : "Noclip: OFF"))
        {
            _noclip = !_noclip;
            foreach (Collider2D c in player.GetComponents<Collider2D>()) c.enabled = !_noclip;
        }
        GUI.enabled = true;
    }

    private void DrawGameSection()
    {
        Header("GAME");
        if (GUILayout.Button(SceneLoader.UseTownFlow ? "Run mode: TOWNS (tap for corridor)" : "Run mode: CORRIDOR (tap for towns)"))
        {
            SceneLoader.UseTownFlow = !SceneLoader.UseTownFlow;
            Say("New Game now uses the " + (SceneLoader.UseTownFlow ? "towns" : "corridor"));
        }
        GUILayout.BeginHorizontal();
        foreach (float ts in new[] { 1f, 2f, 4f })
            if (GUILayout.Button(Mathf.Approximately(Time.timeScale, ts) ? $"[Time x{ts}]" : $"Time x{ts}"))
                Time.timeScale = ts;
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Close any dialog")) vc_DialogPanel.Instance?.Hide();
        if (GUILayout.Button("Open End Run screen"))
        {
            vc_EndRunUI ui = vc_EndRunUI.Instance ?? FindFirstObjectByType<vc_EndRunUI>();
            ui?.Show();
            _open = false;
        }
        if (GUILayout.Button("Fake a full run + results (F8, needs login)")) { vc_DebugSkipToEnd.RunFromMenu(); _open = false; }
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private void Say(string msg)
    {
        _status = msg;
        _statusUntil = Time.unscaledTime + 3f;
        Debug.Log("[DebugMenu] " + msg);
    }

    private static vc_QuestRoom FindRunningQuest()
    {
        foreach (vc_QuestRoom r in FindObjectsByType<vc_QuestRoom>(FindObjectsSortMode.None))
            if (r.IsInProgress) return r;
        return null;
    }

    private void SetSpeed(PlayerController player, float multiplier)
    {
        if (player == null) return;
        if (_baseSpeed < 0f) _baseSpeed = player.speed;
        _speedMultiplier = multiplier;
        player.speed = _baseSpeed * multiplier;
    }

    private static int GiveAllSkills()
    {
        if (vc_PlayerInventory.Instance == null) return 0;
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
            ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
        vc_SkillData[] skills = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        if (skills == null) return 0;

        int added = 0;
        foreach (vc_SkillData s in skills)
        {
            if (s == null) continue;
            vc_PlayerInventory.Instance.AddSkill(s);
            added++;
        }
        return added;
    }
}
#endif
