using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;

[DisallowMultipleComponent]
public class vc_EndRunUI : MonoBehaviour
{
    public static vc_EndRunUI Instance { get; private set; }

    private VisualElement _root;
    private Button _btn;
    private bool _isSubmitting;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _root = root.Q<VisualElement>("end-run-root");
        _btn  = root.Q<Button>("end-run-btn");
        _btn?.RegisterCallback<ClickEvent>(_ => OnClicked());
        _root?.SetDisplay(false);
    }

    public void Show() => _root?.SetDisplay(true);
    public void Hide() => _root?.SetDisplay(false);

    private void OnClicked()
    {
        if (_isSubmitting) return;

        vc_DialogPanel dialog = vc_DialogPanel.Instance
            ?? FindFirstObjectByType<vc_DialogPanel>();

        if (dialog == null)
        {
            StartCoroutine(SubmitAndGoToEnd());
            return;
        }

        dialog.ShowChoice(
            title:        "No More Quests",
            body:         "You've completed all available quests.\nProceed to results?",
            icon:         null,
            confirmLabel: "Yes",
            cancelLabel:  "No",
            onConfirm:    () => StartCoroutine(SubmitAndGoToEnd()),
            onCancel:     null
        );
    }

    /// <summary>
    /// Submits telemetry (run summary + prediction) then loads the EndScene.
    /// Mirrors the flow used by vc_DebugSkipToEnd so quest data is never lost.
    /// </summary>
    private IEnumerator SubmitAndGoToEnd()
    {
        _isSubmitting = true;
        Hide();

        vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
        if (telemetry == null)
        {
            Debug.LogWarning("[vc_EndRunUI] vc_SessionTelemetry unavailable. Going to EndScene without submission.");
            yield return FinalizeRun();
            SceneLoader.GoToEnd();
            yield break;
        }

        Debug.Log("[vc_EndRunUI] Submitting run summary to backend...");
        yield return telemetry.SubmitRunSummary(
            success => Debug.Log("[vc_EndRunUI] Run summary submitted successfully."),
            error   => Debug.LogWarning($"[vc_EndRunUI] Run summary failed: {error}"));

        yield return telemetry.SubmitAndPredict(
            cluster => Debug.Log($"[vc_EndRunUI] Prediction done. Cluster={cluster}"));

        Debug.Log("[vc_EndRunUI] Done. Loading EndScene.");
        yield return FinalizeRun();
        SceneLoader.GoToEnd();
    }

    /// <summary>
    /// Ends the run for real: waits (bounded) for the server to mark run_finished so the
    /// main menu's Continue is greyed out when we get back there. If the call cannot land,
    /// a local pending flag makes the menu treat the run as finished anyway and retry.
    /// </summary>
    private IEnumerator FinalizeRun()
    {
        vc_SaveManager.DeleteSave();

        var telemetryManager = SunodGame.Telemetry.TelemetryManager.Instance;
        if (telemetryManager == null)
        {
            PlayerPrefs.SetInt(SunodGame.Telemetry.TelemetryManager.RunFinishPendingPrefKey, 1);
            PlayerPrefs.Save();
            yield break;
        }

        bool done = false;
        bool succeeded = false;
        telemetryManager.FinishMyRunState(
            _ => { succeeded = true; done = true; },
            e => { Debug.LogWarning($"[vc_EndRunUI] Finish run-state failed: {e}"); done = true; });

        float waited = 0f;
        while (!done && waited < 8f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (succeeded)
        {
            PlayerPrefs.DeleteKey(SunodGame.Telemetry.TelemetryManager.RunFinishPendingPrefKey);
        }
        else
        {
            PlayerPrefs.SetInt(SunodGame.Telemetry.TelemetryManager.RunFinishPendingPrefKey, 1);
        }
        PlayerPrefs.Save();
        Debug.Log($"[vc_EndRunUI] Run finalized. Server flag landed: {succeeded}.");
    }
}
