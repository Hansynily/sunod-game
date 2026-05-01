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

        private Transform GetContentParent()
        {
            return _contentRoot;
        }
    }
}
