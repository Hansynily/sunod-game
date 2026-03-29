using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseTrack()
        {
            _trackActive = !_trackActive;

            if (!_trackActive)
            {
                ClearPlanDots();
                ShowToast("Track disabled.");
                return;
            }

            _nextPlanUpdateAt = 0f;
            UpdatePlanIndicator();
            ShowToast("Track enabled.");
        }

        private void SpawnPawPrint()
        {
        }

        private void UpdatePawPrints()
        {
        }
    }
}
