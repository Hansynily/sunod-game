using SunodGame.Core;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        // Legacy non-challenge win dialog hook kept only so dormant fallback UI still compiles.
        private void OnContinuePressed()
        {
            if (_submittingResults) return;

            _submittingResults = true;
            if (_continueButton != null)
                _continueButton.interactable = false;

            SceneLoader.GoToEnd();
        }

        private void UnlockChallengeSessionSkills()
        {
            for (int i = 0; i < _collected.Length; i++)
                _collected[i] = true;

            _collectedSkillCount = _collected.Length;
            UnlockSlot(0, "Build", 0);
            UnlockSlot(1, "Track", 1);
            UnlockSlot(2, "Mimic", 2);
            UnlockSlot(3, "Bond", 3);
        }

        private Transform GetContentParent()
        {
            return _contentRoot;
        }
    }
}
