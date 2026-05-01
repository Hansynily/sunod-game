using TMPro;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void OnSkillPressed(int slot)
        {
            if (!_initialized) return;
            if (slot < 0 || slot > 3) return;
            if (!_slotUnlocked[slot]) return;

            int skillIndex = _slotSkillIndex[slot];
            if (skillIndex < 0) return;

            TriggerSkill(skillIndex);
        }

        public void TriggerSkill(int skillIndex)
        {
            if (!_initialized) return;
            if (skillIndex < 0 || skillIndex >= SkillLetters.Length) return;

            RegisterSkillUse(skillIndex);

            switch (skillIndex)
            {
                case 0:
                    UseBuild();
                    break;
                case 1:
                    UseTrack();
                    break;
                case 2:
                    UseMimic();
                    break;
                case 3:
                    UseBond();
                    break;
                case 4:
                    UseCharm();
                    break;
                case 5:
                    ShowToast("Plan skill is parked.");
                    break;
            }
        }

        private void RegisterSkillUse(int skillIndex)
        {
            _skillUseCount[skillIndex]++;
            if (_firstUseOrder[skillIndex] == int.MaxValue)
                _firstUseOrder[skillIndex] = _nextUseOrder++;

            if (ChallengeSessionController.Instance != null)
                ChallengeSessionController.Instance.RecordSkillUse((RiasecCode)skillIndex);
        }

        private void UpdateSkillButtonVisuals()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (!_slotUnlocked[slot]) continue;
                if (_slotButtonLabels[slot] == null || _slotButtonImages[slot] == null) continue;

                int skillIndex = _slotSkillIndex[slot];
                string baseName = skillIndex switch
                {
                    0 => "Build",
                    1 => "Track",
                    2 => "Mimic",
                    3 => "Bond",
                    4 => "Charm",
                    _ => "Skill"
                };

                string label = baseName;
                float alpha = 1f;

                if (skillIndex == 0 && _buildUsed)
                {
                    label = "Build (Used)";
                    alpha = GetBuildUsedAlpha();
                }
                else if (skillIndex == 1 && _trackActive)
                {
                    label = "Track [ON]";
                }
                else if (skillIndex == 3)
                {
                    if (_bondExpirations.Count > 0)
                    {
                        float longest = 0f;
                        for (int i = 0; i < _bondExpirations.Count; i++)
                        {
                            float rem = _bondExpirations[i] - Time.time;
                            if (rem > longest) longest = rem;
                        }

                        label = $"Bond x{_bondExpirations.Count} ({longest:F1}s)";
                    }
                    else
                    {
                        label = "Bond";
                    }
                }

                _slotButtonLabels[slot].text = label;

                Color c = _slotButtonImages[slot].color;
                c.a = alpha;
                _slotButtonImages[slot].color = c;
            }
        }

        private float GetBuildUsedAlpha()
        {
            if (_sceneReferences != null)
                return _sceneReferences.BuildUsedAlpha;

            return 0.35f;
        }
    }
}
