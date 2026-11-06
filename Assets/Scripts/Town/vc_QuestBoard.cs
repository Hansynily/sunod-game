using System;
using System.Collections.Generic;
using SunodGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The plaza quest board and the quest journal (TASKS §3, 2026-11-07).
///
///  - Quest board (the board at the plaza): one poster per person asking for help TODAY (the
///    same people standing around town with a "!"). Accept puts the request in your journal.
///    The board's other tab has the town's notices (rumors, fiesta, everyday notes).
///  - Quest journal (the Journal button): the requests you accepted and haven't done yet.
///    "Go" takes you straight to that person (a quick fade); talk to them to start.
///  Accepting costs nothing; starting the quest uses energy as before. Talking to someone
///  directly still works too. Accepted requests are saved ("acc:" markers), so Continue keeps
///  them. Added to the town root by vc_TownStage; draws into vc_RunProgressUI's overlay.
///
/// UI V2: sn-scrim + sn-panel sheet, sn-card posters, round close button; styles for the
/// qb-* classes live in RunProgress.uss (the overlay's document). Opens with a scrim fade +
/// sheet pop and the cards cascade in.
/// </summary>
[DisallowMultipleComponent]
public class vc_QuestBoard : MonoBehaviour
{
    public const string AcceptedPrefix = "acc:";

    public static vc_QuestBoard Current { get; private set; }

    private vc_TownStage _stage;
    private VisualElement _overlay, _panel, _list, _tabs;
    private Label _title, _footer;
    private Button _tabRequests, _tabNotices;
    private string _notices = "";
    private bool _journal;
    private bool _showNotices;
    private bool _open;

    private void Awake()
    {
        Current = this;
        _stage = GetComponent<vc_TownStage>();
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (_overlay != null) _overlay.RemoveFromHierarchy();
    }

    public bool IsOpen => _overlay != null && _open;

    private string StageId => _stage != null ? _stage.stageId : "town";
    private string Marker(vc_TownNPC n) => $"{AcceptedPrefix}{StageId}:{n.QuestId}";

    public bool IsAccepted(vc_TownNPC n) => n != null && vc_TownFlow.IsQuestDone(Marker(n));

    /// <summary>Accepted, not played yet: what the journal lists.</summary>
    public List<vc_TownNPC> JournalEntries()
    {
        var list = new List<vc_TownNPC>();
        if (_stage == null) return list;
        foreach (vc_TownNPC n in _stage.QuestGivers)
            if (n.HasQuest && !n.Attempted && IsAccepted(n)) list.Add(n);
        return list;
    }

    // ── opening ───────────────────────────────────────────────────────────

    /// <summary>The plaza board: today's request posters, plus the notices tab.</summary>
    public void OpenBoard(string notices)
    {
        _notices = notices ?? "";
        _journal = false;
        _showNotices = false;
        Open();
    }

    public void OpenJournal()
    {
        _journal = true;
        _showNotices = false;
        Open();
    }

    private void Open()
    {
        if (!Build()) return;
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);
        _open = true;
        _overlay.pickingMode = PickingMode.Position;
        _overlay.BringToFront();
        vc_UIAnim.Show(_overlay, vc_UIAnim.Enter.Fade);
        vc_UIAnim.Show(_panel, vc_UIAnim.Enter.Pop, 0.04f);
        PlayerController p = FindFirstObjectByType<PlayerController>();
        p?.MoveAction.Disable();
        Refresh(true);
    }

    public void Close()
    {
        if (_overlay == null) return;
        _open = false;
        _overlay.pickingMode = PickingMode.Ignore;   // let taps through while it fades out
        vc_UIAnim.Hide(_panel, null, vc_UIAnim.Exit.Shrink);
        vc_UIAnim.Hide(_overlay, null, vc_UIAnim.Exit.Fade, 0.06f);
        PlayerController p = FindFirstObjectByType<PlayerController>();
        p?.MoveAction.Enable();
    }

    // ── content ───────────────────────────────────────────────────────────

    private void Refresh(bool animate = false)
    {
        Fill();
        if (animate) vc_UIAnim.Stagger(_list.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.12f);
    }

    private void Fill()
    {
        _list.Clear();
        _tabs.style.display = _journal ? DisplayStyle.None : DisplayStyle.Flex;
        SetTab(_tabRequests, !_showNotices);
        SetTab(_tabNotices, _showNotices);

        if (_journal)
        {
            _title.text = "Journal";
            List<vc_TownNPC> entries = JournalEntries();
            foreach (vc_TownNPC n in entries) _list.Add(Card(n, true));
            if (entries.Count == 0) _list.Add(Note("Nothing yet. Accept requests at the plaza board."));
            _footer.text = entries.Count > 0 ? "Go takes you there. Talk to start (uses energy)." : "";
            return;
        }

        _title.text = "Town Board";
        int open = 0;
        if (_stage != null)
            foreach (vc_TownNPC n in _stage.TodayRequests) if (n.HasQuest && !n.Attempted) open++;
        _tabRequests.text = open > 0 ? $"Requests ({open})" : "Requests";

        if (_showNotices)
        {
            _list.Add(Note(string.IsNullOrEmpty(_notices) ? "No notices today." : _notices));
            _footer.text = "";
            return;
        }

        int shown = 0;
        if (_stage != null)
            foreach (vc_TownNPC n in _stage.TodayRequests)
            {
                if (!n.HasQuest || n.Attempted) continue;
                _list.Add(Card(n, false));
                shown++;
            }
        if (shown == 0) _list.Add(Note("No new requests. Come back tomorrow!"));
        _footer.text = shown > 0 ? "Accepted requests go in your Journal." : "";
    }

    private VisualElement Card(vc_TownNPC n, bool journalCard)
    {
        var card = new VisualElement();
        card.AddToClassList("sn-card");
        card.AddToClassList("qb-card");

        var text = new VisualElement();
        text.AddToClassList("qb-card-text");
        var name = new Label(n.npcName);
        name.AddToClassList("sn-body");
        name.AddToClassList("qb-name");
        text.Add(name);
        var ask = new Label(n.askLine);
        ask.AddToClassList("sn-caption");
        ask.AddToClassList("qb-ask");
        text.Add(ask);
        // the level only, never the kind: picking the skill is the player's call
        var sub = new Label($"Rec. Lv{vc_SkillLevels.RecommendedLevel(vc_SkillLevels.StageIndex, n.questCode)}");
        sub.AddToClassList("sn-badge");
        sub.AddToClassList("qb-rec");
        text.Add(sub);
        card.Add(text);

        bool accepted = IsAccepted(n);
        string label = journalCard ? "Go" : accepted ? "Accepted" : "Accept";
        Button act = Btn(label, () =>
        {
            if (journalCard) { Close(); _stage?.TeleportTo(n); return; }
            if (IsAccepted(n)) return;
            Accept(n);
            Refresh();
        });
        act.AddToClassList("qb-act");
        if (journalCard || !accepted) act.AddToClassList("sn-btn--primary");   // the card's one action
        act.SetEnabled(journalCard || !accepted);
        card.Add(act);
        return card;
    }

    private void Accept(vc_TownNPC n)
    {
        string m = Marker(n);
        vc_TownFlow.MarkQuestDone(m);
        vc_QuestRoom.SaveProgressIds(new[] { m }, gameObject.scene.name);
        SunodGame.Core.vc_Sfx.Play("stamp", 0.6f, 0.05f);
        vc_RunProgressUI.Instance?.ShowStamp("Accepted!", vc_UITheme.Green, -0.9f);
    }

    private static Label Note(string text)
    {
        var l = new Label(text);
        l.AddToClassList("sn-body");
        l.AddToClassList("qb-note");
        return l;
    }

    // ── building the panel (once) ─────────────────────────────────────────

    private bool Build()
    {
        if (_overlay != null) return true;
        VisualElement root = vc_RunProgressUI.Instance != null ? vc_RunProgressUI.Instance.OverlayRoot : null;
        if (root == null) return false;

        _overlay = new VisualElement { name = "quest-board" };
        _overlay.AddToClassList("sn-scrim");
        _overlay.pickingMode = PickingMode.Position;
        _overlay.style.display = DisplayStyle.None;
        root.Add(_overlay);

        _panel = new VisualElement();
        _panel.AddToClassList("sn-panel");
        _panel.AddToClassList("sn-sheet");
        _panel.AddToClassList("qb-panel");
        _overlay.Add(_panel);

        // title centred, round close button top-right
        var head = new VisualElement();
        head.AddToClassList("qb-head");
        var spacer = new VisualElement();
        spacer.AddToClassList("qb-head-spacer");
        head.Add(spacer);
        _title = new Label("Town Board");
        _title.AddToClassList("sn-title");
        _title.AddToClassList("qb-title");
        head.Add(_title);
        Button close = Btn("×", Close, "icon-sm");
        close.AddToClassList("qb-close");
        head.Add(close);
        _panel.Add(head);

        _tabs = new VisualElement();
        _tabs.AddToClassList("qb-tabs");
        _tabRequests = Btn("Requests", () => { _showNotices = false; Refresh(true); }, "small");
        _tabNotices = Btn("Notices", () => { _showNotices = true; Refresh(true); }, "small");
        _tabRequests.AddToClassList("qb-tab");
        _tabNotices.AddToClassList("qb-tab");
        _tabs.Add(_tabRequests);
        _tabs.Add(_tabNotices);
        _panel.Add(_tabs);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("qb-list");
        _list = scroll.contentContainer;
        _panel.Add(scroll);

        _footer = new Label("");
        _footer.AddToClassList("sn-caption");
        _footer.AddToClassList("qb-footer");
        _panel.Add(_footer);
        return true;
    }

    private static Button Btn(string text, Action onClick, params string[] variants)
    {
        return vc_UITheme.MakeButton(text, () => { SunodGame.Core.vc_Sfx.Play("click", 0.6f); onClick?.Invoke(); }, variants);
    }

    private static void SetTab(Button b, bool on) => b.EnableInClassList("qb-tab--on", on);
}
