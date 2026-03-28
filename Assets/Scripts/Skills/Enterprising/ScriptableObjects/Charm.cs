// LEGACY-PARKED: Not good for demo. Only use when on non-demo phase.
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Enterprising/Charm")]
public class Charm : SkillData
{
    public override void Activate(GameObject player)
    {
        GameObject.FindGameObjectWithTag("NPC").GetComponent<NPCController>().isCharmed = true;
    }

    public override void Deactivate(GameObject player)
    {
        GameObject.FindGameObjectWithTag("NPC").GetComponent<NPCController>().isCharmed = false;
    }
}
