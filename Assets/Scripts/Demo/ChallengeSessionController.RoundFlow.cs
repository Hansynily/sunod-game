using System;
using SunodGame.Core;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        public void RecordSkillUse(RiasecCode riasecCode)
        {
            if (_externallyPaused) return;
            if (_state != ChallengeSessionPhase.RoundActive) return;

            int index = (int)riasecCode;
            if (index < 0 || index >= _activeRoundSkillUse.Length) return;

            _activeRoundSkillUse[index]++;
            Debug.Log($"[ChallengeSession] Skill used -> {riasecCode} on round {_currentRoundIndex + 1}");
            SetDebugText($"{GetSkillDebugLabel(index)} used.");
        }

        public void RegisterRetry()
        {
            if (_externallyPaused) return;
            if (_state != ChallengeSessionPhase.RoundActive) return;
            _currentRetryCount++;
        }

        public void CompleteActiveRound(bool solved)
        {
            if (_externallyPaused) return;
            CompleteActiveRoundWithRetryCount(solved, _currentRetryCount, autoAdvance: false);
        }

        public void CompleteActiveRoundWithRetryCount(bool solved, int retryCount, bool autoAdvance = false)
        {
            if (_externallyPaused) return;
            if (_state != ChallengeSessionPhase.RoundActive) return;
            if (_currentRoundIndex < 0 || _currentRoundIndex >= _definitions.Count) return;

            ChallengeDefinition definition = _definitions[_currentRoundIndex];
            int clampedRetryCount = Mathf.Max(0, retryCount);
            int starsEarned = solved ? definition.star_thresholds.EvaluateStars(clampedRetryCount) : 0;
            ChallengeRoundResult result = BuildRoundResult(definition, solved, starsEarned, clampedRetryCount);

            _roundResults.Add(result);
            SetStarsDisplay(starsEarned);
            EnterState(ChallengeSessionPhase.RoundResult);

            if (_currentRoundIndex >= _definitions.Count - 1)
            {
                CompleteSession();
                return;
            }

            if (autoAdvance)
            {
                AdvanceToNextRound();
                return;
            }

            SetNextRoundButtonVisible(true);
        }

        private void StartNewSession()
        {
            EnterState(ChallengeSessionPhase.SessionStart);

            GameSessionData.Reset();
            _roundResults.Clear();
            Array.Clear(_activeRoundSkillUse, 0, _activeRoundSkillUse.Length);

            _currentRetryCount = 0;
            _currentRoundIndex = -1;
            _sessionId = Guid.NewGuid().ToString("N");
            _startedAtIso = DateTimeOffset.UtcNow.ToString("o");
            _sessionStartTime = Time.unscaledTime;

            GameSessionData.session_id = _sessionId;
            GameSessionData.started_at = _startedAtIso;
            GameSessionData.result_source = string.Empty;

            SessionState.Instance?.BeginRun("challenge_session");

            AdvanceToNextRound();
        }

        private void AdvanceToNextRound()
        {
            EnterState(ChallengeSessionPhase.NextRound);

            int nextRoundIndex = _currentRoundIndex + 1;
            if (nextRoundIndex >= _definitions.Count)
            {
                CompleteSession();
                return;
            }

            BeginRound(nextRoundIndex);
        }

        private void BeginRound(int roundIndex)
        {
            _currentRoundIndex = roundIndex;
            _currentRetryCount = 0;
            _roundStartTime = Time.unscaledTime;
            Array.Clear(_activeRoundSkillUse, 0, _activeRoundSkillUse.Length);

            EnterState(ChallengeSessionPhase.RoundIntro);

            ChallengeDefinition definition = _definitions[_currentRoundIndex];
            ApplyChallengeRootVisibility(_currentRoundIndex);
            UpdateHudForRound(definition, _currentRoundIndex);
            SetStarsDisplay(0);
            SetNextRoundButtonVisible(false);

            EnterState(ChallengeSessionPhase.RoundActive);
        }

        private void CompleteSession()
        {
            if (_externallyPaused) return;
            EnterState(ChallengeSessionPhase.SessionComplete);
            StoreSessionSummary();
            SubmitRunSummaryAndLoadEndScene();
        }

        private void HandleNextRoundClicked()
        {
            if (_externallyPaused) return;
            if (_state != ChallengeSessionPhase.RoundResult) return;
            AdvanceToNextRound();
        }

        private void EnterState(ChallengeSessionPhase nextState)
        {
            _state = nextState;
            Debug.Log($"[ChallengeSession] State -> {_state}");
        }
    }
}
