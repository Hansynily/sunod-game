using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    /// <summary>
    /// Loops on the main menu (see vc_LoopMeta):
    ///  - after a finished run, Continue turns into "Go Back": straight into the going-back
    ///    cutscene, where the player chooses what to remember (vc_LoopMoments), then the
    ///    next loop starts,
    ///  - My Progress shows the loops lived so far (from loop 2 on),
    ///  - New Game wipes the loops too (with a warning once there's something to lose).
    /// </summary>
    public partial class MainMenuUIToolkit
    {
        [Header("Loops")]
        [Tooltip("Skill master list + starting kit (vc_GameSettings), for the skills to remember.")]
        [SerializeField] private vc_GameSettings gameSettings;

        /// <summary>Set by the results screen's Play Again: go back as soon as the menu knows the loop is over.</summary>
        public static bool OpenGoBackOnLoad;

        private Label _lblContinue;
        private VisualElement _iconContinue;
        private VisualElement _loopsSection;
        private Label _lblNewRunBody;
        private bool _goBackMode;
        private bool _goingBack;

        private void BindLoops(VisualElement root)
        {
            _lblContinue = root.Q<Label>("lbl-continue");
            _iconContinue = root.Q<VisualElement>("icon-continue");
            _loopsSection = root.Q<VisualElement>("loops-section");
            _lblNewRunBody = root.Q<Label>("lbl-newrun-body");
            _loopsSection?.SetDisplay(false);
            SetGoBackMode(false);
        }

        private void UnbindLoops()
        {
            OpenGoBackOnLoad = false;
        }

        // ── Continue <-> Go Back ──────────────────────────────────────────────

        /// <summary>Fetches the loop record; a finished run turns Continue into Go Back.</summary>
        private void RefreshLoopState(bool runFinished)
        {
            vc_LoopMeta.Fetch(meta =>
            {
                if (this == null) return;
                bool goBack = runFinished && meta != null && meta.current_finished;
                // A finished run whose loop record couldn't be fetched (offline, or a server
                // without loops yet): still say "Go Back", just greyed out, instead of a dead Continue.
                SetGoBackMode(goBack || (runFinished && meta == null));
                if (goBack) SetContinueEnabled(true);
                if (goBack && OpenGoBackOnLoad) StartGoBack();
                OpenGoBackOnLoad = false;
            });
        }

        private void SetGoBackMode(bool on)
        {
            _goBackMode = on;
            if (_lblContinue != null) _lblContinue.text = on ? "Go Back" : "Continue";
            _iconContinue?.EnableInClassList("icon-goback", on);
            _iconContinue?.EnableInClassList("icon-continue", !on);
        }

        private bool HasLoopsToLose =>
            vc_LoopMeta.Current != null && (vc_LoopMeta.Current.loops_completed > 0 || vc_LoopMeta.Current.current_loop > 1);

        private void RefreshNewRunWarning()
        {
            if (_lblNewRunBody == null) return;
            _lblNewRunBody.text = HasLoopsToLose
                ? "Everything will be erased: your run, remembered skills, keepsakes and stars."
                : "Your saved progress will be erased. Continue?";
        }

        // ── Going back ────────────────────────────────────────────────────────

        /// <summary>
        /// The figure's cutscene; after its first cut the player chooses what to remember
        /// (vc_LoopMoments.ShowRemember), the new loop is started on the server, then time
        /// rewinds and the first town loads.
        /// </summary>
        private void StartGoBack()
        {
            LoopMetaResponse meta = vc_LoopMeta.Current;
            if (meta == null || _goingBack) return;
            _goingBack = true;
            vc_Sfx.Play("click", 0.6f);
            TelemetryManager.Instance?.TagButtonClick("GoBack");

            List<vc_SkillData> candidates = MemoryCandidates(meta);
            var preselected = new List<string>(meta.kept_skills);
            int slots = Mathf.Max(1, meta.memory_slots);

            SunodGame.Cutscene.vc_CutscenePlayer.PendingInterludeAfterCut = 0;
            SunodGame.Cutscene.vc_CutscenePlayer.PendingInterlude = resume =>
            {
                vc_LoopMoments moments = vc_LoopMoments.Instance;
                if (moments == null) { BeginLoop(new List<string>(), _ => resume()); return; }
                moments.ShowRemember(candidates, slots, preselected, BeginLoop, resume);
            };

            if (!SunodGame.Cutscene.vc_CutscenePlayer.Play("GoBack", StartLoopRun))
            {
                _goingBack = false;
                Debug.LogWarning("[MainMenu] Go Back cutscene missing.");
            }
        }

        /// <summary>Clears the finished run's save and starts the next loop. Reports null on success.</summary>
        private static void BeginLoop(List<string> kept, System.Action<string> reply)
        {
            TelemetryManager tm = TelemetryManager.Instance;
            if (tm == null) { reply("Not signed in."); return; }

            vc_SaveManager.DeleteSave();
            vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
            vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
            PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);

            var payload = new LoopBeginPayload { kept_skills = kept };
            System.Action begin = () => tm.BeginMyLoop(payload,
                meta => { vc_LoopMeta.Set(meta); reply(null); },
                error =>
                {
                    Debug.LogWarning($"[MainMenu] Go Back failed: {error}");
                    reply("Couldn't go back right now. Check your connection and try again.");
                });
            tm.ResetMyRunState(_ => begin(), _ => begin());
        }

        private static void StartLoopRun()
        {
            vc_SessionTelemetry.Instance?.StartSession();
            SceneLoader.GoToPlay();
        }

        /// <summary>Skills they can remember: anything they owned at the end of the last loop
        /// (or already remembered), except the starting kit everyone gets anyway.</summary>
        private List<vc_SkillData> MemoryCandidates(LoopMetaResponse meta)
        {
            var starting = new HashSet<string>();
            if (gameSettings != null && gameSettings.startingSkills != null)
                foreach (vc_SkillData s in gameSettings.startingSkills) if (s != null) starting.Add(s.skillName);
            return meta.last_owned_skills.Concat(meta.kept_skills).Distinct()
                .Where(s => !starting.Contains(s))
                .Select(FindSkill).Where(s => s != null)
                .OrderBy(s => vc_SkillManager.SlotIndexForLetter(s.riaSecLetter))
                .ToList();
        }

        // ── My Progress: the loops lived so far ───────────────────────────────

        private void RefreshLoopsSection()
        {
            if (_loopsSection == null) return;
            vc_LoopMeta.Fetch(meta => { if (this != null) BuildLoopsSection(meta); });
        }

        private void BuildLoopsSection(LoopMetaResponse meta)
        {
            _loopsSection.Clear();
            bool show = meta != null && (meta.current_loop > 1 || meta.owned_mementos.Count > 0);
            _loopsSection.SetDisplay(show);
            if (!show) return;

            _loopsSection.Add(Heading(meta.current_finished
                ? $"Your loops ({meta.loops_completed} lived)"
                : $"Loop {meta.current_loop}  -  {vc_LoopMeta.ThreatName(meta.threat)}"));

            if (meta.kept_skills.Count > 0)
                _loopsSection.Add(Tags("Remembering: ", meta.kept_skills.Select(s => FindSkill(s)?.DisplayName ?? s)));

            if (meta.owned_mementos.Count > 0)
                _loopsSection.Add(Tags("Keepsakes: ", meta.owned_mementos.Select(m => vc_LoopMeta.KeepsakeById(m)?.name ?? m)));

            var careers = meta.history.Select(h => h.career).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
            if (careers.Count > 0) _loopsSection.Add(Tags("Paths found: ", careers));

            for (int i = meta.history.Count - 1; i >= 0; i--)
            {
                LoopHistoryItem h = meta.history[i];
                var card = new VisualElement();
                card.AddToClassList("history-card");
                card.AddToClassList("sl-panel-cream");
                var index = new Label($"L{h.loop}");
                index.AddToClassList("history-index");
                card.Add(index);
                var body = new VisualElement();
                body.AddToClassList("history-body");
                var career = new Label(string.IsNullOrWhiteSpace(h.career) ? "No career result" : h.career);
                career.AddToClassList("history-career");
                body.Add(career);
                var sub = new Label($"{vc_LoopMeta.ThreatName(h.threat)}  -  {h.passed} passed, {h.failed} failed");
                sub.AddToClassList("history-sub");
                body.Add(sub);
                card.Add(body);
                var code = new Label(string.IsNullOrWhiteSpace(h.holland_code) ? "---" : h.holland_code);
                code.AddToClassList("history-code");
                card.Add(code);
                _loopsSection.Add(card);
            }

            var divider = new VisualElement();
            divider.AddToClassList("divider");
            _loopsSection.Add(divider);
        }

        private static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("history-heading");
            return l;
        }

        private static VisualElement Tags(string label, IEnumerable<string> items)
        {
            var row = new VisualElement();
            row.AddToClassList("loop-tags");
            var lead = new Label(label);
            lead.AddToClassList("loop-tag-lead");
            row.Add(lead);
            foreach (string item in items)
            {
                var tag = new Label(item);
                tag.AddToClassList("loop-tag");
                row.Add(tag);
            }
            return row;
        }

        private vc_SkillData FindSkill(string name) =>
            gameSettings != null && gameSettings.skills != null
                ? System.Array.Find(gameSettings.skills, s => s != null && string.Equals(s.skillName, name, System.StringComparison.OrdinalIgnoreCase))
                : null;
    }
}
