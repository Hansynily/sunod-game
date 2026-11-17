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
            int questsOutOf = Mathf.Max(48, GameSessionData.rounds_attempted);
            Label quests = root.Q<Label>("lbl-quests");
            Label stars  = root.Q<Label>("lbl-stars");
            quests.text = $"{GameSessionData.rounds_cleared} of {questsOutOf}";
            stars.text  = GameSessionData.total_stars.ToString();

            // Ended early ("See my results so far"): say the result is provisional.
            Label partial = root.Q<Label>("lbl-partial-note");
            if (partial != null)
            {
                partial.text = GameSessionData.partialRunNote ?? string.Empty;
                partial.style.display = string.IsNullOrWhiteSpace(partial.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // Website card: a link to the SUNOD server's site.
            Label siteLink = root.Q<Label>("lbl-survey-link");
            if (siteLink != null)
                siteLink.text = "Visit website";

            _btnPlayAgain  = root.Q<Button>("btn-play-again");
            _btnMainMenu   = root.Q<Button>("btn-main-menu");
            _btnSurveyLink = root.Q<Button>("btn-survey-link");

            _btnPlayAgain? .RegisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu?  .RegisterCallback<ClickEvent>(OnMainMenuClicked);
            _btnSurveyLink?.RegisterCallback<ClickEvent>(OnSurveyLinkClicked);

            PlayEntrance(root, quests, stars, questsOutOf);
            ShowLoopPaths(root);
        }

        /// <summary>
        /// From loop 2 on, when the loops disagree: show both the first path (loop 1, the
        /// baseline) and the latest, with what changed (same rule as My Progress and the admin
        /// page; text from the server, built from what the runs recorded).
        /// </summary>
        private void ShowLoopPaths(VisualElement root)
        {
            VisualElement section = root.Q<VisualElement>("career-section");
            if (section == null) return;
            vc_LoopMeta.Fetch(meta =>
            {
                if (this == null || meta == null || meta.paths == null || meta.paths.baseline == null
                    || meta.paths.baseline.loop <= 0 || !meta.paths.differ) return;
                LoopPaths p = meta.paths;
                var box = new VisualElement { name = "loop-paths" };
                box.style.marginTop = 10;
                box.style.alignItems = Align.Center;
                Label Line(string text, int size, Color color, bool bold)
                {
                    var l = new Label(text);
                    l.AddToClassList(bold ? "sn-bold" : "sn-caption");
                    l.style.fontSize = size;
                    l.style.color = color;
                    l.style.whiteSpace = WhiteSpace.Normal;
                    l.style.unityTextAlign = TextAnchor.MiddleCenter;
                    return l;
                }
                box.Add(Line($"Your first path (baseline): {p.baseline.holland_code} · {p.baseline.career}", 20, vc_UITheme.InkMuted, false));
                box.Add(Line($"Your latest path: {p.latest.holland_code} · {p.latest.career}", 22, new Color32(76, 50, 140, 255), true));
                if (!string.IsNullOrWhiteSpace(p.change_line)) box.Add(Line(p.change_line, 20, vc_UITheme.Ink, true));
                if (!string.IsNullOrWhiteSpace(p.reason_line)) box.Add(Line(p.reason_line, 18, vc_UITheme.InkMuted, false));
                section.Add(box);
                vc_UIAnim.Show(box, vc_UIAnim.Enter.SlideUp, 0.6f);
            });
        }

        /// <summary>
        /// UI V2 reveal: result card pops in, the career chip punches, stats cascade, the
        /// numbers count up, then the website card and the buttons slide in.
        /// </summary>
        private static void PlayEntrance(VisualElement root, Label quests, Label stars, int questsOutOf)
        {
            vc_UIAnim.PressAll(root);

            VisualElement result = root.Q<VisualElement>("result-panel");
            vc_UIAnim.Show(result, vc_UIAnim.Enter.Pop, 0.05f);

            Label career = root.Q<Label>("lbl-career-name");
            if (career != null)
                vc_UIAnim.Show(career, vc_UIAnim.Enter.Pop, 0.3f, () => vc_UIAnim.Punch(career, 0.14f, 0.4f));

            VisualElement stats = root.Q<VisualElement>("stats-section");
            if (stats != null) vc_UIAnim.Stagger(stats.Children(), vc_UIAnim.Enter.SlideUp, 0.06f, 0.45f);

            // start the numbers at 0 so nothing flashes at the final value during the delay
            if (quests != null) quests.text = "0 of " + questsOutOf;
            if (stars != null) stars.text = "0";
            if (quests != null)
                vc_UIAnim.CountUp(quests, 0, GameSessionData.rounds_cleared, "{0} of " + questsOutOf, 0.7f, 0.6f);
            if (stars != null)
                vc_UIAnim.CountUp(stars, 0, GameSessionData.total_stars, "{0}", 0.8f, 0.65f);

            vc_UIAnim.Show(root.Q<VisualElement>("survey-card"), vc_UIAnim.Enter.SlideLeft, 0.35f);

            VisualElement actions = root.Q<VisualElement>("action-row");
            if (actions != null) vc_UIAnim.Stagger(actions.Children(), vc_UIAnim.Enter.SlideUp, 0.06f, 0.75f);
        }

        private void OnDisable()
        {
            _btnPlayAgain? .UnregisterCallback<ClickEvent>(OnPlayAgainClicked);
            _btnMainMenu?  .UnregisterCallback<ClickEvent>(OnMainMenuClicked);
            _btnSurveyLink?.UnregisterCallback<ClickEvent>(OnSurveyLinkClicked);
        }

        private void OnSurveyLinkClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("SiteLink");
            vc_SiteLink.Open();
        }

        private void OnPlayAgainClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("PlayAgain");
            // Another run = the next loop: the main menu's Go Back screen (memories,
            // keepsakes), which also clears the finished run's save properly.
            MainMenuUIToolkit.OpenGoBackOnLoad = true;
            SceneLoader.GoToMainMenu();
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

            return top.Count > 0 ? string.Join(", ", top) : "---";
        }

        private static string SafeText(string value) =>
            string.IsNullOrWhiteSpace(value) ? "---" : value;
    }
}
