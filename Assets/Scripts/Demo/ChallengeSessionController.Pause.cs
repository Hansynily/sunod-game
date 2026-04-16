using UnityEngine;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        public void SetExternalPause(bool isPaused)
        {
            if (isPaused)
            {
                if (_externallyPaused)
                {
                    return;
                }

                _pauseStartedAtUnscaled = Time.unscaledTime;
                _externallyPaused = true;
                return;
            }

            if (!_externallyPaused)
            {
                return;
            }

            float pausedDuration = Mathf.Max(0f, Time.unscaledTime - _pauseStartedAtUnscaled);
            if (pausedDuration > 0f)
            {
                _sessionStartTime += pausedDuration;
                _roundStartTime += pausedDuration;
            }

            _pauseStartedAtUnscaled = 0f;
            _externallyPaused = false;
        }
    }
}
