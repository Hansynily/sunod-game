using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

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
        vc_UIAnim.Press(_btn);
        _shown = false;
        _root?.SetDisplay(false);
    }

    private bool _shown;
    private vc_UIAnim.Handle _pulse;

    /// <summary>Slides up from the bottom edge, then breathes gently until it's used.</summary>
    public void Show()
    {
        if (_root == null || _shown) return;
        _shown = true;
        vc_UIAnim.StopPulse(ref _pulse, _root);
        vc_UIAnim.Show(_root, vc_UIAnim.Enter.SlideUp, 0f, () =>
        {
            if (_shown && _pulse == null) _pulse = vc_UIAnim.Pulse(_root, 0.04f, 1.4f);
        });
    }

    public void Hide()
    {
        _shown = false;
        vc_UIAnim.StopPulse(ref _pulse, _root);
        if (_root == null) return;
        if (_root.resolvedStyle.display == DisplayStyle.None)
        {
            vc_UIAnim.StopAll(_root);
            _root.SetDisplay(false);
            return;
        }
        vc_UIAnim.Hide(_root, null, vc_UIAnim.Exit.SlideDown);
    }

    /// <summary>Submits the run and goes to the results, skipping the End Run button (town mode's final stage).</summary>
    public void SubmitNow()
    {
        if (_isSubmitting) return;
        StartCoroutine(SubmitAndGoToEnd());
    }

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
            title:        "All done!",
            body:         "No quests left.\nSee your results?",
            icon:         null,
            confirmLabel: "Results",
            cancelLabel:  "Not yet",
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
            GoToEndThroughDisaster();
            yield break;
        }

        // Offline admin session: nothing is sent; the result is worked out on the phone (any run)
        if (vc_OfflineSession.Active)
        {
            vc_SessionTelemetry.Instance?.SubmitLocalMetricsOnly();
            vc_FairMode.ApplyLocalResult();
            vc_OfflineSession.FinishLoopLocally();
            yield return FinalizeRun();
            GoToEndThroughDisaster();
            yield break;
        }

        if (vc_FairMode.ThisRun)
        {
            yield return SubmitFairRun(telemetry);
            yield return FinalizeRun();
            GoToEndThroughDisaster();
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
        GoToEndThroughDisaster();
    }

    /// <summary>
    /// The end of a loop: this loop's threat strikes (a short cutscene), then the results.
    /// Falls straight through to the results in the corridor version or with no threat.
    /// </summary>
    /// <summary>
    /// College Fair mode: the same two server calls, each with a time limit. If either fails
    /// or runs out of time, the result is worked out on the phone (vc_FairMode.ApplyLocalResult),
    /// so the visitor never sees an error screen.
    /// </summary>
    private IEnumerator SubmitFairRun(vc_SessionTelemetry telemetry)
    {
        bool summaryOk = false, summaryDone = false;
        StartCoroutine(telemetry.SubmitRunSummary(
            _ => { summaryOk = true; summaryDone = true; },
            e => { Debug.LogWarning($"[vc_EndRunUI] Fair run summary failed: {e}"); summaryDone = true; }));
        for (float t = 0f; !summaryDone && t < 12f; t += Time.unscaledDeltaTime) yield return null;

        int cluster = -1;
        bool predictDone = false;
        if (summaryOk)
        {
            StartCoroutine(telemetry.SubmitAndPredict(c => { cluster = c; predictDone = true; }));
            for (float t = 0f; !predictDone && t < 15f; t += Time.unscaledDeltaTime) yield return null;
        }

        if (!summaryOk || !predictDone || cluster < 0)
        {
            Debug.LogWarning("[vc_EndRunUI] Fair run: server result missing, using the on-phone result.");
            vc_FairMode.ApplyLocalResult();
        }
    }

    private static void GoToEndThroughDisaster()
    {
        string threat = vc_LoopMeta.Threat;
        // a fair run (and any offline run) always ends with the disaster, even when the loop record never loaded
        if (vc_FairMode.ThisRun || vc_OfflineSession.Active) threat = vc_FairMode.ThreatOrDefault(threat);
        if (SceneLoader.UseTownFlow && !string.IsNullOrEmpty(threat)
            && SunodGame.Cutscene.vc_CutscenePlayer.Play("Threat_" + threat, SceneLoader.GoToEnd))
            return;
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
        // offline admin session: no server flag and no "pending" flag for later - it must never
        // land on the real account once the connection is back
        if (vc_OfflineSession.Active) yield break;

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
