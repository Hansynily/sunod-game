using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace SunodGame.Demo
{
    public class DemoSceneReferences : MonoBehaviour
    {
        [Header("Demo Skill Buttons")]
        [SerializeField] private OnScreenButton[] demoSkillButtons = new OnScreenButton[4];
        [SerializeField] private TMP_Text[] demoSkillLabels = new TMP_Text[4];

        public OnScreenButton[] DemoSkillButtons => demoSkillButtons;
        public TMP_Text[] DemoSkillLabels => demoSkillLabels;

        public bool HasDemoSkillReferences()
        {
            if (demoSkillButtons == null || demoSkillLabels == null) return false;
            if (demoSkillButtons.Length < 4 || demoSkillLabels.Length < 4) return false;

            for (int i = 0; i < 4; i++)
            {
                if (demoSkillButtons[i] == null || demoSkillLabels[i] == null)
                    return false;
            }

            return true;
        }
    }
}
