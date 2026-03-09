using System.Collections.Generic;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void OnContinuePressed()
        {
            if (_submittingResults) return;
            _submittingResults = true;

            if (_continueButton != null)
                _continueButton.interactable = false;

            GameSessionData.skillUseCount = (int[])_skillUseCount.Clone();
            GameSessionData.firstUseOrder = (int[])_firstUseOrder.Clone();
            CareerResultResolver.ResolveAndStore(GameSessionData.skillUseCount, GameSessionData.firstUseOrder);
            GameSessionData.usedBackendResult = false;
            GameSessionData.backendMessage = string.Empty;

            var telemetry = TelemetryManager.Instance;
            if (telemetry == null)
            {
                SceneLoader.GoToEnd();
                return;
            }

            var payload = new QuestAttemptTelemetryIn
            {
                quest_id = !string.IsNullOrWhiteSpace(SessionState.Instance?.CurrentQuestId)
                    ? SessionState.Instance.CurrentQuestId
                    : "cat_demo_quest",
                quest_result = "success",
                time_spent_seconds = SessionState.Instance != null ? SessionState.Instance.GetElapsedSeconds() : 0,
                selected_skills = BuildSelectedSkillsForTelemetry(),
            };

            telemetry.SubmitQuestAttempt(
                payload,
                onSuccess: (res) =>
                {
                    GameSessionData.backendMessage = res?.message ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(res?.holland_code))
                    {
                        GameSessionData.hollandCode = res.holland_code.ToUpperInvariant();
                        GameSessionData.usedBackendResult = true;
                    }

                    if (!string.IsNullOrWhiteSpace(res?.career_result))
                    {
                        GameSessionData.careerResult = res.career_result;
                        GameSessionData.usedBackendResult = true;
                    }

                    SceneLoader.GoToEnd();
                },
                onError: (err) =>
                {
                    GameSessionData.backendMessage = err ?? "Telemetry submit failed.";
                    SceneLoader.GoToEnd();
                }
            );
        }

        private List<SelectedSkill> BuildSelectedSkillsForTelemetry()
        {
            var skills = new List<SelectedSkill>();
            for (int i = 0; i < _skillUseCount.Length && i < SkillLetters.Length; i++)
            {
                int uses = Mathf.Max(0, _skillUseCount[i]);
                for (int n = 0; n < uses; n++)
                {
                    skills.Add(new SelectedSkill
                    {
                        riasec_code = SkillLetters[i],
                        skill_name = SkillActionNames[i]
                    });
                }
            }

            if (skills.Count == 0)
            {
                skills.Add(new SelectedSkill
                {
                    riasec_code = "C",
                    skill_name = "Plan"
                });
            }

            return skills;
        }
    }
}
