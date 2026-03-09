using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseMimic()
        {
            _catFollowUntil = Time.time + MimicFollowDuration;

            if (_audioSource != null && _mimicClip != null)
                _audioSource.PlayOneShot(_mimicClip);

            ShowToast("Mimic used: cat is approaching.");
        }
    }
}
