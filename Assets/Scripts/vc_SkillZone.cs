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

    private static int _activeZoneCount = 0;
    private bool _playerInside = false;
    private Color _originalColor;

    private void Awake()
    {
        if (zoneSprite == null)
            zoneSprite = GetComponent<SpriteRenderer>();

        if (zoneSprite != null)
            _originalColor = zoneSprite.color;
    }

    private void Update()
    {
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
