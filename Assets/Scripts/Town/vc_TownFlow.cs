using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Life-stage flow for the town version of the game (Game_Scene_Town). Loads the
/// Elementary, High School and College towns one after another, additively, the same
/// way vc_FloorLoader loads floors in the corridor version. After the last town the
/// run ends through the normal End Run screen (results + prediction).
/// </summary>
public class vc_TownFlow : MonoBehaviour
{
    public const string GameSceneName = "Game_Scene_Town";
    public static readonly string[] TownScenes = { "Town_Elementary", "Town_HighSchool", "Town_College" };

    public static vc_TownFlow Instance { get; private set; }

    /// <summary>Which NPC quests have been attempted this run (pass or fail).</summary>
    private static readonly HashSet<string> Attempted = new HashSet<string>();

    public static bool IsAttempted(string npcId) => !string.IsNullOrEmpty(npcId) && Attempted.Contains(npcId);
    public static void MarkAttempted(string npcId) { if (!string.IsNullOrEmpty(npcId)) Attempted.Add(npcId); }
    public static void ResetRun() => Attempted.Clear();

    [Tooltip("Index into TownScenes to start from.")]
    [SerializeField] private int startTown;

    private string _currentTown;
    private bool _loading;

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

        ResetRun();
        LoadTown(TownScenes[Mathf.Clamp(startTown, 0, TownScenes.Length - 1)]);
    }

    public void LoadTown(string sceneName)
    {
        if (_loading) return;
        StartCoroutine(LoadTownRoutine(sceneName));
    }

    public void LoadNextTown()
    {
        int idx = System.Array.IndexOf(TownScenes, _currentTown);
        if (idx >= 0 && idx + 1 < TownScenes.Length)
        {
            LoadTown(TownScenes[idx + 1]);
            return;
        }

        // Last stage done: hand over to the normal end-of-run flow.
        vc_QuestHUD.Instance?.HideQuestInfo();
        vc_EndRunUI ui = vc_EndRunUI.Instance ?? FindFirstObjectByType<vc_EndRunUI>();
        ui?.Show();
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
    }
}
