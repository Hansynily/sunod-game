using UnityEngine;

[DisallowMultipleComponent]
public class vc_NPCController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float reachThreshold = 0.4f;

    private Transform followTarget;
    private Vector3 walkTarget;
    private bool isFollowing = false;
    private bool isWalkingToPoint = false;
    private bool reachedDestination = false;

    public void FollowTarget(Transform target)
    {
        followTarget = target;
        isFollowing = true;
        isWalkingToPoint = false;
    }

    public void WalkToPoint(Vector3 point)
    {
        walkTarget = point;
        isWalkingToPoint = true;
        isFollowing = false;
        reachedDestination = false;
    }

    public bool HasReachedDestination()
    {
        return reachedDestination;
    }

    private void Update()
    {
        if (isFollowing && followTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, followTarget.position, moveSpeed * Time.deltaTime);
            return;
        }

        if (!isWalkingToPoint || reachedDestination)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, walkTarget, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, walkTarget) < reachThreshold)
        {
            reachedDestination = true;
        }
    }
}
