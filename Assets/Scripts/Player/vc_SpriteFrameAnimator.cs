using System;
using UnityEngine;
using SunodGame.Core;

/// <summary>
/// Stardew-style frame-based character animation: swaps whole PNG frames on a
/// SpriteRenderer at a fixed frame rate - no bones, no Animator. Picks walk/idle and
/// facing (right/left/up/down) from PlayerController.Move; the dominant axis wins.
/// Walk speed scales with joystick tilt so a light push steps slower.
/// Two frame sets: the boy (fields below) and the girl, switched by vc_PlayerOptions.UseGirlAvatar.
/// </summary>
[DisallowMultipleComponent]
public class vc_SpriteFrameAnimator : MonoBehaviour
{
    [Serializable]
    public class FrameSet
    {
        public Sprite[] idleRight, idleLeft, idleUp, idleDown;
        public Sprite[] walkRight, walkLeft, walkUp, walkDown;
    }

    private enum Facing { Right, Left, Up, Down }

    [SerializeField] private PlayerController player;
    [SerializeField] private SpriteRenderer body;

    [Header("Boy frames (PNG sprites, in play order)")]
    [SerializeField] private Sprite[] idleRight;
    [SerializeField] private Sprite[] idleLeft;
    [SerializeField] private Sprite[] walkRight;
    [SerializeField] private Sprite[] walkLeft;
    [SerializeField] private Sprite[] idleUp;
    [SerializeField] private Sprite[] idleDown;
    [SerializeField] private Sprite[] walkUp;
    [SerializeField] private Sprite[] walkDown;

    [Header("Girl frames")]
    [SerializeField] private FrameSet girl = new FrameSet();

    [Header("Timing")]
    [SerializeField] private float walkFps = 9f;
    [SerializeField] private float idleFps = 4f;
    [Tooltip("Walk frames run at least this fraction of walkFps at tiny tilts.")]
    [Range(0.1f, 1f)] [SerializeField] private float minWalkRate = 0.5f;
    [SerializeField] private float moveThreshold = 0.05f;
    [Tooltip("Vertical input must beat horizontal by this factor to face up/down (stops flicker on diagonals).")]
    [SerializeField] private float verticalBias = 1.15f;

    [Header("Footsteps")]
    [Tooltip("Walk frame indices where a foot lands (one footstep sound each).")]
    [SerializeField] private int[] footstepFrames = { 1, 4 };
    [SerializeField] private float footstepVolume = 0.55f;

    private Facing _facing = Facing.Down;
    private Facing _lastSide = Facing.Right;
    private bool _walking;
    private float _frameClock;
    private int _frame;
    private bool _girl;

    private void Reset()
    {
        player = GetComponentInParent<PlayerController>();
        body = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (body == null) body = GetComponent<SpriteRenderer>();
        _girl = vc_PlayerOptions.UseGirlAvatar;
    }

    private void OnEnable()  => vc_PlayerOptions.Changed += OnOptionsChanged;
    private void OnDisable() => vc_PlayerOptions.Changed -= OnOptionsChanged;

    private void OnOptionsChanged() => _girl = vc_PlayerOptions.UseGirlAvatar;

    private void Update()
    {
        Vector2 move = player != null ? player.Move : Vector2.zero;
        float mag = Mathf.Clamp01(move.magnitude);
        bool walking = mag > moveThreshold;

        if (walking)
        {
            float ax = Mathf.Abs(move.x), ay = Mathf.Abs(move.y);
            if (ay > ax * verticalBias) _facing = move.y > 0f ? Facing.Up : Facing.Down;
            else if (ax > moveThreshold)
            {
                _facing = move.x > 0f ? Facing.Right : Facing.Left;
                _lastSide = _facing;
            }
        }

        if (walking != _walking)
        {
            // Restart the cycle on state change so a step always begins on frame 0.
            _walking = walking;
            _frame = 0;
            _frameClock = 0f;
        }

        Sprite[] frames = Pick(walking);
        if (frames == null || frames.Length == 0 || body == null) return;

        float fps = walking ? walkFps * Mathf.Lerp(minWalkRate, 1f, mag) : idleFps;
        _frameClock += Time.deltaTime * fps;
        while (_frameClock >= 1f)
        {
            _frameClock -= 1f;
            _frame++;
            if (walking && IsFootstepFrame(_frame % frames.Length))
                vc_Sfx.PlayRandom("footstep", 4, footstepVolume, 0.08f);
        }

        body.sprite = frames[_frame % frames.Length];
    }

    /// <summary>Frames for the current avatar + facing, falling back to the side frames
    /// when a set has no up/down art.</summary>
    private Sprite[] Pick(bool walking)
    {
        FrameSet set = _girl && HasFrames(girl.idleRight) ? girl : null;

        Sprite[] r  = walking ? (set != null ? set.walkRight : walkRight) : (set != null ? set.idleRight : idleRight);
        Sprite[] l  = walking ? (set != null ? set.walkLeft  : walkLeft)  : (set != null ? set.idleLeft  : idleLeft);
        Sprite[] up = walking ? (set != null ? set.walkUp    : walkUp)    : (set != null ? set.idleUp    : idleUp);
        Sprite[] dn = walking ? (set != null ? set.walkDown  : walkDown)  : (set != null ? set.idleDown  : idleDown);
        Sprite[] side = _lastSide == Facing.Right ? r : l;

        switch (_facing)
        {
            case Facing.Up:   return HasFrames(up) ? up : side;
            case Facing.Down: return HasFrames(dn) ? dn : side;
            case Facing.Left: return l;
            default:          return r;
        }
    }

    private static bool HasFrames(Sprite[] s) => s != null && s.Length > 0;

    private bool IsFootstepFrame(int index)
    {
        if (footstepFrames == null) return false;
        for (int i = 0; i < footstepFrames.Length; i++)
            if (footstepFrames[i] == index) return true;
        return false;
    }
}
