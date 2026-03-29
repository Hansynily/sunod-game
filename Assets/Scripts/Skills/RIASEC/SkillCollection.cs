using TMPro;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        public void CollectSkill(int skillIndex, GameObject collectible)
        {
            if (skillIndex < 0 || skillIndex >= _collected.Length) return;
            if (_collected[skillIndex]) return;

            _collected[skillIndex] = true;
            _collectedSkillCount++;

            switch (skillIndex)
            {
                case 0:
                    UnlockSlot(0, "Build", 0);
                    break;
                case 1:
                    UnlockSlot(1, "Track", 1);
                    break;
                case 2:
                    UnlockSlot(2, "Mimic", 2);
                    break;
                case 3:
                    UnlockSlot(3, "Bond", 3);
                    break;
                case 4:
                    UnlockSlot(3, "Charm", 4);
                    break;
                case 5:
                    break;
            }

            if (collectible != null)
                Destroy(collectible);

            ShowToast($"Skill Acquired: {SkillLetters[skillIndex]}");
            UpdateObjectiveText();
        }

        private void UnlockSlot(int slot, string skillName, int skillIndex)
        {
            _slotUnlocked[slot] = true;
            _slotSkillIndex[slot] = skillIndex;

            OnScreenButton button = _slotButtons[slot];
            if (button != null)
                button.gameObject.SetActive(true);

            TMP_Text label = _slotButtonLabels[slot];
            if (label != null)
                label.text = skillName;

        }

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
