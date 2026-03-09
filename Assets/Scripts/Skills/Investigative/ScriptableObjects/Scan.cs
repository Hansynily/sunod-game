using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Investigative/Scan")]
public class Scan : SkillData
{
    public int xrayLayer = 6;
    public int playerLayer = 0;
        
    public override void Activate(GameObject player)
    {
        Camera.main.cullingMask ^= (1 << xrayLayer);
        Camera.main.cullingMask ^= (1 << playerLayer);
    }

    public override void Deactivate(GameObject player)
    {
        Camera.main.cullingMask ^= (1 << xrayLayer);
        Camera.main.cullingMask ^= (1 << playerLayer);
    }
}
