using UnityEngine;

public enum HollandCode
{
    Realistic,
    Investigative,
    Artistic,
    Social,
    Enterprising,
    Conventional
}

public abstract class SkillData : ScriptableObject
{
    public string skillName;
    
    [TextArea]
    public string description;

    public HollandCode hollandCode;
    
    public Sprite icon;

    public abstract void Activate(GameObject player);   
    public abstract void Deactivate(GameObject player);
}
