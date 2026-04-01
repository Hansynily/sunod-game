using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_FloatingMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float displayDuration = 2f;

    private Coroutine hideRoutine;

    public TextMeshProUGUI MessageText => messageText;
    public bool IsShowing => hideRoutine != null && messageText != null && messageText.gameObject.activeSelf;

    public void Show(string message)
    {
        gameObject.SetActive(true);

        if (messageText == null)
        {
            return;
        }

        messageText.text = message;
        messageText.gameObject.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void HideNow()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        HideVisuals();
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        hideRoutine = null;
        HideVisuals();
    }

    private void HideVisuals()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }
}
