using System;
using UnityEngine;

/// <summary>
/// Reusable NPC component that moves this NPC to a target position or Transform.
/// Subscribe to OnArrived to react when the NPC reaches the destination.
/// </summary>
[DisallowMultipleComponent]
public class vc_NPC_GoTo : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalThreshold = 0.3f;

    /// <summary>Fired once when the NPC reaches the destination.</summary>
    public event Action OnArrived;

    private Vector3 destination;
    private bool isMoving = false;

    /// <summary>Moves toward the given Transform's position.</summary>
    public void GoTo(Transform target)
    {
        if (target == null) return;
        GoTo(target.position);
    }

    /// <summary>Moves toward the given world position.</summary>
    public void GoTo(Vector3 position)
    {
        destination = position;
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving) return;

        transform.position = Vector2.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, destination) <= arrivalThreshold)
        {
            isMoving = false;
            OnArrived?.Invoke();
        }
    }
}
