using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;

/// <summary>
/// The loop's big moments, shown full-screen over whatever scene is running, in the game's
/// own style (pixel font from the panel theme, wood / cream / violet):
///  - Remember: inside the going-back cutscene, the skills you used this life float up as
///    cards and you hold on to a few (no menus),
///  - Wake up: the first morning of a new loop - "Loop 2", the skills you remembered pop in
///    and fly down to your skill buttons, then the keepsakes you still carry,
///  - Gift: a townsperson hands you their keepsake (it stays with you every loop),
///  - Pocket: the keepsakes you carry, as small icons at the top of the town screen.
/// Built in code on its own UIDocument (borrowing the scene's PanelSettings), so no scene
/// setup is needed. Lives across scene loads.
/// </summary>
public class vc_LoopMoments : MonoBehaviour
{
    private static vc_LoopMoments _instance;

    public static vc_LoopMoments Instance
    {
        get
        {
            if (_instance != null) return _instance;
            PanelSettings panel = null;
            foreach (UIDocument d in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (d.panelSettings != null) { panel = d.panelSettings; break; }
            if (panel == null) return null;
            var go = new GameObject("[Loop Moments]");
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 900;
            _instance = go.AddComponent<vc_LoopMoments>();
            return _instance;
        }
    }

    /// <summary>The overlay if it already exists (never creates one).</summary>
    public static vc_LoopMoments Existing => _instance;

    public bool IsShowing => _stage != null && _stage.style.display == DisplayStyle.Flex;

    private static readonly Color Ink = new Color32(60, 54, 66, 255);
    private static readonly Color Cream = new Color32(247, 236, 214, 255);
    private static readonly Color Gold = new Color32(255, 214, 90, 255);
    private static readonly Color Violet = new Color32(176, 140, 255, 255);
    private static readonly Color DeepViolet = new Color32(60, 40, 110, 255);
    private static readonly Color Wood = new Color32(170, 121, 89, 255);

    private VisualElement _root, _stage, _pocket;
    private readonly List<VisualElement> _twinkles = new List<VisualElement>();

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;

        _stage = new VisualElement { name = "loop-stage" };
        Fill(_stage);
        _stage.style.alignItems = Align.Center;
        _stage.style.justifyContent = Justify.Center;
        _stage.style.display = DisplayStyle.None;
        _root.Add(_stage);

        _pocket = new VisualElement { name = "loop-pocket", pickingMode = PickingMode.Position };
        _pocket.style.position = Position.Absolute;
        _pocket.style.flexDirection = FlexDirection.Row;
        _pocket.style.alignItems = Align.Center;
        _pocket.style.display = DisplayStyle.None;        _root.Add(_pocket);

        vc_LoopMeta.Changed += RefreshPocket;
    }

    private void OnDisable() => vc_LoopMeta.Changed -= RefreshPocket;

    private void Update()
    {
        // stars behind the big moments twinkle in two steps (pixel style)
        for (int i = 0; i < _twinkles.Count; i++)
            _twinkles[i].style.opacity = Mathf.Repeat(Time.unscaledTime * 0.7f + i * 0.37f, 1f) < 0.5f ? 1f : 0.3f;
    }

    // ══ Remember (inside the going-back cutscene) ════════════════════════════════════

    /// <summary>
    /// "What will you remember?" Skills float up as cards; tap to hold on to up to
    /// <paramref name="slots"/>. <paramref name="confirm"/> gets the held skill ids and a
    /// callback: null = success (the moment closes and <paramref name="done"/> runs), or an
    /// error to show (the player can try again).
    /// </summary>
    public void ShowRemember(List<vc_SkillData> candidates, int slots, List<string> preselected,
                             Action<List<string>, Action<string>> confirm, Action done)
    {
        StopAllCoroutines();
        BeginStage(new Color(0.055f, 0.047f, 0.133f, 0.97f), stars: true);

        var held = new List<string>();
        foreach (string s in preselected ?? new List<string>())
            if (held.Count < slots && candidates.Exists(c => c.skillName == s)) held.Add(s);

        var title = Text("What will you remember?", 44, Gold);
        title.style.marginBottom = 6;
        _stage.Add(title);
        var sub = Text("", 24, Cream);
        sub.style.marginBottom = 26;
        _stage.Add(sub);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.style.maxWidth = Length.Percent(92);
        _stage.Add(row);

        var cards = new List<(vc_SkillData skill, VisualElement card)>();
        void Refresh()
        {
            sub.text = candidates.Count == 0
                ? "Nothing new to remember."
                : $"{held.Count} / {slots}";
            foreach (var (skill, card) in cards)
                SetHeld(card, held.Contains(skill.skillName), held.Count >= slots);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            vc_SkillData skill = candidates[i];
            VisualElement card = SkillCard(skill, 150);
            card.style.marginLeft = 12; card.style.marginRight = 12; card.style.marginBottom = 18;
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (held.Remove(skill.skillName)) { vc_Sfx.Play("click", 0.5f, 0.05f); Refresh(); return; }
                if (held.Count >= slots) { vc_Sfx.Play("click", 0.3f); Shake(card); return; }
                held.Add(skill.skillName);
                vc_Sfx.Play("page_next", 0.7f, 0.05f);
                Burst(card, Violet, 10);
                Refresh();
            });
            row.Add(card);
            cards.Add((skill, card));
            StartCoroutine(Float(card, i * 0.6f));
            StartCoroutine(PopIn(card, 0.15f + i * 0.08f));
        }

        var error = Text("", 22, new Color32(255, 150, 130, 255));
        error.style.marginTop = 6;
        _stage.Add(error);

        Button go = WoodButton(candidates.Count == 0 ? "Go back" : "Remember these");
        go.style.marginTop = 10;
        bool busy = false;
        go.clicked += () =>
        {
            if (busy) return;
            busy = true;
            go.SetEnabled(false);
            error.text = "";
            vc_Sfx.Play("click", 0.6f);
            confirm(new List<string>(held), err =>
            {
                if (err != null)
                {
                    busy = false;
                    go.SetEnabled(true);
                    error.text = err;
                    return;
                }
                StartCoroutine(RememberOutro(cards, held, done));
            });
        };
        _stage.Add(go);
        Refresh();
        StartCoroutine(FadeStage(0f, 1f, 0.4f));
    }

    private IEnumerator RememberOutro(List<(vc_SkillData skill, VisualElement card)> cards, List<string> held, Action done)
    {
        vc_Sfx.Play("cut_whoosh", 0.6f);
        // what you let go of falls away; what you hold rises into the light
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.9f);
            foreach (var (skill, card) in cards)
            {
                bool keep = held.Contains(skill.skillName);
                card.style.opacity = keep ? 1f - k * k : 1f - k;
                card.style.translate = new Translate(0, keep ? -120f * k * k : 60f * k);
                float s = keep ? 1f + 0.15f * Mathf.Sin(k * Mathf.PI) : 1f - 0.3f * k;
                card.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            yield return null;
        }
        yield return FadeStage(1f, 0f, 0.3f);
        EndStage();
        done?.Invoke();
    }

    private static void SetHeld(VisualElement card, bool held, bool full)
    {
        Color edge = held ? Violet : new Color32(150, 130, 120, 255);
        Border(card, held ? 5 : 3, edge, held ? DeepViolet : new Color32(100, 85, 82, 255));
        card.style.backgroundColor = held ? (Color)new Color32(234, 224, 255, 255) : Cream;
        card.style.opacity = held || !full ? 1f : 0.55f;
        var mark = card.Q<Label>("held-mark");
        if (mark != null) mark.style.display = held ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ══ Wake up (first morning of a new loop) ════════════════════════════════════════

    public void ShowWakeUp(int loop, string threat, List<vc_SkillData> remembered, List<vc_LoopMeta.Keepsake> keepsakes, Action done)
    {
        StopAllCoroutines();
        BeginStage(new Color(0.055f, 0.047f, 0.133f, 0.94f), stars: true);
        StartCoroutine(WakeUpRoutine(loop, threat, remembered, keepsakes, done));
    }

    private IEnumerator WakeUpRoutine(int loop, string threat, List<vc_SkillData> remembered, List<vc_LoopMeta.Keepsake> keepsakes, Action done)
    {
        yield return FadeStage(0f, 1f, 0.5f);

        var big = Text($"LOOP {loop}", 72, Gold);
        _stage.Add(big);
        vc_Sfx.Play("step_done", 0.8f);
        yield return PopIn(big, 0f);

        var line = Text("You remember.", 26, Cream);
        line.style.marginTop = 4;
        line.style.marginBottom = 22;
        _stage.Add(line);
        yield return FadeIn(line, 0.4f);

        var cards = new List<VisualElement>();
        if (remembered.Count > 0)
        {
            var row = Row();
            _stage.Add(row);
            foreach (vc_SkillData s in remembered)
            {
                VisualElement card = SkillCard(s, 128);
                SetHeld(card, true, false);
                card.style.marginLeft = 10; card.style.marginRight = 10;
                row.Add(card);
                cards.Add(card);
                vc_Sfx.Play("page_next", 0.7f, 0.06f);
                Burst(card, Violet, 12);
                yield return PopIn(card, 0f);
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }

        if (keepsakes.Count > 0)
        {
            var label = Text("Keepsakes", 22, Cream);
            label.style.marginTop = 20;
            _stage.Add(label);
            var row = Row();
            row.style.marginTop = 8;
            _stage.Add(row);
            foreach (vc_LoopMeta.Keepsake k in keepsakes)
            {
                VisualElement chip = KeepsakeChip(k, 56, showName: true);
                chip.style.marginLeft = 10; chip.style.marginRight = 10;
                row.Add(chip);
                yield return PopIn(chip, 0f);
            }
        }

        string warn = ThreatWarning(threat);
        if (!string.IsNullOrEmpty(warn))
        {
            var w = Text(warn, 22, new Color32(255, 190, 150, 255));
            w.style.marginTop = 22;
            _stage.Add(w);
            yield return FadeIn(w, 0.5f);
        }

        Button begin = WoodButton("Begin");
        begin.style.marginTop = 24;
        bool go = false;
        begin.clicked += () => { vc_Sfx.Play("click", 0.6f); go = true; };
        _stage.Add(begin);
        while (!go) yield return null;
        begin.SetEnabled(false);

        // the remembered skills fly down to the skill buttons (bottom-right)
        vc_Sfx.Play("cut_whoosh", 0.5f);
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.7f);
            float e = k * k;
            foreach (VisualElement c in cards)
            {
                c.style.translate = new Translate(520f * e, 320f * e);
                float s = 1f - 0.7f * e;
                c.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            _stage.style.opacity = 1f - e;
            yield return null;
        }
        EndStage();
        RefreshPocket();
        done?.Invoke();
    }

    private static string ThreatWarning(string threat) => threat switch
    {
        "typhoon" => "Threat: Typhoon",
        "flood" => "Threat: Flood",
        "earthquake" => "Threat: Earthquake",
        "fire" => "Threat: Fire",
        _ => "",
    };

    // ══ Keepsake Shop (the stall in town) ═════════════════════════════════════════════

    private VisualElement _shopList;
    private Label _shopStars, _shopError;
    private PlayerController _shopPlayer;

    public void ShowShop()
    {
        StopAllCoroutines();
        BeginStage(new Color(0.02f, 0.02f, 0.06f, 0.75f), stars: false);
        _shopPlayer = FindFirstObjectByType<PlayerController>();
        _shopPlayer?.MoveAction.Disable();

        var card = new VisualElement();
        card.style.width = 700;
        card.style.maxWidth = Length.Percent(92);
        card.style.maxHeight = Length.Percent(92);
        card.style.paddingTop = 16; card.style.paddingBottom = 18;
        card.style.paddingLeft = 24; card.style.paddingRight = 24;
        card.style.backgroundColor = Cream;
        Border(card, 5, Wood, new Color32(100, 85, 82, 255));
        _stage.Add(card);

        card.Add(Text("Keepsake Shop", 34, Ink));
        _shopStars = Text("", 24, new Color32(168, 92, 30, 255));
        _shopStars.style.marginBottom = 8;
        card.Add(_shopStars);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _shopList = new VisualElement();
        scroll.Add(_shopList);
        card.Add(scroll);

        _shopError = Text("", 20, new Color32(190, 60, 50, 255));
        card.Add(_shopError);

        Button leave = WoodButton("Leave");
        leave.style.alignSelf = Align.Center;
        leave.style.marginTop = 8;
        leave.clicked += CloseShop;
        card.Add(leave);

        BuildShopRows();
        StartCoroutine(FadeStage(0f, 1f, 0.2f));
        vc_LoopMeta.Fetch(_ => { if (_shopList != null && IsShowing) { vc_LoopMeta.MarkShopSeen(); BuildShopRows(); } });
    }

    private void CloseShop()
    {
        vc_Sfx.Play("click", 0.5f);
        _shopList = null;
        EndStage();
        _shopPlayer?.MoveAction.Enable();
        RefreshPocket();
    }

    private void BuildShopRows()
    {
        if (_shopList == null) return;
        _shopList.Clear();
        var meta = vc_LoopMeta.Current;
        _shopStars.text = meta != null ? $"Stars: {meta.stars}" : "";
        if (meta == null) { _shopError.text = "Couldn't reach the server."; return; }

        foreach (vc_LoopMeta.Keepsake k in vc_LoopMeta.Keepsakes)
        {
            bool owned = meta.owned_mementos.Contains(k.id);
            bool unlocked = owned || meta.unlocked_mementos.Contains(k.id);
            int cost = vc_LoopMeta.Price(k.id);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 2;
            row.style.borderBottomColor = (Color)new Color32(214, 190, 150, 255);
            row.style.opacity = unlocked ? 1f : 0.6f;

            VisualElement icon = Icon(k.id, 44);
            icon.style.marginRight = 12;
            icon.style.flexShrink = 0;
            if (!unlocked) icon.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.9f);
            row.Add(icon);

            var body = new VisualElement();
            body.style.flexGrow = 1; body.style.flexShrink = 1;
            var title = Text(unlocked ? k.name : "???", 24, Ink);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(title);
            var sub = Text(unlocked ? k.effect : $"Help {k.giver} to unlock.", 20, new Color32(110, 84, 58, 255));
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(sub);
            row.Add(body);

            Button btn = WoodButton(owned ? "Owned" : !unlocked ? "Locked" : $"{cost} stars");
            btn.style.minWidth = 150; btn.style.height = 52; btn.style.fontSize = 22;
            btn.style.flexShrink = 0;
            if (owned || !unlocked || meta.stars < cost) btn.SetEnabled(false);
            string id = k.id;
            btn.clicked += () => BuyKeepsake(id);
            row.Add(btn);
            _shopList.Add(row);
        }
    }

    private void BuyKeepsake(string id)
    {
        var tm = SunodGame.Telemetry.TelemetryManager.Instance;
        if (tm == null) return;
        vc_Sfx.Play("click", 0.6f);
        _shopError.text = "";
        tm.BuyMemento(id,
            meta =>
            {
                vc_LoopMeta.Set(meta);
                vc_Sfx.Play("step_done", 0.8f);
                BuildShopRows();
            },
            _ => { if (_shopError != null) _shopError.text = "Couldn't buy it."; });
    }

    // ══ Unlocked (small notice: a keepsake is now in the shop) ═══════════════════════

    private VisualElement _notice;

    public void ShowUnlocked(vc_LoopMeta.Keepsake k)
    {
        if (k == null) return;
        _notice?.RemoveFromHierarchy();
        _notice = new VisualElement { pickingMode = PickingMode.Ignore };
        _notice.style.position = Position.Absolute;
        _notice.style.top = 90;
        _notice.style.left = Length.Percent(50);
        _notice.style.translate = new Translate(Length.Percent(-50), 0);
        _notice.style.flexDirection = FlexDirection.Row;
        _notice.style.alignItems = Align.Center;
        _notice.style.paddingLeft = 14; _notice.style.paddingRight = 18;
        _notice.style.paddingTop = 8; _notice.style.paddingBottom = 10;
        _notice.style.backgroundColor = Cream;
        Border(_notice, 4, Wood, new Color32(100, 85, 82, 255));
        VisualElement icon = Icon(k.id, 44);
        icon.style.marginRight = 10;
        _notice.Add(icon);
        var col = new VisualElement { pickingMode = PickingMode.Ignore };
        var head = Text("NEW IN THE SHOP", 18, new Color32(168, 92, 30, 255));
        head.style.unityTextAlign = TextAnchor.MiddleLeft;
        col.Add(head);
        var name = Text(k.name, 26, Ink);
        name.style.unityTextAlign = TextAnchor.MiddleLeft;
        col.Add(name);
        _notice.Add(col);
        _root.Add(_notice);
        vc_Sfx.Play("step_done", 0.7f);
        StartCoroutine(NoticeRoutine(_notice));
    }

    private IEnumerator NoticeRoutine(VisualElement n)
    {
        yield return PopIn(n, 0f);
        Burst(n, Gold, 10);
        yield return new WaitForSecondsRealtime(3f);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            n.style.opacity = 1f - t / 0.4f;
            yield return null;
        }
        n.RemoveFromHierarchy();
        if (_notice == n) _notice = null;
    }

    // ══ Pocket (keepsakes, small, under the quest panel) ═══════════════════════════

    private bool _pocketWanted;
    private VisualElement _questPanel;

    public void SetPocketVisible(bool visible)
    {
        if (_pocketWanted == visible) return;
        _pocketWanted = visible;
        RefreshPocket();
    }

    private void LateUpdate()
    {
        if (_pocket == null || _pocket.style.display == DisplayStyle.None) return;
        if (_questPanel == null || _questPanel.panel == null)
        {
            _questPanel = null;
            foreach (UIDocument d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                VisualElement qp = d.rootVisualElement?.Q<VisualElement>("quest-panel");
                if (qp != null && qp.resolvedStyle.display != DisplayStyle.None) { _questPanel = qp; break; }
            }
        }
        if (_questPanel == null) { _pocket.style.left = 24; _pocket.style.top = 24; return; }
        Rect r = _questPanel.worldBound;
        Vector2 p = _root.WorldToLocal(new Vector2(r.xMin, r.yMax));
        _pocket.style.left = p.x;
        _pocket.style.top = p.y + 6f;
    }

    private void RefreshPocket()
    {
        if (_pocket == null) return;
        _pocket.Clear();
        var owned = vc_LoopMeta.Current != null ? vc_LoopMeta.Current.owned_mementos : null;
        bool show = _pocketWanted && owned != null && owned.Count > 0 && !IsShowing;
        _pocket.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;
        foreach (string id in owned)
        {
            vc_LoopMeta.Keepsake k = vc_LoopMeta.KeepsakeById(id);
            if (k == null) continue;
            VisualElement icon = Icon(id, 26);
            icon.style.marginRight = 4;
            icon.pickingMode = PickingMode.Position;
            icon.RegisterCallback<ClickEvent>(_ =>
            {
                vc_Sfx.Play("click", 0.5f);
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"{k.name}: {k.effect}", 3f);
            });
            _pocket.Add(icon);
        }
    }
    // ══ building blocks ════════════════════════════════════════════════════════════

    private void BeginStage(Color backdrop, bool stars)
    {
        _stage.Clear();
        _twinkles.Clear();
        _stage.style.backgroundColor = backdrop;
        _stage.style.opacity = 0f;
        _stage.style.display = DisplayStyle.Flex;
        _stage.pickingMode = PickingMode.Position;
        _pocket.style.display = DisplayStyle.None;
        if (!stars) return;
        var rng = new System.Random(7);
        var sky = new VisualElement { pickingMode = PickingMode.Ignore };
        Fill(sky);
        for (int i = 0; i < 46; i++)
        {
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.style.position = Position.Absolute;
            s.style.left = Length.Percent(rng.Next(0, 100));
            s.style.top = Length.Percent(rng.Next(0, 100));
            int size = rng.Next(0, 4) == 0 ? 6 : 3;
            s.style.width = size; s.style.height = size;
            s.style.backgroundColor = rng.Next(0, 3) == 0 ? Violet : (Color)new Color32(220, 214, 255, 255);
            sky.Add(s);
            _twinkles.Add(s);
        }
        _stage.Add(sky);
    }

    private void EndStage()
    {
        _stage.style.display = DisplayStyle.None;
        _stage.pickingMode = PickingMode.Ignore;
        _stage.Clear();
        _twinkles.Clear();
    }

    private VisualElement SkillCard(vc_SkillData skill, float width)
    {
        string letter = (skill.riaSecLetter ?? "?").Trim().ToUpperInvariant();
        var card = new VisualElement();
        card.style.width = width;
        card.style.alignItems = Align.Center;
        card.style.paddingBottom = 12;
        card.style.backgroundColor = Cream;
        Border(card, 3, new Color32(150, 130, 120, 255), new Color32(100, 85, 82, 255));

        var strip = Text($"{letter}  {vc_SkillManager.CategoryNameForSlot(vc_SkillManager.SlotIndexForLetter(letter))}".ToUpperInvariant(), 16, Color.white);
        strip.style.alignSelf = Align.Stretch;
        strip.style.backgroundColor = KindColor(letter);
        strip.style.paddingTop = 4; strip.style.paddingBottom = 4;
        card.Add(strip);

        var sym = new VisualElement { pickingMode = PickingMode.Ignore };
        sym.style.width = width * 0.48f; sym.style.height = width * 0.48f;
        sym.style.marginTop = 10; sym.style.marginBottom = 6;
        sym.style.backgroundImage = SunodGame.UI.vc_RiasecSymbols.Background(letter);
        sym.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        card.Add(sym);

        var name = Text((skill.DisplayName ?? skill.skillName).ToUpperInvariant(), 20, Ink);
        card.Add(name);

        // Skill level: remembering a skill keeps it (SKILL_SYSTEM_DESIGN.md §6)
        int level = vc_SkillLevels.CarriedLevel(skill);
        var lv = Text(level >= vc_SkillLevels.MaxLevel ? $"Lv{level} (max)" : $"Lv{level}", 15,
            level >= vc_SkillLevels.MaxLevel ? (Color)new Color32(170, 120, 0, 255) : level == 2 ? (Color)new Color32(90, 100, 130, 255) : Ink);
        lv.style.marginTop = 2;
        card.Add(lv);

        var mark = Text("REMEMBERED", 14, Color.white);
        mark.name = "held-mark";
        mark.style.position = Position.Absolute;
        mark.style.top = -14;
        mark.style.backgroundColor = DeepViolet;
        mark.style.paddingLeft = 8; mark.style.paddingRight = 8; mark.style.paddingTop = 2; mark.style.paddingBottom = 2;
        mark.style.display = DisplayStyle.None;
        card.Add(mark);
        foreach (VisualElement child in card.Children()) child.pickingMode = PickingMode.Ignore;
        return card;
    }

    private VisualElement KeepsakeChip(vc_LoopMeta.Keepsake k, float size, bool showName)
    {
        var chip = new VisualElement { pickingMode = PickingMode.Ignore };
        chip.style.alignItems = Align.Center;
        chip.Add(Icon(k.id, size));
        if (showName)
        {
            var n = Text(k.name, 18, Cream);
            n.style.marginTop = 4;
            chip.Add(n);
        }
        return chip;
    }

    private static VisualElement Icon(string id, float size)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = size; icon.style.height = size;
        Texture2D tex = vc_LoopMeta.KeepsakeIcon(id);
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            icon.style.backgroundImage = new StyleBackground(tex);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        return icon;
    }

    private static Label Text(string text, int size, Color color)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.maxWidth = Length.Percent(100);
        return l;
    }

    private static VisualElement Row()
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.justifyContent = Justify.Center;
        r.style.alignItems = Align.FlexEnd;
        return r;
    }

    private static Button WoodButton(string label)
    {
        var b = new Button { text = label, focusable = false };
        b.style.fontSize = 26;
        b.style.color = Ink;
        b.style.minWidth = 240;
        b.style.height = 64;
        b.style.paddingLeft = 28; b.style.paddingRight = 28;
        b.style.backgroundImage = StyleKeyword.None;
        b.style.backgroundColor = (Color)new Color32(232, 207, 166, 255);
        Border(b, 4, Wood, new Color32(100, 85, 82, 255));
        b.style.borderBottomWidth = 8;
        return b;
    }

    private static void Border(VisualElement e, float width, Color color, Color bottom)
    {
        e.style.borderTopWidth = width; e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
        e.style.borderBottomWidth = width;
        e.style.borderTopColor = color; e.style.borderLeftColor = color; e.style.borderRightColor = color;
        e.style.borderBottomColor = bottom;
        e.style.borderTopLeftRadius = 0; e.style.borderTopRightRadius = 0;
        e.style.borderBottomLeftRadius = 0; e.style.borderBottomRightRadius = 0;
    }

    private static void Fill(VisualElement e)
    {
        e.style.position = Position.Absolute;
        e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
    }

    public static Color KindColor(string letter) => letter switch
    {
        "R" => new Color32(214, 96, 70, 255),
        "I" => new Color32(80, 140, 210, 255),
        "A" => new Color32(176, 110, 210, 255),
        "S" => new Color32(90, 170, 100, 255),
        "E" => new Color32(226, 160, 60, 255),
        "C" => new Color32(110, 120, 150, 255),
        _ => new Color32(120, 104, 98, 255),
    };

    // ── tiny tweens (unscaled time, so they run while the game is paused) ──

    private IEnumerator FadeStage(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _stage.style.opacity = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        _stage.style.opacity = to;
    }

    private static IEnumerator FadeIn(VisualElement e, float seconds)
    {
        e.style.opacity = 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            e.style.opacity = t / seconds;
            yield return null;
        }
        e.style.opacity = 1f;
    }

    /// <summary>Pops in with a little overshoot: 0 -> 1.15 -> 1.</summary>
    private static IEnumerator PopIn(VisualElement e, float delay)
    {
        e.style.scale = new Scale(Vector3.zero);
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        const float dur = 0.28f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            e.style.scale = new Scale(new Vector3(s, s, 1f));
            yield return null;
        }
        e.style.scale = new Scale(Vector3.one);
    }

    /// <summary>Gentle bob while shown (whole pixels).</summary>
    private static IEnumerator Float(VisualElement e, float phase)
    {
        while (e.panel != null)
        {
            float y = Mathf.Round(Mathf.Sin((Time.unscaledTime + phase) * 2.2f) * 4f);
            e.style.translate = new Translate(0, y);
            yield return null;
        }
    }

    private void Shake(VisualElement e) => StartCoroutine(ShakeRoutine(e));

    private static IEnumerator ShakeRoutine(VisualElement e)
    {
        for (int i = 0; i < 6; i++)
        {
            e.style.translate = new Translate(i % 2 == 0 ? 6 : -6, 0);
            yield return new WaitForSecondsRealtime(0.04f);
        }
        e.style.translate = new Translate(0, 0);
    }

    /// <summary>Little square sparks flying out from an element's centre.</summary>
    private void Burst(VisualElement from, Color color, int count)
    {
        if (from == null) return;
        StartCoroutine(BurstRoutine(from, color, count));
    }

    private IEnumerator BurstRoutine(VisualElement from, Color color, int count)
    {
        yield return null;   // let layout settle
        Rect r = from.worldBound;
        Vector2 c = _root.WorldToLocal(r.center);
        var sparks = new List<(VisualElement e, Vector2 v)>();
        for (int i = 0; i < count; i++)
        {
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.style.position = Position.Absolute;
            s.style.width = 8; s.style.height = 8;
            s.style.backgroundColor = color;
            s.style.left = c.x; s.style.top = c.y;
            _root.Add(s);
            float a = (i / (float)count) * Mathf.PI * 2f;
            sparks.Add((s, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(120f, 200f)));
        }
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f;
            foreach (var (e, v) in sparks)
            {
                e.style.translate = new Translate(Mathf.Round(v.x * k), Mathf.Round(v.y * k));
                e.style.opacity = 1f - k;
            }
            yield return null;
        }
        foreach (var (e, _) in sparks) e.RemoveFromHierarchy();
    }
}
