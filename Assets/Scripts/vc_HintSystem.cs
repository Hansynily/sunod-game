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
            hintButton.onClick.RemoveListener(HandleHintButtonPressed);
            hintButton.onClick.AddListener(HandleHintButtonPressed);
        }
    }

    public void SetHints(string[] newHints)
    {
        hints = newHints;
        currentHintIndex = 0;

        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }

        if (hintText != null)
        {
            hintText.text = string.Empty;
        }
    }

    public void NextHint()
    {
        if (hints == null || hints.Length == 0 || hintText == null)
        {
            return;
        }

        currentHintIndex = (currentHintIndex + 1) % hints.Length;
        hintText.text = hints[currentHintIndex];
    }

    private void HandleHintButtonPressed()
    {
        if (hintPanel == null || hintText == null || hints == null || hints.Length == 0)
        {
            return;
        }

        if (!hintPanel.activeSelf)
        {
            hintPanel.SetActive(true);
            hintText.text = hints[currentHintIndex];
            return;
        }

        NextHint();
    }
}
