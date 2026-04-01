using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_HintSystem : MonoBehaviour
{
    [SerializeField] private Button hintButton;
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private string[] hints;
    [SerializeField] private int currentHintIndex = 0;

    private void Awake()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }

        if (hintButton != null)
        {
            hintButton.onClick.RemoveListener(ToggleHint);
            hintButton.onClick.AddListener(ToggleHint);
        }
    }

    public void SetHints(string[] newHints)
    {
        hints = newHints;
        currentHintIndex = 0;
    }

    public void NextHint()
    {
        if (hints == null || hints.Length == 0 || hintText == null)
        {
            return;
        }

        currentHintIndex = Mathf.Min(currentHintIndex + 1, hints.Length - 1);
        hintText.text = hints[currentHintIndex];
    }

    private void ToggleHint()
    {
        if (hintPanel == null)
        {
            return;
        }

        bool isActive = hintPanel.activeSelf;
        hintPanel.SetActive(!isActive);

        if (!isActive && hintText != null && hints != null && hints.Length > 0)
        {
            hintText.text = hints[currentHintIndex];
        }
    }
}
