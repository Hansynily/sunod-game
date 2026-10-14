using UnityEngine;

/// <summary>
/// Stardew-style frame-based character animation: swaps whole PNG frames on a
/// SpriteRenderer at a fixed frame rate - no bones, no Animator. Picks walk/idle and
/// left/right from PlayerController.Move; vertical-only movement keeps the last facing.
/// Walk speed scales with joystick tilt so a light push steps slower.
/// </summary>
[DisallowMultipleComponent]
public class vc_SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private SpriteRenderer body;

    [Header("Frames (PNG sprites, in play order)")]
    [SerializeField] private Sprite[] idleRight;
    [SerializeField] private Sprite[] idleLeft;
    [SerializeField] private Sprite[] walkRight;
    [SerializeField] private Sprite[] walkLeft;

    [Header("Timing")]
    [SerializeField] private float walkFps = 9f;
    [SerializeField] private float idleFps = 4f;
    [Tooltip("Walk frames run at least this fraction of walkFps at tiny tilts.")]
    [Range(0.1f, 1f)] [SerializeField] private float minWalkRate = 0.5f;
    [SerializeField] private float moveThreshold = 0.05f;

    private bool _facingRight = true;
    private bool _walking;
    private float _frameClock;
    private int _frame;

    private void Reset()
    {
        player = GetComponentInParent<PlayerController>();
        body = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (body == null) body = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        Vector2 move = player != null ? player.Move : Vector2.zero;
        float mag = Mathf.Clamp01(move.magnitude);
        bool walking = mag > moveThreshold;

        if (Mathf.Abs(move.x) > moveThreshold)
            _facingRight = move.x > 0f;

        if (walking != _walking)
        {
            // Restart the cycle on state change so a step always begins on frame 0.
            _walking = walking;
            _frame = 0;
            _frameClock = 0f;
        }

        Sprite[] frames = walking
            ? (_facingRight ? walkRight : walkLeft)
            : (_facingRight ? idleRight : idleLeft);
        if (frames == null || frames.Length == 0 || body == null) return;

        float fps = walking ? walkFps * Mathf.Lerp(minWalkRate, 1f, mag) : idleFps;
        _frameClock += Time.deltaTime * fps;
        while (_frameClock >= 1f)
        {
            _frameClock -= 1f;
            _frame++;
        }

        body.sprite = frames[_frame % frames.Length];
    }
}
