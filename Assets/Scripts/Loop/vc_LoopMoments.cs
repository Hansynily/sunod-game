using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

/// <summary>
/// The loop's big moments, shown full-screen over whatever scene is running, in the UI V2
/// look (sn-* theme classes, Pixelify Sans, vc_UIAnim motion):
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

    // night sky behind the big moments (Remember / Wake up)
    private static readonly Color NightSky = new Color(0.12f, 0.10f, 0.22f, 0.96f);
    private static readonly Color StarDot = new Color32(232, 226, 255, 255);
    private static readonly Color HeldFill = new Color32(238, 230, 255, 255);
    private static readonly Color HeldEdge = new Color32(96, 66, 170, 255);
    private static readonly Color ErrorOnDark = new Color32(255, 170, 150, 255);

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
        _pocket.AddToClassList("sn-chip");
        _pocket.style.position = Position.Absolute;
        _pocket.style.paddingLeft = 10; _pocket.style.paddingRight = 10;
        _pocket.style.display = DisplayStyle.None;
        _root.Add(_pocket);

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
        BeginStage(NightSky, stars: true);

        var held = new List<string>();
        foreach (string s in preselected ?? new List<string>())
            if (held.Count < slots && candidates.Exists(c => c.skillName == s)) held.Add(s);

        var title = Text("What will you remember?", "title", vc_UITheme.Gold);
        title.style.marginBottom = 4;
        _stage.Add(title);
        var sub = Text("", "body", vc_UITheme.CreamHi);
        sub.style.marginBottom = 24;
        _stage.Add(sub);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.style.maxWidth = Length.Percent(92);
        _stage.Add(row);

        var cards = new List<(vc_SkillData skill, VisualElement card)>();
        void Apply(vc_SkillData skill, VisualElement card) =>
            SetHeld(card, held.Contains(skill.skillName), held.Count >= slots);
        void Refresh()
        {
            sub.text = candidates.Count == 0
                ? "Nothing new to remember."
                : $"{held.Count} / {slots}";
            foreach (var (skill, card) in cards) Apply(skill, card);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            vc_SkillData skill = candidates[i];
            // the slot bobs, the card inside pops / shakes / punches (one transform each)
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.style.marginLeft = 12; slot.style.marginRight = 12; slot.style.marginBottom = 18;
            VisualElement card = SkillCard(skill, 150);
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (held.Remove(skill.skillName)) { vc_Sfx.Play("click", 0.5f, 0.05f); Refresh(); return; }
                if (held.Count >= slots) { vc_Sfx.Play("click", 0.3f); vc_UIAnim.Shake(card); return; }
                held.Add(skill.skillName);
                vc_Sfx.Play("page_next", 0.7f, 0.05f);
                Burst(card, vc_UITheme.Violet, 10);
                vc_UIAnim.Punch(card, 0.1f, 0.3f);
                Refresh();
            });
            slot.Add(card);
            row.Add(slot);
            cards.Add((skill, card));
            StartCoroutine(Float(slot, i * 0.6f));
        }
        Refresh();
        for (int i = 0; i < cards.Count; i++)
        {
            var (skill, card) = cards[i];
            vc_UIAnim.Show(card, vc_UIAnim.Enter.Pop, 0.15f + i * 0.06f, () => Apply(skill, card));
        }

        var error = Text("", "caption", ErrorOnDark);
        error.style.marginTop = 6;
        _stage.Add(error);

        Button go = null;
        bool busy = false;
        go = MakeButton(candidates.Count == 0 ? "Go back" : "Remember", () =>
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
                    vc_UIAnim.Shake(error);
                    return;
                }
                StartCoroutine(RememberOutro(cards, held, done));
            });
        }, "primary");
        go.style.marginTop = 10;
        _stage.Add(go);
        vc_UIAnim.Show(go, vc_UIAnim.Enter.SlideUp, 0.3f + cards.Count * 0.06f);
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
        yield return HideStage();
        EndStage();
        done?.Invoke();
    }

    private static void SetHeld(VisualElement card, bool held, bool full)
    {
        if (held)
        {
            vc_UITheme.Border(card, 4f, vc_UITheme.Violet);
            card.style.borderBottomColor = HeldEdge;
            card.style.borderBottomWidth = 7f;
            card.style.backgroundColor = HeldFill;
        }
        else
        {
            // back to the plain sn-card look
            card.style.borderTopWidth = StyleKeyword.Null; card.style.borderBottomWidth = StyleKeyword.Null;
            card.style.borderLeftWidth = StyleKeyword.Null; card.style.borderRightWidth = StyleKeyword.Null;
            card.style.borderTopColor = StyleKeyword.Null; card.style.borderBottomColor = StyleKeyword.Null;
            card.style.borderLeftColor = StyleKeyword.Null; card.style.borderRightColor = StyleKeyword.Null;
            card.style.backgroundColor = StyleKeyword.Null;
        }
        card.style.opacity = held || !full ? 1f : 0.55f;
        var mark = card.Q<Label>("held-mark");
        if (mark != null) mark.style.display = held ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ══ Wake up (first morning of a new loop) ════════════════════════════════════════

    public void ShowWakeUp(int loop, string threat, List<vc_SkillData> remembered, List<vc_LoopMeta.Keepsake> keepsakes, Action done)
    {
        StopAllCoroutines();
        BeginStage(NightSky, stars: true);
        StartCoroutine(WakeUpRoutine(loop, threat, remembered, keepsakes, done));
    }

    private IEnumerator WakeUpRoutine(int loop, string threat, List<vc_SkillData> remembered, List<vc_LoopMeta.Keepsake> keepsakes, Action done)
    {
        yield return new WaitForSecondsRealtime(0.3f);

        var big = Text($"Loop {loop}", "display", vc_UITheme.Gold);
        big.style.fontSize = 72;
        _stage.Add(big);
        vc_Sfx.Play("step_done", 0.8f);
        yield return PopIn(big, 0f);

        var line = Text("You remember.", "body", vc_UITheme.CreamHi);
        line.style.marginTop = 4;
        line.style.marginBottom = 22;
        _stage.Add(line);
        yield return FadeIn(line);

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
                Burst(card, vc_UITheme.Violet, 12);
                yield return PopIn(card, 0f);
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        if (keepsakes.Count > 0)
        {
            var label = Text("Keepsakes", "caption", vc_UITheme.CreamHi);
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
            var w = Text(warn, "body", ErrorOnDark);
            w.style.marginTop = 22;
            _stage.Add(w);
            yield return FadeIn(w);
            vc_UIAnim.Punch(w, 0.08f, 0.3f);
        }

        bool go = false;
        Button begin = MakeButton("Begin", () => { vc_Sfx.Play("click", 0.6f); go = true; }, "primary");
        begin.style.marginTop = 24;
        _stage.Add(begin);
        vc_UIAnim.Show(begin, vc_UIAnim.Enter.SlideUp);
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

    private VisualElement _shopList, _shopCard;
    private Label _shopStars, _shopError;
    private PlayerController _shopPlayer;

    public void ShowShop()
    {
        StopAllCoroutines();
        BeginStage(vc_UITheme.Scrim, stars: false);
        _shopPlayer = FindFirstObjectByType<PlayerController>();
        _shopPlayer?.MoveAction.Disable();

        var card = new VisualElement();
        card.AddToClassList("sn-panel");
        card.AddToClassList("sn-sheet");
        card.style.width = 720;
        card.style.maxWidth = Length.Percent(92);
        card.style.maxHeight = Length.Percent(92);
        _shopCard = card;
        _stage.Add(card);

        // header: title centred, round close top-right
        var header = new VisualElement();
        header.AddToClassList("sn-header");
        header.style.justifyContent = Justify.SpaceBetween;
        var spacer = new VisualElement();
        spacer.style.width = 72; spacer.style.flexShrink = 0;
        header.Add(spacer);
        var title = Text("Keepsake Shop", "title");
        title.style.flexGrow = 1;
        header.Add(title);
        Button close = MakeButton("×", CloseShop, "icon-sm");
        close.style.marginTop = 0; close.style.marginBottom = 0; close.style.marginRight = 0;
        close.style.fontSize = 40;
        header.Add(close);
        card.Add(header);

        _shopStars = Text("", "body");
        _shopStars.AddToClassList("sn-chip");
        _shopStars.AddToClassList("sn-semibold");
        _shopStars.style.alignSelf = Align.Center;
        _shopStars.style.marginBottom = 10;
        card.Add(_shopStars);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _shopList = new VisualElement();
        scroll.Add(_shopList);
        card.Add(scroll);

        _shopError = Text("", "caption", vc_UITheme.Red);
        _shopError.style.marginTop = 6;
        card.Add(_shopError);

        BuildShopRows();
        vc_UIAnim.Show(card, vc_UIAnim.Enter.Pop);
        if (_shopList != null) vc_UIAnim.Stagger(_shopList.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.12f);
        vc_LoopMeta.Fetch(_ => { if (_shopList != null && IsShowing) { vc_LoopMeta.MarkShopSeen(); BuildShopRows(); } });
    }

    private void CloseShop()
    {
        if (_shopList == null) return;          // already closing
        vc_Sfx.Play("click", 0.5f);
        _shopList = null;
        _shopPlayer?.MoveAction.Enable();
        StartCoroutine(CloseShopRoutine());
    }

    private IEnumerator CloseShopRoutine()
    {
        if (_shopCard != null) vc_UIAnim.Hide(_shopCard);
        _shopCard = null;
        yield return HideStage();
        EndStage();
        RefreshPocket();
    }

    private void BuildShopRows()
    {
        if (_shopList == null) return;
        _shopList.Clear();
        var meta = vc_LoopMeta.Current;
        _shopStars.text = meta != null ? $"{meta.stars} stars" : "";
        _shopStars.style.display = meta != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (meta == null) { _shopError.text = "No connection."; return; }

        foreach (vc_LoopMeta.Keepsake k in vc_LoopMeta.Keepsakes)
        {
            bool owned = meta.owned_mementos.Contains(k.id);
            bool unlocked = owned || meta.unlocked_mementos.Contains(k.id);
            int cost = vc_LoopMeta.Price(k.id);

            var row = new VisualElement();
            row.AddToClassList("sn-card");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10;
            if (!unlocked) row.style.backgroundColor = vc_UITheme.CreamLo;

            VisualElement icon = Icon(k.id, 52);
            icon.style.marginRight = 14;
            icon.style.flexShrink = 0;
            if (!unlocked) icon.style.unityBackgroundImageTintColor = new Color(vc_UITheme.Ink.r, vc_UITheme.Ink.g, vc_UITheme.Ink.b, 0.85f);
            row.Add(icon);

            var body = new VisualElement();
            body.style.flexGrow = 1; body.style.flexShrink = 1;
            var title = Text(unlocked ? k.name : "???", "body");
            title.AddToClassList("sn-semibold");
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(title);
            var sub = Text(unlocked ? k.effect : $"Help {k.giver}", "caption");
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(sub);
            row.Add(body);

            bool canBuy = !owned && unlocked && meta.stars >= cost;
            string id = k.id;
            Button btn = MakeButton(owned ? "Owned" : !unlocked ? "Locked" : $"{cost} stars",
                () => BuyKeepsake(id), "small", canBuy ? "gold" : null);
            btn.style.minWidth = 150;
            btn.style.flexShrink = 0;
            btn.style.marginRight = 0;
            if (!canBuy) btn.SetEnabled(false);
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
                if (_shopStars != null) vc_UIAnim.Punch(_shopStars, 0.12f, 0.3f);
            },
            _ =>
            {
                if (_shopError == null) return;
                _shopError.text = "Couldn't buy.";
                vc_UIAnim.Shake(_shopError);
            });
    }

    // ══ Unlocked (small notice: a keepsake is now in the shop) ═══════════════════════

    private VisualElement _notice;

    public void ShowUnlocked(vc_LoopMeta.Keepsake k)
    {
        if (k == null) return;
        _notice?.parent?.RemoveFromHierarchy();
        _notice?.RemoveFromHierarchy();

        // full-width strip that centres the card, so the card's own translate stays free for motion
        var holder = new VisualElement { pickingMode = PickingMode.Ignore };
        holder.style.position = Position.Absolute;
        holder.style.top = 96;
        holder.style.left = 0; holder.style.right = 0;
        holder.style.alignItems = Align.Center;

        _notice = new VisualElement { pickingMode = PickingMode.Ignore };
        _notice.AddToClassList("sn-panel");
        _notice.style.flexDirection = FlexDirection.Row;
        _notice.style.alignItems = Align.Center;
        _notice.style.paddingTop = 10; _notice.style.paddingBottom = 10;
        _notice.style.borderTopColor = vc_UITheme.GoldDark; _notice.style.borderBottomColor = vc_UITheme.GoldDark;
        _notice.style.borderLeftColor = vc_UITheme.GoldDark; _notice.style.borderRightColor = vc_UITheme.GoldDark;
        VisualElement icon = Icon(k.id, 52);
        icon.style.marginRight = 14;
        _notice.Add(icon);
        var col = new VisualElement { pickingMode = PickingMode.Ignore };
        var head = Text("New in shop", "caption", vc_UITheme.GoldDark);
        head.AddToClassList("sn-bold");
        head.style.unityTextAlign = TextAnchor.MiddleLeft;
        col.Add(head);
        var name = Text(k.name, "subtitle");
        name.style.unityTextAlign = TextAnchor.MiddleLeft;
        col.Add(name);
        _notice.Add(col);
        holder.Add(_notice);
        _root.Add(holder);
        vc_Sfx.Play("step_done", 0.7f);
        StartCoroutine(NoticeRoutine(_notice));
    }

    private IEnumerator NoticeRoutine(VisualElement n)
    {
        vc_UIAnim.Show(n, vc_UIAnim.Enter.Drop);
        yield return new WaitForSecondsRealtime(0.25f);
        Burst(n, vc_UITheme.Gold, 10);
        yield return new WaitForSecondsRealtime(3f);
        bool gone = false;
        vc_UIAnim.Hide(n, () => gone = true, vc_UIAnim.Exit.SlideUp);
        float timeout = 1f;
        while (!gone && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        VisualElement holder = n.parent;
        n.RemoveFromHierarchy();
        holder?.RemoveFromHierarchy();
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
        _pocket.style.top = p.y + 8f;
    }

    private void RefreshPocket()
    {
        if (_pocket == null) return;
        var owned = vc_LoopMeta.Current != null ? vc_LoopMeta.Current.owned_mementos : null;
        bool show = _pocketWanted && owned != null && owned.Count > 0 && !IsShowing;
        bool wasShown = _pocket.style.display == DisplayStyle.Flex;
        if (!show)
        {
            if (wasShown) vc_UIAnim.Hide(_pocket, null, vc_UIAnim.Exit.Fade);
            return;
        }
        _pocket.Clear();
        foreach (string id in owned)
        {
            vc_LoopMeta.Keepsake k = vc_LoopMeta.KeepsakeById(id);
            if (k == null) continue;
            VisualElement icon = Icon(id, 40);
            icon.style.marginLeft = 3; icon.style.marginRight = 3;
            icon.pickingMode = PickingMode.Position;
            icon.RegisterCallback<ClickEvent>(_ =>
            {
                vc_Sfx.Play("click", 0.5f);
                vc_UIAnim.Punch(icon, 0.2f, 0.3f);
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"{k.name}: {k.effect}", 3f);
            });
            _pocket.Add(icon);
        }
        if (!wasShown) vc_UIAnim.Show(_pocket, vc_UIAnim.Enter.SlideDown);
        else vc_UIAnim.StopAll(_pocket);
        if (wasShown) { _pocket.style.opacity = StyleKeyword.Null; _pocket.style.translate = StyleKeyword.Null; _pocket.style.scale = StyleKeyword.Null; }
    }
    // ══ building blocks ════════════════════════════════════════════════════════════

    private void BeginStage(Color backdrop, bool stars)
    {
        vc_UIAnim.StopAll(_stage);
        _stage.Clear();
        _twinkles.Clear();
        _stage.style.backgroundColor = backdrop;
        _stage.pickingMode = PickingMode.Position;
        vc_UIAnim.StopAll(_pocket);
        _pocket.style.display = DisplayStyle.None;
        vc_UIAnim.Show(_stage, vc_UIAnim.Enter.Fade, 0f, null, 0.35f);
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
            s.style.backgroundColor = rng.Next(0, 3) == 0 ? vc_UITheme.Violet : StarDot;
            sky.Add(s);
            _twinkles.Add(s);
        }
        _stage.Add(sky);
    }

    /// <summary>Fades the stage out (it stays in the tree; EndStage clears it).</summary>
    private IEnumerator HideStage()
    {
        bool hidden = false;
        vc_UIAnim.Hide(_stage, () => hidden = true, vc_UIAnim.Exit.Fade, 0f, false, 0.25f);
        float timeout = 1f;
        while (!hidden && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
    }

    private void EndStage()
    {
        vc_UIAnim.StopAll(_stage);
        _stage.style.display = DisplayStyle.None;
        _stage.style.opacity = StyleKeyword.Null;
        _stage.pickingMode = PickingMode.Ignore;
        _stage.Clear();
        _twinkles.Clear();
    }

    private VisualElement SkillCard(vc_SkillData skill, float width)
    {
        string letter = (skill.riaSecLetter ?? "?").Trim().ToUpperInvariant();
        var card = new VisualElement();
        card.AddToClassList("sn-card");
        card.style.width = width;
        card.style.alignItems = Align.Center;
        card.style.paddingLeft = 0; card.style.paddingRight = 0; card.style.paddingTop = 0;
        card.style.paddingBottom = 12;
        card.style.overflow = Overflow.Visible;

        var strip = Text($"{letter}  {vc_SkillManager.CategoryNameForSlot(vc_SkillManager.SlotIndexForLetter(letter))}", "caption", vc_UITheme.CreamHi);
        strip.AddToClassList("sn-bold");
        strip.style.fontSize = 18;
        strip.style.alignSelf = Align.Stretch;
        strip.style.backgroundColor = KindColor(letter);
        strip.style.paddingTop = 4; strip.style.paddingBottom = 4;
        strip.style.borderTopLeftRadius = 13; strip.style.borderTopRightRadius = 13;
        card.Add(strip);

        var sym = new VisualElement { pickingMode = PickingMode.Ignore };
        sym.style.width = width * 0.48f; sym.style.height = width * 0.48f;
        sym.style.marginTop = 10; sym.style.marginBottom = 6;
        sym.style.backgroundImage = vc_RiasecSymbols.Background(letter);
        sym.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        card.Add(sym);

        var name = Text(skill.DisplayName ?? skill.skillName, "caption", vc_UITheme.Ink);
        name.AddToClassList("sn-semibold");
        name.style.paddingLeft = 6; name.style.paddingRight = 6;
        card.Add(name);

        // Skill level: remembering a skill keeps it (SKILL_SYSTEM_DESIGN.md §6)
        int level = vc_SkillLevels.CarriedLevel(skill);
        var lv = new Label(level >= vc_SkillLevels.MaxLevel ? $"Lv{level} max" : $"Lv{level}") { pickingMode = PickingMode.Ignore };
        lv.AddToClassList("sn-badge");
        if (level >= vc_SkillLevels.MaxLevel) lv.AddToClassList("sn-badge--gold");
        else if (level == 2) lv.AddToClassList("sn-badge--silver");
        lv.style.marginTop = 4;
        card.Add(lv);

        var mark = new Label("Remembered") { name = "held-mark", pickingMode = PickingMode.Ignore };
        mark.AddToClassList("sn-badge");
        mark.style.position = Position.Absolute;
        mark.style.top = -16;
        mark.style.backgroundColor = HeldEdge;
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
            var n = Text(k.name, "caption", vc_UITheme.CreamHi);
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

    /// <summary>Themed label: role = sn-* type class (display, title, subtitle, body, caption).</summary>
    private static Label Text(string text, string role, Color? color = null)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.AddToClassList("sn-" + role);
        l.AddToClassList("sn-center");
        if (color.HasValue) l.style.color = color.Value;
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

    private static Button MakeButton(string label, Action onClick, params string[] variants)
    {
        Button b = vc_UITheme.MakeButton(label, onClick, variants);
        b.focusable = false;
        if (Array.IndexOf(variants, "icon-sm") < 0) b.style.minWidth = 240;
        return b;
    }

    private static void Fill(VisualElement e)
    {
        e.style.position = Position.Absolute;
        e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
    }

    /// <summary>RIASEC kind colour (UI V2 tokens), muted ink for anything else.</summary>
    public static Color KindColor(string letter) => letter switch
    {
        "R" or "I" or "A" or "S" or "E" or "C" => vc_UITheme.Riasec(letter),
        _ => vc_UITheme.InkMuted,
    };

    // ── tiny tweens (unscaled time, so they run while the game is paused) ──

    private static IEnumerator FadeIn(VisualElement e)
    {
        vc_UIAnim.Show(e, vc_UIAnim.Enter.Fade, 0f, null, 0.35f);
        yield return new WaitForSecondsRealtime(0.35f);
    }

    /// <summary>Pops in with a little overshoot (vc_UIAnim), waits until it lands.</summary>
    private static IEnumerator PopIn(VisualElement e, float delay)
    {
        vc_UIAnim.Show(e, vc_UIAnim.Enter.Pop, delay);
        yield return new WaitForSecondsRealtime(delay + 0.28f);
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
