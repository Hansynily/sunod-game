// LEGACY-PARKED: Not good for demo. Only use when on non-demo phase.
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Realistic/Giant")]
public class Giant : SkillData
{
    public float sizeMultiplier = 2.0f;

    public override void Activate(GameObject player)
    {
        player.transform.localScale *= sizeMultiplier;
    }

    public override void Deactivate(GameObject player)
    {
        player.transform.localScale /= sizeMultiplier;
    }
}
