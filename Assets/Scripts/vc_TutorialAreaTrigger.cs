using UnityEngine;

/// <summary>
/// A one-shot trigger volume the player walks into to advance a tutorial beat.
/// Place between the spawn and the skill markers so the "six skills" explanation
/// fires on entry instead of automatically. Disables itself after firing.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class vc_TutorialAreaTrigger : MonoBehaviour
{
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;

        vc_TutorialFlowController.Instance?.OnEnteredSkillArea();
        gameObject.SetActive(false);
    }
}
