using UnityEngine;

[DisallowMultipleComponent]
public class vc_XrayEffect : MonoBehaviour
{
    [SerializeField] private GameObject[] xrayTargets;
    [SerializeField] private GameObject[] revealTargets;
    [SerializeField] private BoxCollider2D[] wallColliders;
    [SerializeField] private float fadedAlpha = 0.25f;

    public void ActivateXray()
    {
        SetXrayAlpha(fadedAlpha);
        SetRevealTargets(true);
        SetWallCollidersEnabled(false);
    }

    public void DeactivateXray()
    {
        SetXrayAlpha(1f);
        SetRevealTargets(false);
        SetWallCollidersEnabled(true);
    }

    private void SetXrayAlpha(float alpha)
    {
        if (xrayTargets == null)
        {
            return;
        }

        for (int i = 0; i < xrayTargets.Length; i++)
        {
            if (xrayTargets[i] == null)
            {
                continue;
            }

            SpriteRenderer renderer = xrayTargets[i].GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void SetRevealTargets(bool isVisible)
    {
        if (revealTargets == null)
        {
            return;
        }

        for (int i = 0; i < revealTargets.Length; i++)
        {
            if (revealTargets[i] != null)
            {
                revealTargets[i].SetActive(isVisible);
            }
        }
    }

    private void SetWallCollidersEnabled(bool isEnabled)
    {
        if (wallColliders == null)
        {
            return;
        }

        for (int i = 0; i < wallColliders.Length; i++)
        {
            if (wallColliders[i] != null)
            {
                wallColliders[i].enabled = isEnabled;
            }
        }
    }
}
