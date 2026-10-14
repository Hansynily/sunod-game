using UnityEngine;

/// <summary>
/// Game-style look for skill markers (tutorial RIASEC markers + hallway skill markers).
/// Builds its visuals at runtime from Resources/MarkerFx - no scene setup beyond adding
/// the component:
///   - glowing ground ring (pulses) + soft pixel halo behind the symbol (breathes)
///   - floating RIASEC symbol that bobs gently
///   - a few pixel sparkles drifting up and fading
/// Everything is tinted with the category colour (same palette as the HUD slots).
/// Cycle mode (hallway marker, which offers a random draw) rotates through all six symbols.
/// The marker's own SpriteRenderer (old diamond) is hidden; colliders are untouched.
/// </summary>
[DisallowMultipleComponent]
public class vc_MarkerGlow : MonoBehaviour
{
    [Tooltip("R, I, A, S, E or C. Ignored in cycle mode. Empty = taken from vc_TutorialSkillMarker's skill.")]
    [SerializeField] private string riasecLetter = "";
    [Tooltip("Rotate through all six symbols/colours (hallway marker: the pick is a surprise).")]
    [SerializeField] private bool cycleAllLetters;
    [SerializeField] private float cycleSeconds = 0.9f;

    [Header("Motion (subtle)")]
    [SerializeField] private float iconHeight = 0.5f;
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobSpeed = 2.2f;
    [SerializeField] private float pulseSpeed = 2.6f;
    [SerializeField] private int sparkleCount = 4;
    [SerializeField] private float sparkleRise = 0.7f;
    [SerializeField] private float sparkleLife = 1.6f;

    [Header("Sorting")]
    [SerializeField] private int groundOrder = 1;
    [SerializeField] private int iconOrder = 5;

    private const string Letters = "RIASEC";
    private const float Ppu = 32f;

    private Transform _rig;
    private SpriteRenderer _ring, _halo, _icon;
    private SpriteRenderer[] _sparkles;
    private float[] _sparklePhase;
    private float[] _sparkleX;
    private Sprite[] _symbols;
    private int _letterIndex;
    private float _cycleClock;
    private float _seed;

    public static Color ColorFor(char letter)
    {
        switch (char.ToUpperInvariant(letter))
        {
            case 'R': return new Color32(217, 58, 74, 255);
            case 'I': return new Color32(240, 138, 36, 255);
            case 'A': return new Color32(142, 79, 214, 255);
            case 'S': return new Color32(47, 163, 90, 255);
            case 'E': return new Color32(47, 111, 214, 255);
            case 'C': return new Color32(34, 179, 196, 255);
            default:  return new Color32(255, 220, 120, 255);
        }
    }

    private void Awake()
    {
        _seed = Random.value * 10f;

        if (string.IsNullOrWhiteSpace(riasecLetter))
        {
            var tutorial = GetComponent<vc_TutorialSkillMarker>();
            if (tutorial != null && tutorial.Skill != null) riasecLetter = tutorial.Skill.riaSecLetter;
        }

        var oldDiamond = GetComponent<SpriteRenderer>();
        if (oldDiamond != null) oldDiamond.enabled = false;

        Build();
        _letterIndex = Mathf.Max(0, Letters.IndexOf(char.ToUpperInvariant(FirstChar(riasecLetter))));
        ApplyLetter(_letterIndex);
    }

    private static char FirstChar(string s) => string.IsNullOrWhiteSpace(s) ? 'R' : s.Trim()[0];

    private void Build()
    {
        // Rig undoes any non-uniform marker scale (tutorial markers are 0.5 x 1 x 0.5).
        _rig = new GameObject("MarkerFx").transform;
        _rig.SetParent(transform, false);
        Vector3 s = transform.lossyScale;
        _rig.localScale = new Vector3(SafeInv(s.x), SafeInv(s.y), 1f);

        _symbols = new Sprite[6];
        for (int i = 0; i < 6; i++) _symbols[i] = LoadSprite($"MarkerFx/sym_{Letters[i]}", 58f);

        _ring = MakeRenderer("Ring", LoadSprite("MarkerFx/ring", Ppu * 0.8f), groundOrder);
        _halo = MakeRenderer("Halo", LoadSprite("MarkerFx/halo", Ppu * 0.75f), groundOrder + 1);
        _icon = MakeRenderer("Icon", null, iconOrder);

        Sprite sparkle = LoadSprite("MarkerFx/sparkle", Ppu);
        _sparkles = new SpriteRenderer[sparkleCount];
        _sparklePhase = new float[sparkleCount];
        _sparkleX = new float[sparkleCount];
        for (int i = 0; i < sparkleCount; i++)
        {
            _sparkles[i] = MakeRenderer("Sparkle", sparkle, iconOrder + 1);
            _sparklePhase[i] = (float)i / Mathf.Max(1, sparkleCount);
            _sparkleX[i] = Random.Range(-0.28f, 0.28f);
        }
    }

    private static float SafeInv(float v) => Mathf.Abs(v) < 0.0001f ? 1f : 1f / v;

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_rig, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static Sprite LoadSprite(string path, float ppu)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
    }

    private void ApplyLetter(int index)
    {
        char letter = Letters[Mathf.Clamp(index, 0, 5)];
        Color c = ColorFor(letter);
        if (_icon != null) _icon.sprite = _symbols[Mathf.Clamp(index, 0, 5)];
        if (_ring != null) _ring.color = c;
        if (_halo != null) _halo.color = Color.Lerp(c, Color.white, 0.35f);
        if (_sparkles != null)
            foreach (var sp in _sparkles) if (sp != null) sp.color = Color.Lerp(c, Color.white, 0.5f);
    }

    private void Update()
    {
        float t = Time.time + _seed;

        if (cycleAllLetters)
        {
            _cycleClock += Time.deltaTime;
            if (_cycleClock >= cycleSeconds)
            {
                _cycleClock = 0f;
                _letterIndex = (_letterIndex + 1) % 6;
                ApplyLetter(_letterIndex);
            }
        }

        // Pulses are stepped (4 levels) so they read as pixel-art animation, not tweening.
        float pulse = Mathf.Floor((Mathf.Sin(t * pulseSpeed) * 0.5f + 0.5f) * 4f) / 4f;

        if (_ring != null)
        {
            _ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            float rs = 1f + pulse * 0.12f;
            _ring.transform.localScale = new Vector3(rs, rs, 1f);
            Color rc = _ring.color; rc.a = 0.55f + pulse * 0.4f; _ring.color = rc;
        }

        float bob = Mathf.Round(Mathf.Sin(t * bobSpeed) * bobAmplitude * Ppu) / Ppu;   // whole-pixel steps
        if (_icon != null) _icon.transform.localPosition = new Vector3(0f, iconHeight + bob, 0f);

        if (_halo != null)
        {
            _halo.transform.localPosition = new Vector3(0f, iconHeight + bob, 0f);
            float hs = 1.0f + pulse * 0.15f;
            _halo.transform.localScale = new Vector3(hs, hs, 1f);
            Color hc = _halo.color; hc.a = 0.45f + pulse * 0.35f; _halo.color = hc;
        }

        if (_sparkles == null) return;
        for (int i = 0; i < _sparkles.Length; i++)
        {
            SpriteRenderer sp = _sparkles[i];
            if (sp == null) continue;

            float life = Mathf.Repeat(Time.time / sparkleLife + _sparklePhase[i], 1f);
            if (life < Time.deltaTime / sparkleLife) _sparkleX[i] = Random.Range(-0.28f, 0.28f);

            float y = Mathf.Round(life * sparkleRise * Ppu) / Ppu;
            sp.transform.localPosition = new Vector3(_sparkleX[i], 0.05f + y, 0f);
            Color sc = sp.color;
            sc.a = Mathf.Sin(life * Mathf.PI);               // fade in, then out
            sp.color = sc;
            float ss = life < 0.5f ? 1f : 0.6f;
            sp.transform.localScale = new Vector3(ss, ss, 1f);
        }
    }
}
