using UnityEngine;

/// <summary>
/// The player's home in each town - the house from the dream. It starts wrecked and gets
/// rebuilt step by step as the player helps people in that town (purely visual, no
/// scoring). Stage 0 = wrecked ... last stage = fully repaired.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownHome : MonoBehaviour
{
    public SpriteRenderer baseRenderer;
    public SpriteRenderer topRenderer;
    [Tooltip("Base pieces from most damaged to fully repaired.")]
    public Sprite[] baseStages;
    [Tooltip("Top (roof) pieces, same order as baseStages.")]
    public Sprite[] topStages;
    public GameObject[] debris;   // shown while the house is still damaged

    private int _stage = -1;

    /// <summary>Front door: bottom-middle of the house sprite.</summary>
    public Vector3 DoorPosition
    {
        get
        {
            if (baseRenderer != null && baseRenderer.sprite != null)
            {
                Bounds b = baseRenderer.bounds;
                return new Vector3(b.center.x, b.min.y + 0.3f, transform.position.z);
            }
            return transform.position;
        }
    }

    /// <summary>Where the player wakes up each morning: just in front of the door.</summary>
    public Vector3 WakePosition => DoorPosition + new Vector3(0f, -1.1f, 0f);

    public void SetProgress(float progress01)
    {
        int count = Mathf.Max(1, baseStages != null ? baseStages.Length : 1);
        int stage = Mathf.Clamp(Mathf.FloorToInt(progress01 * (count - 1) + 0.0001f), 0, count - 1);
        if (stage == _stage) return;

        bool rebuilt = _stage >= 0 && stage > _stage;
        _stage = stage;

        if (baseRenderer != null && baseStages != null && stage < baseStages.Length) baseRenderer.sprite = baseStages[stage];
        if (topRenderer != null && topStages != null && stage < topStages.Length) topRenderer.sprite = topStages[stage];

        if (debris != null)
        {
            float shownFraction = 1f - (float)stage / (count - 1);
            for (int i = 0; i < debris.Length; i++)
                if (debris[i] != null) debris[i].SetActive(i < Mathf.CeilToInt(debris.Length * shownFraction));
        }

        if (rebuilt) vc_QuestHUD.Instance?.ShowFeedbackTimed("Your home is getting stronger!", 2f);
    }
}
