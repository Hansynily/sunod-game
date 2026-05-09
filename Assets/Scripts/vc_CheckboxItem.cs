using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_CheckboxItem : MonoBehaviour
{
    [SerializeField] private Image checkboxImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Sprite spriteUnchecked;
    [SerializeField] private Sprite spriteChecked;

    public void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }

    public void SetChecked(bool isChecked)
    {
        if (checkboxImage == null) return;
        checkboxImage.sprite = isChecked ? spriteChecked : spriteUnchecked;
    }
}
