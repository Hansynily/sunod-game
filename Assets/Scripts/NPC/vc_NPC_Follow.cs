using UnityEngine;

/// <summary>
/// Reusable NPC component that makes this NPC follow the player.
/// Call Activate() / Deactivate() from quest scripts to toggle behavior.
/// </summary>
[DisallowMultipleComponent]
public class vc_NPC_Follow : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float followDistance = 1.5f;

    private bool isFollowing = false;
    private Transform playerTransform;

    /// <summary>Begins following the player. Caches the player transform on activation.</summary>
    public void Activate()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        playerTransform = player != null ? player.transform : null;
        isFollowing = true;
    }

    /// <summary>Stops following the player.</summary>
    public void Deactivate()
    {
        isFollowing = false;
    }

    private void Update()
    {
        if (!isFollowing || playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= followDistance) return;

        transform.position = Vector2.MoveTowards(
            transform.position,
            playerTransform.position,
            moveSpeed * Time.deltaTime);
    }
}
