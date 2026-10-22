using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SunodGame.Core;

/// <summary>
/// Life-stage flow for the town version of the game (Game_Scene_Town). Loads the
/// Elementary, High School and College towns one after another, additively, the same
/// way vc_FloorLoader loads floors in the corridor version. Between stages it plays a
/// short dream; after the last one it submits the run and shows the results.
///
/// Progress is keyed by quest id (vc_QuestRoom.QuestId), the same ids the run-state
/// checkpoint already stores, so Continue can restore which NPCs were helped.
/// </summary>
public class vc_TownFlow : MonoBehaviour
{
    public const string GameSceneName = "Game_Scene_Town";
    public static readonly string[] TownScenes = { "Town_Elementary", "Town_HighSchool", "Town_College" };

    [Serializable]
    public class StageInfo
    {
        public string scene;
        public string title;
        [Tooltip("Question codes of the stage's built quests, in NPC order (filled by vc_TownBuilder).")]
        public string[] codes;
        [Tooltip("Quest ids matching codes (filled by vc_TownBuilder).")]
        public string[] questIds;
        [TextArea(2, 4)] public string[] dreamAfter;
    }

    public static vc_TownFlow Instance { get; private set; }

    // ── run state (survives town scene loads; reset on a new run) ──────────
    private static readonly HashSet<string> Completed = new HashSet<string>();

    /// <summary>Set by the main menu's Continue before Game_Scene_Town loads.</summary>
    public static string PendingStartTown;
    public static List<string> PendingCompletedQuestIds;

    public static bool IsQuestDone(string questId) => !string.IsNullOrEmpty(questId) && Completed.Contains(questId);
    public static void MarkQuestDone(string questId) { if (!string.IsNullOrEmpty(questId)) Completed.Add(questId); }
    public static void ResetRun() => Completed.Clear();

    /// <summary>Fired whenever overall progress changes (quest done, town loaded).</summary>
    public static event Action ProgressChanged;
    public static void RaiseProgressChanged() => ProgressChanged?.Invoke();

    [Tooltip("Index into TownScenes to start a new run from.")]
    [SerializeField] private int startTown;
    [SerializeField] private StageInfo[] stages = new StageInfo[0];

    private string _currentTown;
    private bool _loading;

    public StageInfo[] Stages => stages;
    public int CurrentStageIndex => Array.IndexOf(TownScenes, _currentTown);
    public string CurrentTown => _currentTown;
    public bool IsLoading => _loading;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Every run in this scene is a real session: make sure telemetry is recording. Only
        // start one if none is open - StartSession() on a live session would wipe the quest
        // records Continue just restored.
        vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
        if (telemetry != null && string.IsNullOrWhiteSpace(telemetry.SessionId))
            telemetry.StartSession();

        // Skills: put the saved loadout (Continue) or the starting kit (new run) back into the
        // skill buttons. Needed because the skill manager outlives the previous run's scene.
        vc_QuestAvailabilityFilter.Instance?.ReapplyForScene();

        // Dev: a town scene was opened and played directly - use it.
        foreach (string town in TownScenes)
        {
            Scene s = SceneManager.GetSceneByName(town);
            if (s.isLoaded)
            {
                _currentTown = town;
                StartCoroutine(EnterWhenReady(s));
                return;
            }
        }

        string first = TownScenes[Mathf.Clamp(startTown, 0, TownScenes.Length - 1)];
        ResetRun();

        // Continue: restore the playthrough's progress and resume in the saved town.
        if (!string.IsNullOrEmpty(PendingStartTown) && Array.IndexOf(TownScenes, PendingStartTown) >= 0)
        {
            first = PendingStartTown;
            if (PendingCompletedQuestIds != null)
                foreach (string id in PendingCompletedQuestIds) MarkQuestDone(id);
        }
        PendingStartTown = null;
        PendingCompletedQuestIds = null;

        LoadTown(first);
    }

    public void LoadTown(string sceneName)
    {
        if (_loading) return;
        StartCoroutine(LoadTownRoutine(sceneName));
    }

    /// <summary>Called when a stage is finished: dream, then the next town (or the results).</summary>
    public void LoadNextTown()
    {
        if (_loading) return;
        int idx = CurrentStageIndex;
        StageInfo info = idx >= 0 && idx < stages.Length ? stages[idx] : null;
        string[] dream = info != null ? info.dreamAfter : null;
        bool last = idx < 0 || idx + 1 >= TownScenes.Length;

        // Teaser: one extra dream line hinting where the player's choices point so far.
        string leaning = vc_RunProgressUI.LeaningText();
        if (dream != null && dream.Length > 0 && !string.IsNullOrEmpty(leaning))
        {
            var withTeaser = new List<string>(dream) { $"Your path leans toward {leaning}..." };
            dream = withTeaser.ToArray();
        }

        vc_RunProgressUI ui = vc_RunProgressUI.Instance;
        if (ui != null && dream != null && dream.Length > 0)
            ui.PlayDream(info.title, dream, last ? 3 : idx, () => AfterDream(idx, last));
        else
            AfterDream(idx, last);
    }

    private void AfterDream(int idx, bool last)
    {
        if (!last)
        {
            LoadTown(TownScenes[idx + 1]);
            return;
        }

        // Last stage done: submit the run and show the results.
        vc_QuestHUD.Instance?.HideQuestInfo();
        vc_EndRunUI ui = vc_EndRunUI.Instance ?? FindFirstObjectByType<vc_EndRunUI>();
        if (ui != null) ui.SubmitNow();
        else SceneLoader.GoToEnd();
    }

    private IEnumerator LoadTownRoutine(string sceneName)
    {
        _loading = true;
        if (!string.IsNullOrEmpty(_currentTown) && SceneManager.GetSceneByName(_currentTown).isLoaded)
            yield return SceneManager.UnloadSceneAsync(_currentTown);

        _currentTown = sceneName;
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        yield return EnterWhenReady(SceneManager.GetSceneByName(sceneName));
        _loading = false;
    }

    private IEnumerator EnterWhenReady(Scene scene)
    {
        yield return null;   // let the town's Awake/Start run
        vc_TownStage stage = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            stage = root.GetComponentInChildren<vc_TownStage>(true);
            if (stage != null) break;
        }

        if (stage == null)
        {
            Debug.LogWarning($"[vc_TownFlow] No vc_TownStage in {scene.name}.");
            yield break;
        }

        stage.Enter();
        RaiseProgressChanged();
    }

    // ── progress queries (used by the progress bar) ───────────────────────

    public int StageQuestCount(int stage) =>
        stage >= 0 && stage < stages.Length && stages[stage].questIds != null ? stages[stage].questIds.Length : 0;

    /// <summary>Played (passed or failed). A quest refused in an old save (before "Not for me" was removed) doesn't count.</summary>
    public bool StageQuestDone(int stage, int i) =>
        stage >= 0 && stage < stages.Length && stages[stage].questIds != null && i < stages[stage].questIds.Length
        && IsQuestDone(stages[stage].questIds[i])
        && !IsQuestDone(vc_TownStage.RefusedPrefix + stages[stage].questIds[i]);

    public string StageQuestCode(int stage, int i) =>
        stage >= 0 && stage < stages.Length && stages[stage].codes != null && i < stages[stage].codes.Length
            ? stages[stage].codes[i] : "";
}
