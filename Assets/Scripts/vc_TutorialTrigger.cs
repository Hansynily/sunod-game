using UnityEngine;

public enum vc_TutorialTriggerType
{
    MoveTarget
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class vc_TutorialTrigger : MonoBehaviour
{
    [SerializeField] private vc_Level0TutorialController controller;
    [SerializeField] private vc_TutorialTriggerType triggerType = vc_TutorialTriggerType.MoveTarget;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<vc_Level0TutorialController>();
        }
    }

    public void Configure(vc_Level0TutorialController tutorialController, vc_TutorialTriggerType configuredTriggerType)
    {
        controller = tutorialController;
        triggerType = configuredTriggerType;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        controller?.HandleTriggerEntered(triggerType);
    }
}
