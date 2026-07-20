using UnityEngine;

/// <summary>
/// One of the six color-coded RIASEC skill markers on the tutorial floor.
/// Walking into it asks the flow controller to present this skill's explain
/// dialog (browse freely; only one gets picked). Dumb component - all decision
/// logic lives in vc_TutorialFlowController.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class vc_TutorialSkillMarker : MonoBehaviour
{
    [SerializeField] private vc_SkillData skill;

    public vc_SkillData Skill => skill;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        vc_TutorialFlowController.Instance?.PresentSkill(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        vc_TutorialFlowController.Instance?.MarkerExited(this);
    }

    public void Despawn()
    {
        gameObject.SetActive(false);
    }
}
