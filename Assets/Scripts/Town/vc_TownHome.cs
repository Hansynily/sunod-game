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

    /// <param name="bonusStages">Extra rebuild steps (Lola's Lantern keepsake).</param>
    public void SetProgress(float progress01, int bonusStages = 0)
    {
        int count = Mathf.Max(1, baseStages != null ? baseStages.Length : 1);
        int stage = Mathf.Clamp(Mathf.FloorToInt(progress01 * (count - 1) + 0.0001f) + bonusStages, 0, count - 1);
        UpdateThreatMarks(stage, count);
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

    // ── This loop's threat, left on the ground around the wrecked home ─────
    // Puddles (flood), torn branches (typhoon), cracks and rubble (earthquake), scorch and
    // ash (fire). Pixel art from Resources/Loop/wreck_<threat>.png, 16 px per unit. Fades
    // out step by step as the home is rebuilt. Purely visual.

    private SpriteRenderer _threatMarks;
    private string _threatShown;

    private void UpdateThreatMarks(int stage, int count)
    {
        string threat = vc_LoopMeta.Threat;
        if (_threatShown != threat)
        {
            _threatShown = threat;
            if (_threatMarks != null) Destroy(_threatMarks.gameObject);
            _threatMarks = null;
            Texture2D tex = string.IsNullOrEmpty(threat) ? null : Resources.Load<Texture2D>("Loop/wreck_" + threat);
            if (tex != null && baseRenderer != null)
            {
                tex.filterMode = FilterMode.Point;
                var go = new GameObject("ThreatMarks");
                go.transform.SetParent(transform, false);
                Bounds b = baseRenderer.bounds;
                go.transform.position = new Vector3(Mathf.Round(b.center.x * 16f) / 16f, Mathf.Round((b.min.y - 0.1f) * 16f) / 16f, transform.position.z);
                _threatMarks = go.AddComponent<SpriteRenderer>();
                _threatMarks.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
                _threatMarks.sortingLayerID = baseRenderer.sortingLayerID;
                _threatMarks.sortingOrder = baseRenderer.sortingOrder - 1;
            }
        }
        if (_threatMarks == null) return;
        // full at stage 0, gone once fully rebuilt; stepped alpha (pixel style)
        float left = count > 1 ? 1f - (float)stage / (count - 1) : 0f;
        Color c = _threatMarks.color;
        c.a = Mathf.Round(left * 4f) / 4f;
        _threatMarks.color = c;
        _threatMarks.enabled = c.a > 0f;
    }
}
