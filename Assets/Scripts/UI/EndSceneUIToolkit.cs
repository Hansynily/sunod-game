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

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            root.Q<Label>("lbl-career-name").text  = GetCareerName();
            root.Q<Label>("lbl-holland-code").text  = SafeText(GameSessionData.hollandCode);
            root.Q<Label>("lbl-top-traits").text    = GetTopTraits();
            root.Q<Label>("lbl-quests").text        = $"{GameSessionData.rounds_cleared}/{GameSessionData.rounds_attempted}";
            root.Q<Label>("lbl-stars").text         = GameSessionData.total_stars.ToString();

            _btnPlayAgain = root.Q<Button>("btn-play-again");
            _btnMainMenu  = root.Q<Button>("btn-main-menu");

            _btnPlayAgain?.RegisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu? .RegisterCallback<ClickEvent>(OnMainMenuClicked);
        }

        private void OnDisable()
        {
            _btnPlayAgain?.UnregisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu? .UnregisterCallback<ClickEvent>(OnMainMenuClicked);
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
