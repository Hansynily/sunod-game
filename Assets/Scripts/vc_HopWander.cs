using UnityEngine;

/// <summary>
/// Ambient critter movement: idles, then hops to a random spot near where it started
/// (small arc + squash), flipping to face the hop. Purely cosmetic - quest targets
/// and colliders are not touched. Used by the rabbits in Room_Forest_WatchTheRabbits.
/// </summary>
[DisallowMultipleComponent]
public class vc_HopWander : MonoBehaviour
{
    [SerializeField] private float wanderRadius = 0.8f;
    [SerializeField] private Vector2 idleTimeRange = new Vector2(0.6f, 2.2f);
    [SerializeField] private float hopDistance = 0.35f;
    [SerializeField] private float hopDuration = 0.28f;
    [SerializeField] private float hopHeight = 0.18f;
    [SerializeField] private Vector2Int hopsPerMove = new Vector2Int(1, 3);
    [Tooltip("Which way the sprite art faces unflipped. The generated rabbit faces left.")]
    [SerializeField] private bool spriteFacesRight;

    private SpriteRenderer _sprite;
    private Vector3 _home;
    private Vector3 _baseScale;
    private Vector3 _ground;       // position without the hop arc
    private Vector3 _hopFrom, _hopTo;
    private float _timer;
    private float _hopT = -1f;     // < 0 = idling
    private int _hopsLeft;
    private Vector3 _moveTarget;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _home = transform.localPosition;
        _ground = _home;
        _timer = Random.Range(0f, idleTimeRange.y);   // desync multiple critters
        _hopT = -1f;
    }

    private void Update()
    {
        if (_hopT < 0f)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) StartMove();
            return;
        }

        _hopT += Time.deltaTime / Mathf.Max(0.01f, hopDuration);
        float t = Mathf.Clamp01(_hopT);

        _ground = Vector3.Lerp(_hopFrom, _hopTo, t);
        float arc = Mathf.Sin(t * Mathf.PI) * hopHeight;
        transform.localPosition = _ground + new Vector3(0f, arc, 0f);

        // Stretch in the air, squash on landing.
        float stretch = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
        transform.localScale = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);

        if (t < 1f) return;

        transform.localScale = _baseScale;
        if (--_hopsLeft > 0 && (_moveTarget - _ground).sqrMagnitude > 0.0004f)
            BeginHop();
        else
        {
            _hopT = -1f;
            _timer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        }
    }

    private void StartMove()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        _moveTarget = _home + new Vector3(offset.x, offset.y, 0f);
        _hopsLeft = Random.Range(hopsPerMove.x, hopsPerMove.y + 1);
        BeginHop();
    }

    private void BeginHop()
    {
        _hopFrom = _ground;
        Vector3 toTarget = _moveTarget - _ground;
        _hopTo = toTarget.magnitude <= hopDistance ? _moveTarget : _ground + toTarget.normalized * hopDistance;
        _hopT = 0f;

        if (_sprite != null && Mathf.Abs(_hopTo.x - _hopFrom.x) > 0.01f)
        {
            bool movingRight = _hopTo.x > _hopFrom.x;
            _sprite.flipX = spriteFacesRight ? !movingRight : movingRight;
        }
    }
}
