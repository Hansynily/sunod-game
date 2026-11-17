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
    private int _stageCount = 1;

    private void OnEnable() => vc_LoopMeta.Changed += RefreshAddOns;
    private void OnDisable() => vc_LoopMeta.Changed -= RefreshAddOns;

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
        _stageCount = count;
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
        RefreshAddOns();
    }

    // ── Home add-ons from the stall (garden, capiz windows, hammock, sari-sari sign) ─────
    // Bought with stars, switched on in the stall's Style section, kept every loop. They show
    // once the home is at least half rebuilt (the "rebuilt home"). Purely visual, no colliders.
    // Art: Tools/art/make_cosmetics.py -> Resources/Cosmetics/home_*.png (16 px per unit).

    private readonly System.Collections.Generic.Dictionary<string, SpriteRenderer> _addOns =
        new System.Collections.Generic.Dictionary<string, SpriteRenderer>();

    private static readonly string[] AddOnIds = { "home_garden", "home_capiz", "home_hammock", "home_sarisari" };

    private void RefreshAddOns()
    {
        if (this == null || baseRenderer == null || baseRenderer.sprite == null) return;
        bool rebuiltEnough = _stage >= 0 && _stage * 2 >= _stageCount - 1;
        Bounds b = baseRenderer.bounds;
        foreach (string id in AddOnIds)
        {
            bool want = rebuiltEnough && vc_LoopMeta.HomeAddOn(id);
            _addOns.TryGetValue(id, out SpriteRenderer sr);
            if (!want) { if (sr != null) sr.gameObject.SetActive(false); continue; }
            if (sr == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Cosmetics/" + id);
                if (tex == null) continue;
                tex.filterMode = FilterMode.Point;
                var go = new GameObject("AddOn_" + id);
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 16f);
                sr.sortingLayerID = baseRenderer.sortingLayerID;
                _addOns[id] = sr;
                if (id == "home_capiz")   // a second window on the other side of the door
                {
                    var twin = new GameObject("AddOn_home_capiz_2");
                    twin.transform.SetParent(go.transform, false);
                    var tsr = twin.AddComponent<SpriteRenderer>();
                    tsr.sprite = sr.sprite;
                    tsr.sortingLayerID = sr.sortingLayerID;
                }
            }
            bool wasOff = !sr.gameObject.activeSelf;
            sr.gameObject.SetActive(true);
            Vector3 at;
            switch (id)
            {
                case "home_garden":      // front-left, on the ground
                    at = new Vector3(b.min.x + 0.4f, b.min.y - 1.3f, 0f);
                    sr.sortingOrder = baseRenderer.sortingOrder + 1;
                    break;
                case "home_hammock":     // out to the right, past the HOME signpost
                    at = new Vector3(b.max.x + 4.4f, b.min.y - 0.3f, 0f);
                    sr.sortingOrder = baseRenderer.sortingOrder + 1;
                    break;
                case "home_sarisari":    // over the door, in front of the roof's edge
                    at = new Vector3(b.center.x, b.min.y + 1.15f, 0f);
                    sr.sortingOrder = Mathf.Max(baseRenderer.sortingOrder, topRenderer != null ? topRenderer.sortingOrder : 0) + 3;
                    if (topRenderer != null) sr.sortingLayerID = topRenderer.sortingLayerID;
                    break;
                default:                 // capiz windows: either side of the door, on the wall
                    at = new Vector3(b.center.x - b.extents.x * 0.55f, b.min.y + 0.45f, 0f);
                    sr.sortingOrder = baseRenderer.sortingOrder + 2;
                    Transform twin = sr.transform.Find("AddOn_home_capiz_2");
                    if (twin != null)
                    {
                        twin.localPosition = new Vector3(Mathf.Round(b.extents.x * 1.1f * 16f) / 16f, 0f, 0f);
                        twin.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder;
                    }
                    break;
            }
            sr.transform.position = new Vector3(Mathf.Round(at.x * 16f) / 16f, Mathf.Round(at.y * 16f) / 16f, transform.position.z);
            if (wasOff) StartCoroutine(PopIn(sr.transform));
        }
    }

    private static System.Collections.IEnumerator PopIn(Transform t)
    {
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / 0.3f)
        {
            float s = k < 0.7f ? Mathf.Lerp(0.3f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
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
