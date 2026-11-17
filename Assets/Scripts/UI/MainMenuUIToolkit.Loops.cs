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
            if (_lblContinue != null) _lblContinue.text = on ? "Go back" : "Continue";
            _iconContinue?.EnableInClassList("icon-goback", on);
            _iconContinue?.EnableInClassList("icon-continue", !on);
            RefreshMenuEmphasis();
        }

        private bool HasLoopsToLose =>
            vc_LoopMeta.Current != null && (vc_LoopMeta.Current.loops_completed > 0 || vc_LoopMeta.Current.current_loop > 1);

        private void RefreshNewRunWarning()
        {
            if (_lblNewRunBody == null) return;
            _lblNewRunBody.text = HasLoopsToLose
                ? "You'll lose everything and start over."
                : "You'll lose this run.";
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

            // owned keepsakes to choose from for the pockets (only these work next loop)
            var owned = meta.owned_mementos.Select(vc_LoopMeta.KeepsakeById).Where(k => k != null).ToList();
            int pockets = Mathf.Clamp(meta.pockets, 2, 4);
            var carriedNow = new List<string>(meta.carried_mementos);

            SunodGame.Cutscene.vc_CutscenePlayer.PendingInterludeAfterCut = 0;
            SunodGame.Cutscene.vc_CutscenePlayer.PendingInterlude = resume =>
            {
                vc_LoopMoments moments = vc_LoopMoments.Instance;
                if (moments == null) { BeginLoop(new List<string>(), carriedNow, _ => resume()); return; }
                List<string> kept = new List<string>();
                // 1) what will you remember? (kept locally) -> 2) what will you carry? -> begin
                moments.ShowRemember(candidates, slots, preselected,
                    (held, reply) => { kept = held; reply(null); },
                    () =>
                    {
                        // nothing to choose: every keepsake fits in the pockets
                        if (owned.Count <= pockets)
                        {
                            BeginLoop(kept, owned.Select(k => k.id).ToList(), err =>
                            {
                                if (err == null) { resume(); return; }
                                moments.ShowRemember(candidates, slots, kept, (h, r) => BeginLoop(h, owned.Select(k => k.id).ToList(), r), resume);
                            });
                            return;
                        }
                        moments.ShowPockets(owned, pockets, carriedNow,
                            (bag, reply) => BeginLoop(kept, bag, reply), resume);
                    });
            };

            if (!SunodGame.Cutscene.vc_CutscenePlayer.Play("GoBack", StartLoopRun))
            {
                _goingBack = false;
                Debug.LogWarning("[MainMenu] Go Back cutscene missing.");
            }
        }

        /// <summary>Clears the finished run's save and starts the next loop. Reports null on success.</summary>
        private static void BeginLoop(List<string> kept, List<string> carried, System.Action<string> reply)
        {
            TelemetryManager tm = TelemetryManager.Instance;
            if (tm == null) { reply("Not signed in."); return; }

            vc_SaveManager.DeleteSave();
            vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
            vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
            vc_SkillLevels.PendingRestore = null;
            PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);

            var payload = new LoopBeginPayload { kept_skills = kept, carried_mementos = carried ?? new List<string>(), has_carried = carried != null };
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
        /// (or already remembered), except the starting kit everyone gets anyway - unless that
        /// starting skill has XP, since remembering it is what keeps its level.</summary>
        private List<vc_SkillData> MemoryCandidates(LoopMetaResponse meta)
        {
            var starting = new HashSet<string>();
            if (gameSettings != null && gameSettings.startingSkills != null)
                foreach (vc_SkillData s in gameSettings.startingSkills) if (s != null) starting.Add(s.skillName);
            var leveled = new HashSet<string>();
            foreach (SkillXpEntryDto e in (meta.last_skill_xp ?? new List<SkillXpEntryDto>()).Concat(meta.kept_skill_xp ?? new List<SkillXpEntryDto>()))
                if (e != null && e.xp > 0 && !string.IsNullOrEmpty(e.skill)) leveled.Add(e.skill);
            return meta.last_owned_skills.Concat(meta.kept_skills).Distinct()
                .Where(s => !starting.Contains(s) || leveled.Contains(s))
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
            bool show = meta != null && (meta.current_loop > 1 || meta.owned_mementos.Count > 0 || meta.history.Count > 0);
            _loopsSection.SetDisplay(show);
            if (!show) return;

            _loopsSection.Add(Heading(meta.current_finished
                ? $"Your loops ({meta.loops_completed} lived)"
                : $"Loop {meta.current_loop}  ·  {vc_LoopMeta.ThreatName(meta.threat)}"));

            // Final result when loops disagree: show both, loop 1 as the baseline.
            LoopPaths paths = meta.paths;
            if (paths != null && paths.baseline != null && paths.baseline.loop > 0)
                _loopsSection.Add(PathsCard(paths));

            if (meta.kept_skills.Count > 0)
                _loopsSection.Add(Tags("Remembering: ", meta.kept_skills.Select(SkillLabel)));
            if (meta.carried_mementos.Count > 0)
                _loopsSection.Add(Tags("Carrying: ", meta.carried_mementos.Select(KeepsakeLabel)));

            // every finished loop, in order, side by side (wraps on narrow screens)
            var row = new VisualElement();
            row.AddToClassList("loop-cards");
            foreach (LoopHistoryItem h in meta.history)
                row.Add(LoopCard(h));
            if (meta.history.Count > 0) _loopsSection.Add(row);

            var divider = new VisualElement();
            divider.AddToClassList("divider");
            _loopsSection.Add(divider);

            vc_UIAnim.Stagger(_loopsSection.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.1f);
            vc_UIAnim.Stagger(row.Children(), vc_UIAnim.Enter.Pop, 0.06f, 0.2f);
        }

        /// <summary>"Your first path (baseline)" next to "Your latest path", plus what changed.</summary>
        private static VisualElement PathsCard(LoopPaths p)
        {
            var card = new VisualElement();
            card.AddToClassList("sn-card");
            card.AddToClassList("loop-paths");
            var pair = new VisualElement();
            pair.AddToClassList("loop-paths-pair");
            pair.Add(PathBox("Your first path (baseline)", p.baseline, false));
            pair.Add(PathBox(p.differ ? "Your latest path" : $"Your latest path (loop {p.latest.loop})", p.latest, true));
            card.Add(pair);
            string what = p.differ ? p.change_line : "Both loops point the same way.";
            if (!string.IsNullOrWhiteSpace(what))
            {
                var l = new Label(what);
                l.AddToClassList("loop-change");
                card.Add(l);
            }
            if (p.differ && !string.IsNullOrWhiteSpace(p.reason_line))
            {
                var r = new Label(p.reason_line);
                r.AddToClassList("loop-reason");
                card.Add(r);
            }
            return card;
        }

        private static VisualElement PathBox(string title, LoopPath path, bool latest)
        {
            var box = new VisualElement();
            box.AddToClassList("loop-path");
            if (latest) box.AddToClassList("loop-path--latest");
            var t = new Label(title);
            t.AddToClassList("loop-path-title");
            box.Add(t);
            var code = new Label(string.IsNullOrWhiteSpace(path?.holland_code) ? "---" : path.holland_code);
            code.AddToClassList("history-code");
            box.Add(code);
            var career = new Label(string.IsNullOrWhiteSpace(path?.career) ? "No career result" : path.career);
            career.AddToClassList("loop-path-career");
            box.Add(career);
            return box;
        }

        /// <summary>One finished loop: "Loop 2 · Fire", code, career, remembered skills, keepsakes, what changed.</summary>
        private VisualElement LoopCard(LoopHistoryItem h)
        {
            var card = new VisualElement();
            card.AddToClassList("sn-card");
            card.AddToClassList("loop-card");
            if (h.loop == 1) card.AddToClassList("loop-card--baseline");
            var head = new Label($"Loop {h.loop}  ·  {vc_LoopMeta.ThreatName(h.threat)}" + (h.loop == 1 ? "  ·  baseline" : ""));
            head.AddToClassList("loop-card-head");
            card.Add(head);
            var code = new Label(string.IsNullOrWhiteSpace(h.holland_code) ? "---" : h.holland_code);
            code.AddToClassList("history-code");
            code.AddToClassList("loop-card-code");
            card.Add(code);
            var career = new Label(string.IsNullOrWhiteSpace(h.career) ? "No career result" : h.career);
            career.AddToClassList("loop-path-career");
            card.Add(career);
            var sub = new Label($"{h.passed} passed, {h.failed} failed");
            sub.AddToClassList("history-sub");
            card.Add(sub);
            card.Add(Line("Remembered", h.kept_skills.Count > 0 ? string.Join(", ", h.kept_skills.Select(SkillLabel)) : "nothing yet"));
            card.Add(Line("Carried", h.mementos.Count > 0
                ? string.Join(", ", h.mementos.Select(m => KeepsakeLabel(m) + (h.mementos_lv2.Contains(m) ? " Lv2" : "")))
                : "no keepsakes"));
            if (!string.IsNullOrWhiteSpace(h.change_line))
            {
                var c = new Label(h.change_line + (string.IsNullOrWhiteSpace(h.reason_line) ? "" : " " + h.reason_line));
                c.AddToClassList(h.changed ? "loop-change" : "loop-reason");
                card.Add(c);
            }
            return card;
        }

        private static VisualElement Line(string lead, string text)
        {
            var l = new Label($"{lead}: {text}");
            l.AddToClassList("loop-card-line");
            return l;
        }

        private string SkillLabel(string s) => FindSkill(s)?.DisplayName ?? s;
        private static string KeepsakeLabel(string m) => vc_LoopMeta.KeepsakeById(m)?.name ?? m;

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
