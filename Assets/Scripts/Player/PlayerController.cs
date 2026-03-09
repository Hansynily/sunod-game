using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public InputAction MoveAction;
    Rigidbody2D rigidbody2d;
    Vector2 move;
    public float speed = 3.0f;
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
        move = MoveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        Vector2 position = (Vector2)rigidbody2d.position + move * speed * Time.deltaTime;
        rigidbody2d.MovePosition(position);
        animator.SetFloat("Move X", move.x);
        if (Mathf.Approximately(move.magnitude, 0f))
            animator.SetBool("isMoving", false);
        else
            animator.SetBool("isMoving", true);
    }

    void LateUpdate()
    {
        if (!cameraDetachedForStability || followCamera == null) return;

        followCamera.position = transform.position + cameraOffset;
        followCamera.rotation = Quaternion.identity;
    }
}
