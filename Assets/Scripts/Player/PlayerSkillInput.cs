using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SkillController))]
public class PlayerSkillInput : MonoBehaviour
{
    public InputAction SkillAction0;
    public InputAction SkillAction1;
    public InputAction SkillAction2;
    public InputAction SkillAction3;

    private SkillController skillController;

    void Awake()
    {
        skillController = GetComponent<SkillController>();

        SkillAction0.Enable();
        SkillAction1.Enable();
        SkillAction2.Enable();
        SkillAction3.Enable();


        SkillAction0.started += ctx => skillController.ActivateSlot(0);
        SkillAction0.canceled += ctx => skillController.DeactivateSlot(0);

        SkillAction1.started += ctx => skillController.ActivateSlot(1);
        SkillAction1.canceled += ctx => skillController.DeactivateSlot(1);

        SkillAction2.started += ctx => skillController.ActivateSlot(2);
        SkillAction2.canceled += ctx => skillController.DeactivateSlot(2);

        SkillAction3.started += ctx => skillController.ActivateSlot(3);
        SkillAction3.canceled += ctx => skillController.DeactivateSlot(3);
    }

}