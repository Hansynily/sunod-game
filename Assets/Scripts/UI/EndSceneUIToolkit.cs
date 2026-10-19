using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class EndSceneUIToolkit : MonoBehaviour
    {
        private static readonly Dictionary<string, string> TraitNames = new()
        {
            { "R", "Realistic" },
            { "I", "Investigative" },
            { "A", "Artistic" },
            { "S", "Social" },
            { "E", "Enterprising" },
            { "C", "Conventional" }
        };

        private Button _btnPlayAgain;
        private Button _btnMainMenu;
        private Button _btnSurveyLink;

        private void Start()
        {
            DisableCarryoverUI();
        }

        private void DisableCarryoverUI()
        {
            var ownDoc = GetComponent<UIDocument>();
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (doc == ownDoc) continue;
                if (doc.gameObject.scene.name == "DontDestroyOnLoad")
                    doc.enabled = false;
                else
                    doc.gameObject.SetActive(false);
            }

            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene.name == "DontDestroyOnLoad")
                    canvas.enabled = false;
                else
                    canvas.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            root.Q<Label>("lbl-career-name").text  = GetCareerName();
            root.Q<Label>("lbl-holland-code").text  = SafeText(GameSessionData.DisplayHollandCode);
            root.Q<Label>("lbl-top-traits").text    = GetTopTraits();
            // out of everyone in the 3 towns (16 each); not a target - nobody can help all 48
            root.Q<Label>("lbl-quests").text        = $"{GameSessionData.rounds_cleared}/48";
            root.Q<Label>("lbl-stars").text         = GameSessionData.total_stars.ToString();

            // Ended early ("See my results so far"): say the result is provisional.
            Label partial = root.Q<Label>("lbl-partial-note");
            if (partial != null)
            {
                partial.text = GameSessionData.partialRunNote ?? string.Empty;
                partial.style.display = string.IsNullOrWhiteSpace(partial.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // Survey: only once the game is really done (full run), never on partial results.
            bool fullRun = string.IsNullOrWhiteSpace(GameSessionData.partialRunNote);
            VisualElement surveyCard = root.Q<VisualElement>("survey-card");
            if (surveyCard != null)
                surveyCard.style.display = fullRun ? DisplayStyle.Flex : DisplayStyle.None;
            if (fullRun)
                vc_Survey.Unlock();

            Label surveyLink = root.Q<Label>("lbl-survey-link");
            if (surveyLink != null)
                surveyLink.text = vc_Survey.DisplayUrl;

            _btnPlayAgain  = root.Q<Button>("btn-play-again");
            _btnMainMenu   = root.Q<Button>("btn-main-menu");
            _btnSurveyLink = root.Q<Button>("btn-survey-link");

            _btnPlayAgain? .RegisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu?  .RegisterCallback<ClickEvent>(OnMainMenuClicked);
            _btnSurveyLink?.RegisterCallback<ClickEvent>(OnSurveyLinkClicked);
        }

        private void OnDisable()
        {
            _btnPlayAgain? .UnregisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu?  .UnregisterCallback<ClickEvent>(OnMainMenuClicked);
            _btnSurveyLink?.UnregisterCallback<ClickEvent>(OnSurveyLinkClicked);
        }

        private void OnSurveyLinkClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("SurveyLink");
            vc_Survey.Open();
        }

        private void OnPlayAgainClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("PlayAgain");
            SceneLoader.GoToPlay();
        }

        private void OnMainMenuClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("MainMenu");
            SceneLoader.GoToMainMenu();
        }

        private static string GetCareerName()
        {
            if (!string.IsNullOrWhiteSpace(GameSessionData.careerResult))
                return GameSessionData.careerResult;
            if (!string.IsNullOrWhiteSpace(GameSessionData.careerFamily))
                return GameSessionData.careerFamily;
            return "Unknown";
        }

        private static string GetTopTraits()
        {
            RiasecScoresDto scores = GameSessionData.riasecScores;
            if (scores == null)
                return "---";

            var entries = new List<KeyValuePair<string, int>>
            {
                new("R", scores.r), new("I", scores.i), new("A", scores.a),
                new("S", scores.s), new("E", scores.e), new("C", scores.c)
            };
            entries.Sort((a, b) => b.Value.CompareTo(a.Value));

            var top = new List<string>();
            foreach (var entry in entries)
            {
                if (entry.Value <= 0 || top.Count >= 3) break;
                if (TraitNames.TryGetValue(entry.Key, out string name))
                    top.Add(name);
            }

            return top.Count > 0 ? string.Join(" ❖ ", top) : "---";
        }

        private static string SafeText(string value) =>
            string.IsNullOrWhiteSpace(value) ? "---" : value;
    }
}
