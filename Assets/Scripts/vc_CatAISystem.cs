using UnityEngine;

[DisallowMultipleComponent]
public class vc_CatAISystem : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private Transform target;

    private bool isCharmed = false;

    public void StartMovingToPlayer()
    {
        isCharmed = true;
    }

    public bool HasReachedPlayer()
    {
        if (target == null)
        {
            return false;
        }

        return Vector2.Distance(transform.position, target.position) < 0.5f;
    }

    private void Update()
    {
        if (!isCharmed || target == null)
        {
            return;
        }

        transform.position = Vector2.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
    }
}
