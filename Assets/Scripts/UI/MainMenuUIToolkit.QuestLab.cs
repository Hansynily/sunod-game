using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;

namespace SunodGame.UI
{
    /// <summary>
    /// Debug tools on the main menu (vc_AdminDebug), for admins and no-account sessions:
    ///  - Settings row "Debug tools: Off / On" (off on every launch and login),
    ///  - a "Quest Lab" menu button while it's on: pick any quest and play it in a sandbox.
    /// Built in code, no UXML changes.
    /// </summary>
    public partial class MainMenuUIToolkit
    {
        private VisualElement _debugRow, _labOverlay, _labList, _labTabs, _fairRow, _offlineRow;
        private Button _btnDebugOff, _btnDebugOn, _btnQuestLab, _btnFairOff, _btnFairOn;
        private readonly Dictionary<string, Button[]> _labOptionButtons = new Dictionary<string, Button[]>();
        private string _labStage = "Elementary";

        private static readonly string[] LabStages = { "Elementary", "High School", "College" };

        private void BindQuestLab(VisualElement root)
        {
            // back from a lab play: drop its leftovers so Continue loads the real run
            vc_AdminDebug.EndLab();

            // Settings row, right under the Gameplay row
            VisualElement gameplay = root.Q<VisualElement>("settings-gameplay-row");
            if (gameplay != null && gameplay.parent != null && root.Q<VisualElement>("settings-debug-row") == null)
            {
                _debugRow = new VisualElement { name = "settings-debug-row" };
                _debugRow.AddToClassList("settings-row");
                var label = new Label("Debug tools");
                label.AddToClassList("settings-label");
                _debugRow.Add(label);
                var choices = new VisualElement();
                choices.AddToClassList("settings-choice-row");
                _btnDebugOff = ChoiceButton("Off", () => SetDebugTools(false));
                _btnDebugOn = ChoiceButton("On", () => SetDebugTools(true));
                choices.Add(_btnDebugOff);
                choices.Add(_btnDebugOn);
                _debugRow.Add(choices);
                gameplay.parent.Insert(gameplay.parent.IndexOf(gameplay) + 1, _debugRow);

                // College Fair mode (vc_FairMode): same admin gate as Debug tools, but saved on
                // the device; it applies from the next New Game
                _fairRow = new VisualElement { name = "settings-fair-row" };
                _fairRow.AddToClassList("settings-row");
                var fairLabel = new Label("Fair mode");
                fairLabel.AddToClassList("settings-label");
                _fairRow.Add(fairLabel);
                var fairChoices = new VisualElement();
                fairChoices.AddToClassList("settings-choice-row");
                _btnFairOff = ChoiceButton("Off", () => SetFairMode(false));
                _btnFairOn = ChoiceButton("On", () => SetFairMode(true));
                fairChoices.Add(_btnFairOff);
                fairChoices.Add(_btnFairOn);
                _fairRow.Add(fairChoices);
                gameplay.parent.Insert(gameplay.parent.IndexOf(_debugRow) + 1, _fairRow);

                // offline admin (vc_OfflineSession): this device can play offline as the cached admin
                _offlineRow = new VisualElement { name = "settings-offline-row" };
                _offlineRow.AddToClassList("settings-row");
                var offLabel = new Label("Offline admin");
                offLabel.AddToClassList("settings-label");
                _offlineRow.Add(offLabel);
                var offChoices = new VisualElement();
                offChoices.AddToClassList("settings-choice-row");
                offChoices.Add(ChoiceButton("Forget", () =>
                {
                    vc_OfflineSession.Forget();
                    vc_Sfx.Play("click", 0.6f);
                    RefreshDebugTools();
                }));
                _offlineRow.Add(offChoices);
                gameplay.parent.Insert(gameplay.parent.IndexOf(_fairRow) + 1, _offlineRow);

                // the rows no longer fit every screen: let them scroll under the header
                VisualElement box = gameplay.parent;
                if (box.Q<ScrollView>("settings-scroll") == null)
                {
                    var rows = box.Children().Where(c => c.ClassListContains("settings-row")).ToList();
                    if (rows.Count > 0)
                    {
                        var sv = new ScrollView(ScrollViewMode.Vertical) { name = "settings-scroll" };
                        sv.style.flexShrink = 1;
                        sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                        box.Insert(box.IndexOf(rows[0]), sv);
                        foreach (VisualElement r in rows) sv.Add(r);
                    }
                }
            }

            // "Quest Lab" tile next to the other menu buttons
            VisualElement panel = root.Q<VisualElement>(className: "menu-panel");
            if (panel != null && root.Q<Button>("btn-questlab") == null)
            {
                _btnQuestLab = new Button(OpenQuestLab) { name = "btn-questlab" };
                _btnQuestLab.AddToClassList("menu-btn");
                _btnQuestLab.AddToClassList("sn-btn");
                var well = new VisualElement { pickingMode = PickingMode.Ignore };
                well.AddToClassList("btn-icon-well");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("btn-icon");
                icon.AddToClassList("icon-tutorial");
                well.Add(icon);
                _btnQuestLab.Add(well);
                var text = new Label("Quest Lab") { pickingMode = PickingMode.Ignore };
                text.AddToClassList("btn-label");
                _btnQuestLab.Add(text);
                panel.Add(_btnQuestLab);
            }
            RefreshDebugTools();
        }

        private static Button ChoiceButton(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("sn-btn");
            b.AddToClassList("settings-choice-btn");
            return b;
        }

        private void SetDebugTools(bool on)
        {
            if (!vc_AdminDebug.CanUse) return;
            vc_AdminDebug.Enabled = on;
            vc_Sfx.Play("click", 0.6f);
            RefreshDebugTools();
        }

        private void SetFairMode(bool on)
        {
            if (!vc_AdminDebug.CanUse) return;
            vc_FairMode.Enabled = on;
            vc_Sfx.Play("click", 0.6f);
            RefreshDebugTools();
        }

        private void RefreshDebugTools()
        {
            _fairRow?.SetDisplay(vc_AdminDebug.CanUse);
            _offlineRow?.SetDisplay(SessionState.Instance != null && SessionState.Instance.IsAdmin && vc_OfflineSession.HasCachedAdmin);
            bool fair = vc_FairMode.Enabled;
            _btnFairOff?.EnableInClassList("settings-choice-btn--on", !fair);
            _btnFairOn?.EnableInClassList("settings-choice-btn--on", fair);
            _debugRow?.SetDisplay(vc_AdminDebug.CanUse);
            bool on = vc_AdminDebug.Enabled;
            _btnDebugOff?.EnableInClassList("settings-choice-btn--on", !on);
            _btnDebugOn?.EnableInClassList("settings-choice-btn--on", on);
            _btnQuestLab?.SetDisplay(on);
        }

        // ── the Quest Lab sheet ───────────────────────────────────────────

        private void OpenQuestLab()
        {
            if (!vc_AdminDebug.Enabled) return;
            vc_Sfx.Play("click", 0.6f);
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _labOverlay?.RemoveFromHierarchy();

            _labOverlay = new VisualElement { name = "questlab-overlay" };
            _labOverlay.AddToClassList("sn-scrim");
            _labOverlay.style.position = Position.Absolute;
            _labOverlay.style.left = 0; _labOverlay.style.right = 0; _labOverlay.style.top = 0; _labOverlay.style.bottom = 0;
            _labOverlay.style.alignItems = Align.Center;
            _labOverlay.style.justifyContent = Justify.Center;

            var sheet = new VisualElement();
            sheet.AddToClassList("sn-panel");
            sheet.AddToClassList("sn-sheet");
            sheet.style.width = 940;
            sheet.style.maxWidth = Length.Percent(94);
            sheet.style.maxHeight = Length.Percent(94);
            _labOverlay.Add(sheet);

            var header = new VisualElement();
            header.AddToClassList("sn-header");
            header.style.flexShrink = 0;
            header.style.justifyContent = Justify.SpaceBetween;
            var spacer = new VisualElement();
            spacer.style.width = 72;
            header.Add(spacer);
            var title = new Label("Quest Lab");
            title.AddToClassList("sn-title");
            title.AddToClassList("sn-center");
            title.style.flexGrow = 1;
            header.Add(title);
            var close = vc_UITheme.MakeButton("×", CloseQuestLab, "icon-sm");
            close.style.fontSize = 40;
            header.Add(close);
            sheet.Add(header);

            var note = new Label("Pick a quest to play. Nothing here is saved.");
            note.AddToClassList("sn-caption");
            note.AddToClassList("sn-center");
            note.style.flexShrink = 0;
            note.style.marginBottom = 8;
            sheet.Add(note);

            // options
            var options = new VisualElement();
            options.style.flexShrink = 0;
            options.style.flexDirection = FlexDirection.Row;
            options.style.flexWrap = Wrap.Wrap;
            options.style.justifyContent = Justify.Center;
            options.style.marginBottom = 8;
            options.Add(OptionGroup("Skills", new[] { "Start", "All" }, () => vc_AdminDebug.LabAllSkills ? 1 : 0,
                i => vc_AdminDebug.LabAllSkills = i == 1));
            options.Add(OptionGroup("Level", new[] { "Lv1", "Lv2", "Lv3" }, () => vc_AdminDebug.LabSkillLevel - 1,
                i => vc_AdminDebug.LabSkillLevel = i + 1));
            options.Add(OptionGroup("Timer", new[] { "On", "Frozen" }, () => vc_AdminDebug.LabFreezeTimer ? 1 : 0,
                i => vc_AdminDebug.LabFreezeTimer = i == 1));
            sheet.Add(options);

            // town tabs
            _labTabs = new VisualElement();
            _labTabs.style.flexShrink = 0;
            _labTabs.style.flexDirection = FlexDirection.Row;
            _labTabs.style.justifyContent = Justify.Center;
            _labTabs.style.marginBottom = 8;
            sheet.Add(_labTabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _labList = new VisualElement();
            _labList.style.flexDirection = FlexDirection.Row;
            _labList.style.flexWrap = Wrap.Wrap;
            _labList.style.justifyContent = Justify.Center;
            scroll.Add(_labList);
            sheet.Add(scroll);

            root.Add(_labOverlay);
            BuildLabList();
            vc_UIAnim.Show(sheet, vc_UIAnim.Enter.Pop);
        }

        private void CloseQuestLab()
        {
            vc_Sfx.Play("click", 0.5f);
            _labOverlay?.RemoveFromHierarchy();
            _labOverlay = null;
        }

        private VisualElement OptionGroup(string name, string[] labels, System.Func<int> current, System.Action<int> pick)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginLeft = 10; group.style.marginRight = 10;
            var l = new Label(name);
            l.AddToClassList("sn-caption");
            l.AddToClassList("sn-semibold");
            l.style.marginRight = 6;
            group.Add(l);
            var buttons = new Button[labels.Length];
            void Refresh()
            {
                for (int i = 0; i < buttons.Length; i++) buttons[i].EnableInClassList("settings-choice-btn--on", i == current());
            }
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                buttons[i] = ChoiceButton(labels[i], () => { pick(idx); vc_Sfx.Play("click", 0.5f); Refresh(); });
                buttons[i].style.minWidth = 0;
                buttons[i].style.minHeight = 44;
                buttons[i].style.height = 48;
                buttons[i].style.fontSize = 20;
                buttons[i].style.paddingLeft = 14; buttons[i].style.paddingRight = 14;
                buttons[i].style.marginLeft = 3; buttons[i].style.marginRight = 3;
                group.Add(buttons[i]);
            }
            _labOptionButtons[name] = buttons;
            Refresh();
            return group;
        }

        private void BuildLabList()
        {
            _labTabs.Clear();
            foreach (string stage in LabStages)
            {
                string st = stage;
                Button tab = vc_UITheme.MakeButton(stage, () => { if (_labStage == st) return; vc_Sfx.Play("click", 0.5f); _labStage = st; BuildLabList(); },
                    "small", _labStage == stage ? "gold" : "ghost");
                tab.focusable = false;
                tab.style.minWidth = 0;
                tab.style.marginLeft = 4; tab.style.marginRight = 4;
                _labTabs.Add(tab);
            }

            _labList.Clear();
            List<vc_AdminDebug.LabQuest> quests = vc_AdminDebug.AllQuests()
                .Where(q => q.stage == _labStage)
                .OrderBy(q => "RIASEC".IndexOf(string.IsNullOrEmpty(q.code) ? 'Z' : char.ToUpperInvariant(q.code[0])))
                .ThenBy(q => q.code)
                .ToList();
            if (quests.Count == 0)
            {
                var none = new Label("No quest list. Run SUNOD > Debug > Build Quest Lab List.");
                none.AddToClassList("sn-caption");
                _labList.Add(none);
                return;
            }
            foreach (vc_AdminDebug.LabQuest q in quests)
            {
                vc_AdminDebug.LabQuest quest = q;
                var card = new Button(() => { vc_Sfx.Play("click", 0.6f); vc_AdminDebug.StartLab(quest); });
                card.AddToClassList("sn-card");
                card.style.width = 270;
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.Center;
                card.style.marginLeft = 6; card.style.marginRight = 6; card.style.marginBottom = 10;
                card.style.paddingTop = 8; card.style.paddingBottom = 8;

                string letter = string.IsNullOrEmpty(q.code) ? "?" : q.code.Substring(0, 1).ToUpperInvariant();
                var badge = new Label(q.code) { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("sn-badge");
                badge.style.backgroundColor = "RIASEC".Contains(letter) ? vc_UITheme.Riasec(letter) : vc_UITheme.InkMuted;
                badge.style.color = Color.white;
                badge.style.minWidth = 54;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.marginRight = 10;
                card.Add(badge);

                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.style.flexShrink = 1;
                var name = new Label(q.quest) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("sn-body");
                name.AddToClassList("sn-semibold");
                name.style.whiteSpace = WhiteSpace.Normal;
                name.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(name);
                var who = new Label(q.npc) { pickingMode = PickingMode.Ignore };
                who.AddToClassList("sn-caption");
                who.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(who);
                card.Add(col);
                _labList.Add(card);
            }
            vc_UIAnim.Stagger(_labList.Children(), vc_UIAnim.Enter.Pop, 0.02f, 0f);
        }
    }
}
