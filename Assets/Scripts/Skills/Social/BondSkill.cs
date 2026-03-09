using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseBond()
        {
            UpdateBondTimers();
            if (_bondExpirations.Count >= 2)
            {
                ShowToast("Bond stacks are at max.");
                return;
            }

            _bondExpirations.Add(Time.time + 5f);
            ShowToast("Bond applied.");
        }

        private void UpdateBondTimers()
        {
            for (int i = _bondExpirations.Count - 1; i >= 0; i--)
            {
                if (_bondExpirations[i] <= Time.time)
                    _bondExpirations.RemoveAt(i);
            }
        }

        private float GetEffectiveFleeRadius()
        {
            int stacks = Mathf.Min(_bondExpirations.Count, 2);
            float multiplier = 1f - (0.3f * stacks);
            return 3f * multiplier;
        }
    }
}
