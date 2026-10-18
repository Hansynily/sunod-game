using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using SunodGame.Core;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public InputAction MoveAction;
    Rigidbody2D rigidbody2d;
    Vector2 move;
    public float speed = 3.0f;

    /// <summary>Current move input (-1..1 per axis). Read by vc_SpriteFrameAnimator.</summary>
    public Vector2 Move => move;

    /// <summary>Cutscene walking (e.g. heading home to sleep): overrides input while set.</summary>
    public Vector2? ScriptedMove { get; set; }

    /// <summary>While true the camera stays where it is instead of following the player.</summary>
    public bool CameraHeld { get; set; }

    Transform followCamera;
    Vector3 cameraOffset;
    bool cameraDetachedForStability;

    Animator animator;
    void Start()
    {
        MoveAction.Enable();
        rigidbody2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        rigidbody2d.constraints |= RigidbodyConstraints2D.FreezeRotation;
        rigidbody2d.angularVelocity = 0f;

        // Fix screen flip
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.transform.parent == transform)
        {
            followCamera = mainCamera.transform;
            cameraOffset = followCamera.localPosition;
            followCamera.SetParent(null, true);
            cameraDetachedForStability = true;
        }
    }

    void Update()
    {
        move = ScriptedMove ?? MoveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        Vector2 position = (Vector2)rigidbody2d.position + move * speed * Time.deltaTime;
        rigidbody2d.MovePosition(position);
        // Legacy bone-rig Animator. Disabled when vc_SpriteFrameAnimator drives frame-based art.
        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.SetFloat("Move X", move.x);
            animator.SetBool("isMoving", !Mathf.Approximately(move.magnitude, 0f));
        }
    }

    void LateUpdate()
    {
        if (!cameraDetachedForStability || followCamera == null || CameraHeld) return;

        followCamera.position = transform.position + cameraOffset;
        followCamera.rotation = Quaternion.identity;

        if (vc_RoomSlot.Current == null) return;

        Bounds b = vc_RoomSlot.Current.RoomBounds;
        Camera cam = followCamera.GetComponent<Camera>();
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 pos = followCamera.position;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        pos.x = minX <= maxX ? Mathf.Clamp(pos.x, minX, maxX) : b.center.x;

        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;
        pos.y = minY <= maxY ? Mathf.Clamp(pos.y, minY, maxY) : b.center.y;

        followCamera.position = pos;
    }
}
