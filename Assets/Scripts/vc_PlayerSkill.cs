using UnityEngine;

[DisallowMultipleComponent]
public abstract class vc_PlayerSkill : MonoBehaviour
{
    [SerializeField] private vc_SkillData skillData;

    public vc_SkillData SkillData => skillData;

    public virtual void UseSkill()
    {
    }
}
