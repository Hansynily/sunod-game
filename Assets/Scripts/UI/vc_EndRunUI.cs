using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;

[DisallowMultipleComponent]
public class vc_EndRunUI : MonoBehaviour
{
    public static vc_EndRunUI Instance { get; private set; }

    private VisualElement _root;
    private Button _btn;

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
        vc_DialogPanel dialog = vc_DialogPanel.Instance
            ?? FindFirstObjectByType<vc_DialogPanel>();

        if (dialog == null)
        {
            SceneLoader.GoToEnd();
            return;
        }

        dialog.ShowChoice(
            title:        "No More Quests",
            body:         "You've completed all available quests.\nProceed to results?",
            icon:         null,
            confirmLabel: "Yes",
            cancelLabel:  "No",
            onConfirm:    () => SceneLoader.GoToEnd(),
            onCancel:     null
        );
    }
}
