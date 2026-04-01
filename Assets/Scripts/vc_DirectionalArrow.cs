using UnityEngine;

[DisallowMultipleComponent]
public class vc_DirectionalArrow : MonoBehaviour
{
    [SerializeField] private Transform arrowTarget;
    [SerializeField] private GameObject arrowVisual;

    private bool isActive = false;

    public void ShowArrow()
    {
        isActive = true;

        if (arrowVisual != null)
        {
            arrowVisual.SetActive(true);
        }
    }

    public void HideArrow()
    {
        isActive = false;

        if (arrowVisual != null)
        {
            arrowVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isActive || arrowTarget == null)
        {
            return;
        }

        Vector3 direction = arrowTarget.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
