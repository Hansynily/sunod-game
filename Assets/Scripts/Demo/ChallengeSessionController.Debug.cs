using UnityEngine;
using UnityEngine.InputSystem;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        private void UpdateDebugTextVisibility()
        {
            if (_debugText != null &&
                _debugText.gameObject.activeSelf &&
                _debugTextHideAt > 0f &&
                Time.unscaledTime >= _debugTextHideAt)
            {
                _debugText.gameObject.SetActive(false);
                _debugTextHideAt = -1f;
            }
        }

        private void HandleKeyboardDebugShortcuts()
        {
            if (_state != ChallengeSessionPhase.RoundActive) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                DebugCompleteThreeStarSuccess();
                return;
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                DebugCompleteTwoStarSuccess();
                return;
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                DebugCompleteOneStarSuccess();
                return;
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
                DebugCompleteZeroStarFailure();
        }

        [ContextMenu("Debug/3 Star Success")]
        public void DebugCompleteThreeStarSuccess()
        {
            SetDebugText("3-star success recorded.");
            CompleteRoundFromDebug(true, 3);
        }

        [ContextMenu("Debug/2 Star Success")]
        public void DebugCompleteTwoStarSuccess()
        {
            SetDebugText("2-star success recorded.");
            CompleteRoundFromDebug(true, 2);
        }

        [ContextMenu("Debug/1 Star Success")]
        public void DebugCompleteOneStarSuccess()
        {
            SetDebugText("1-star success recorded.");
            CompleteRoundFromDebug(true, 1);
        }

        [ContextMenu("Debug/0 Star Failure")]
        public void DebugCompleteZeroStarFailure()
        {
            SetDebugText("0-star failure recorded.");
            CompleteRoundFromDebug(false, 0);
        }

        private void CompleteRoundFromDebug(bool solved, int starsEarned)
        {
            if (_state != ChallengeSessionPhase.RoundActive) return;
            if (_currentRoundIndex < 0 || _currentRoundIndex >= _definitions.Count) return;

            ChallengeDefinition definition = _definitions[_currentRoundIndex];
            int retryCount = definition.star_thresholds.GetRetryCountForStars(starsEarned);
            CompleteActiveRoundWithRetryCount(solved, retryCount, autoAdvance: false);
        }

        private void SetDebugText(string statusLine)
        {
            if (_debugText == null) return;

            _debugText.gameObject.SetActive(true);
            _debugText.text = statusLine;
            _debugTextHideAt = Time.unscaledTime + 1.5f;
        }

        private static string GetSkillDebugLabel(int skillIndex)
        {
            return skillIndex switch
            {
                0 => "Build",
                1 => "Track",
                2 => "Mimic",
                3 => "Bond",
                4 => "Charm",
                5 => "Plan",
                _ => "Unknown skill"
            };
        }
    }
}
