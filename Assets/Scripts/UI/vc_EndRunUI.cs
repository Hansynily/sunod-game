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
            Debug.LogWarning("[vc_EndRunUI] vc_SessionTelemetry unavailable — going to EndScene without submission.");
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
        SceneLoader.GoToEnd();
    }
}
