using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class vc_SkillZone : MonoBehaviour
{
    // Fires whenever the player steps into any skill zone. Additive - used by the
    // tutorial to coach the player; the main game ignores it.
    public static event System.Action PlayerEnteredZone;

    [SerializeField] private SpriteRenderer zoneSprite;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float idleAlphaMin = 0.25f;
    [SerializeField] private float idleAlphaMax = 0.55f;
    [SerializeField] private float activeAlpha = 0.8f;
    [SerializeField] private Color activeTint = new Color(1f, 0.9f, 0.4f);

    [Header("Glow (built at runtime from Resources/MarkerFx, fitted to the BoxCollider2D)")]
    [SerializeField] private bool glow = true;
    [SerializeField] private Color glowColor = new Color(1f, 0.82f, 0.32f);
    [SerializeField] private Color glowActiveColor = new Color(1f, 0.96f, 0.72f);
    [SerializeField] private int glowOrder = 1;

    private const float Ppu = 32f;

    private static int _activeZoneCount = 0;
    private bool _playerInside = false;
    private Color _originalColor;

    private Transform _rig;
    private SpriteRenderer _fill, _border, _aura;
    private Vector2 _size;
    private float _enterPop;
    private float _seed;

    private void Awake()
    {
        if (zoneSprite == null)
            zoneSprite = GetComponent<SpriteRenderer>();

        if (zoneSprite != null)
            _originalColor = zoneSprite.color;

        _seed = Random.value * 10f;
        if (glow) BuildGlow();
    }

    // ── Glow visuals ─────────────────────────────────────────────────────

    private void BuildGlow()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        Sprite fill = LoadSliced("MarkerFx/zone_glow", 7);
        Sprite border = LoadSliced("MarkerFx/zone_border", 5);
        Sprite aura = LoadSliced("MarkerFx/zone_aura", 6);
        if (fill == null || border == null) return;

        // Rig undoes the zone's (often non-uniform) scale so pixels stay square.
        Vector3 s = transform.lossyScale;
        _rig = new GameObject("ZoneGlow").transform;
        _rig.SetParent(transform, false);
        _rig.localPosition = box.offset;
        _rig.localScale = new Vector3(SafeInv(s.x), SafeInv(s.y), 1f);
        Vector2 size = new Vector2(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y));

        Material unlit = Resources.Load<Material>("MarkerFx/ZoneGlowUnlit");
        _size = size;
        if (aura != null) _aura = MakeSliced("Aura", aura, size + Vector2.one * (12f / Ppu), glowOrder, unlit);
        _fill = MakeSliced("Fill", fill, size, glowOrder + 1, unlit);
        _border = MakeSliced("Border", border, size, glowOrder + 2, unlit);

        // The old gold frame sprite is replaced by the glow (particles stay).
        if (zoneSprite != null) zoneSprite.enabled = false;
    }

    private SpriteRenderer MakeSliced(string name, Sprite sprite, Vector2 size, int order, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_rig, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingLayerName = zoneSprite != null ? zoneSprite.sortingLayerName : "Default";
        sr.sortingOrder = order;
        if (mat != null) sr.sharedMaterial = mat;
        sr.color = glowColor;
        return sr;
    }

    private static Sprite LoadSliced(string path, int borderPx)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Ppu,
                             0, SpriteMeshType.FullRect, new Vector4(borderPx, borderPx, borderPx, borderPx));
    }

    private static float SafeInv(float v) => Mathf.Abs(v) < 0.0001f ? 1f : 1f / v;

    private void Update()
    {
        if (_rig != null) { UpdateGlow(); return; }
        if (zoneSprite == null) return;

        if (_playerInside)
        {
            zoneSprite.color = new Color(activeTint.r, activeTint.g, activeTint.b, activeAlpha);
        }
        else
        {
            float alpha = Mathf.Lerp(idleAlphaMin, idleAlphaMax, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            zoneSprite.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, alpha);
        }
    }

    private void UpdateGlow()
    {
        // Stepped pulse (4 levels) so it reads as pixel-art animation, not tweening.
        float t = Time.time + _seed;
        float pulse = Mathf.Floor((Mathf.Sin(t * pulseSpeed * 1.4f) * 0.5f + 0.5f) * 4f) / 4f;

        _enterPop = Mathf.MoveTowards(_enterPop, 0f, Time.deltaTime * 3f);
        Color c = _playerInside ? glowActiveColor : glowColor;

        if (_fill != null)
        {
            float a = _playerInside ? 0.85f + pulse * 0.15f : 0.45f + pulse * 0.35f;
            _fill.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a + _enterPop * 0.5f));
        }
        if (_border != null)
        {
            float a = _playerInside ? 1f : 0.6f + pulse * 0.4f;
            _border.color = new Color(c.r, c.g, c.b, a);
        }
        if (_aura != null)
        {
            // aura breathes outward in whole pixels
            float grow = (12f + Mathf.Round(pulse * 4f) * 2f + (_playerInside ? 4f : 0f)) / Ppu;
            _aura.size = _size + Vector2.one * grow;
            _aura.color = new Color(c.r, c.g, c.b, _playerInside ? 1f : 0.55f + pulse * 0.45f);
        }
    }

    private void OnDisable()
    {
        if (_playerInside)
            HandleExit();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _playerInside) return;

        _playerInside = true;
        _activeZoneCount++;
        _enterPop = 1f;

        if (_activeZoneCount == 1)
            vc_SkillManager.Instance?.SetSkillsInteractable(true);

        PlayerEnteredZone?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !_playerInside) return;
        HandleExit();
    }

    private void HandleExit()
    {
        _playerInside = false;
        _activeZoneCount = Mathf.Max(0, _activeZoneCount - 1);

        if (_activeZoneCount == 0)
            vc_SkillManager.Instance?.SetSkillsInteractable(false);
    }

    public static void ResetCounter()
    {
        _activeZoneCount = 0;
    }
}
