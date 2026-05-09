using System;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_DirectionalArrow : MonoBehaviour
{
    [SerializeField] private Transform arrowTarget;
    [SerializeField] private GameObject arrowVisual;
    // The arrow sprite points down by default, so we offset its rotation to line it up with targets.
    [SerializeField] private float rotationOffset = 90f;

    private bool isActive = false;
    private SpriteRenderer arrowSpriteRenderer;

    public static vc_DirectionalArrow Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (arrowVisual != null)
        {
            arrowSpriteRenderer = arrowVisual.GetComponent<SpriteRenderer>();
        }

        if (ShouldStartHidden())
        {
            ClearTarget();
            HideArrow();
        }
    }

    public void SetTarget(Transform target)
    {
        arrowTarget = target;
    }

    public void ClearTarget()
    {
        arrowTarget = null;
    }

    public void ShowArrow()
    {
        isActive = true;

        SetArrowVisible(true);
    }

    public void HideArrow()
    {
        isActive = false;

        SetArrowVisible(false);
    }

    private void Update()
    {
        if (!isActive || arrowTarget == null)
        {
            return;
        }

        Vector3 direction = arrowTarget.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool ShouldStartHidden()
    {
        return string.Equals(gameObject.name?.Trim(), "GoalArrow", StringComparison.OrdinalIgnoreCase);
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowSpriteRenderer != null)
        {
            arrowSpriteRenderer.enabled = visible;
            return;
        }

        if (arrowVisual != null)
        {
            arrowVisual.SetActive(visible);
        }
    }
}
