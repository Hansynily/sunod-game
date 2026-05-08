using UnityEngine;

[DisallowMultipleComponent]
public abstract class vc_PlayerSkill : MonoBehaviour
{
    [SerializeField] private vc_SkillData skillData;

    public vc_SkillData SkillData => skillData;

    public void Initialize(vc_SkillData data)
    {
        skillData = data;
    }

    public virtual void UseSkill()
    {
    }
}
