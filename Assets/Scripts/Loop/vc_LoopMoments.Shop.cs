using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.UI;

/// <summary>
/// The stall in town (vc_KeepsakeStall): a little shop run by Aling Tess, in four sections -
/// Keepsakes · Pockets · Helpers · Style. Stars are the only currency; every purchase
/// is one atomic server call (sunod-game-server app/loop_meta.py buy). Buying has a beat:
/// stars fly from your purse to the item, it pops and sparkles, and the keeper says a line.
/// </summary>
public partial class vc_LoopMoments
{
    public const string KeeperName = "Aling Tess";

    private static readonly string[] Tabs = { "Keepsakes", "Pockets", "Helpers", "Style" };

    private VisualElement _shopList, _shopCard, _shopTabs;
    private Label _shopStars, _shopError, _keeperLine;
    private PlayerController _shopPlayer;
    private string _shopTab = "Keepsakes";
    private bool _shopBusy;

    public void ShowShop()
    {
        StopAllCoroutines();
        BeginStage(vc_UITheme.Scrim, stars: false);
        _shopPlayer = FindFirstObjectByType<PlayerController>();
        _shopPlayer?.MoveAction.Disable();
        _shopBusy = false;

        var card = new VisualElement();
        card.AddToClassList("sn-panel");
        card.AddToClassList("sn-sheet");
        card.style.width = 900;
        card.style.maxWidth = Length.Percent(94);
        card.style.maxHeight = Length.Percent(94);
        _shopCard = card;
        _stage.Add(card);

        // header: title centred, round close top-right
        var header = new VisualElement();
        header.AddToClassList("sn-header");
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.flexShrink = 0;
        var spacer = new VisualElement();
        spacer.style.width = 72; spacer.style.flexShrink = 0;
        header.Add(spacer);
        var title = Text("The Stall", "title");
        title.style.flexGrow = 1;
        header.Add(title);
        Button close = MakeButton("×", CloseShop, "icon-sm");
        close.style.marginTop = 0; close.style.marginBottom = 0; close.style.marginRight = 0;
        close.style.fontSize = 40;
        header.Add(close);
        card.Add(header);

        // the keeper + her line, and your stars
        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        top.style.marginBottom = 8;
        top.style.flexShrink = 0;
        var keeper = new VisualElement { pickingMode = PickingMode.Ignore };
        keeper.style.width = 64; keeper.style.height = 64; keeper.style.flexShrink = 0;
        keeper.style.marginRight = 10;
        Texture2D face = Resources.Load<Texture2D>("Shop/keeper_face");
        if (face != null)
        {
            face.filterMode = FilterMode.Point;
            keeper.style.backgroundImage = new StyleBackground(face);
            keeper.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        top.Add(keeper);
        var bubble = new VisualElement();
        bubble.AddToClassList("sn-card");
        bubble.style.flexGrow = 1; bubble.style.flexShrink = 1;
        bubble.style.paddingTop = 6; bubble.style.paddingBottom = 8;
        _keeperLine = Text("", "caption", vc_UITheme.Ink);
        _keeperLine.style.unityTextAlign = TextAnchor.MiddleLeft;
        bubble.Add(_keeperLine);
        top.Add(bubble);
        _shopStars = Text("", "body");
        _shopStars.AddToClassList("sn-chip");
        _shopStars.AddToClassList("sn-semibold");
        _shopStars.style.marginLeft = 10;
        _shopStars.style.flexShrink = 0;
        top.Add(_shopStars);
        card.Add(top);
        Say(Greeting());

        // section tabs
        _shopTabs = new VisualElement();
        _shopTabs.style.flexDirection = FlexDirection.Row;
        _shopTabs.style.flexWrap = Wrap.Wrap;
        _shopTabs.style.justifyContent = Justify.Center;
        _shopTabs.style.marginBottom = 8;
        _shopTabs.style.flexShrink = 0;
        card.Add(_shopTabs);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _shopList = new VisualElement();
        scroll.Add(_shopList);
        card.Add(scroll);

        _shopError = Text("", "caption", vc_UITheme.Red);
        _shopError.style.marginTop = 6;
        card.Add(_shopError);

        BuildShop();
        vc_UIAnim.Show(card, vc_UIAnim.Enter.Pop);
        vc_UIAnim.Stagger(_shopList.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.12f);
        vc_LoopMeta.Fetch(_ => { if (_shopList != null && IsShowing) { vc_LoopMeta.MarkShopSeen(); BuildShop(); } });
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

    private void BuildShop()
    {
        if (_shopList == null) return;
        LoopMetaResponse meta = vc_LoopMeta.Current;
        int stars = vc_LoopMeta.Available;
        _shopStars.text = meta != null ? $"★ {stars}" : "";
        _shopStars.style.display = meta != null ? DisplayStyle.Flex : DisplayStyle.None;

        _shopTabs.Clear();
        foreach (string tab in Tabs)
        {
            string t = tab;
            Button b = MakeButton(tab, () => { if (_shopTab == t) return; vc_Sfx.Play("click", 0.5f); _shopTab = t; BuildShop(); Stagger(); },
                "small", _shopTab == tab ? "gold" : "ghost");
            b.style.minWidth = 0;
            b.style.marginLeft = 4; b.style.marginRight = 4; b.style.marginBottom = 4;
            if (tab == "Keepsakes" && vc_LoopMeta.HasNewInShop()) b.text = tab + " !";
            _shopTabs.Add(b);
        }

        _shopList.Clear();
        if (meta == null) { _shopError.text = "Not ready yet. Try again soon."; return; }
        switch (_shopTab)
        {
            case "Pockets": BuildPockets(meta); break;
            case "Helpers": BuildHelpers(meta); break;
            case "Style": BuildStyle(meta); break;
            default: BuildKeepsakes(meta); break;
        }
    }

    private void Stagger()
    {
        if (_shopList != null) vc_UIAnim.Stagger(_shopList.Children(), vc_UIAnim.Enter.SlideUp, 0.03f, 0f);
    }

    // ── Keepsakes ─────────────────────────────────────────────────────────

    private void BuildKeepsakes(LoopMetaResponse meta)
    {
        _shopList.Add(Note($"Only what's in your pockets works. ({meta.carried_mementos.Count}/{vc_LoopMeta.Pockets})"));
        foreach (vc_LoopMeta.Keepsake k in vc_LoopMeta.Keepsakes)
        {
            bool owned = meta.owned_mementos.Contains(k.id);
            bool unlocked = owned || meta.unlocked_mementos.Contains(k.id);
            bool lv2 = vc_LoopMeta.IsLv2(k.id);
            bool canUpgrade = owned && vc_LoopMeta.Lv2Unlocked(k.id);
            bool carried = meta.carried_mementos.Contains(k.id);

            string sub = !unlocked ? (k.fromEvent ? $"Free from {k.giver}." : $"Help {k.giver}.")
                : lv2 ? $"{k.effect}  Lv2: {k.lv2Effect}"
                : canUpgrade ? $"{k.effect}  Lv2: {k.lv2Effect}"
                : !k.fromEvent && owned ? $"{k.effect}  (Help {k.giver} again for Lv2.)"
                : k.effect;

            string label; string[] variant; Action act = null; bool enabled = false;
            if (canUpgrade)
            {
                int c = vc_LoopMeta.Lv2Price(k.id);
                label = $"Lv2 ★{c}"; variant = new[] { "small", "gold" }; enabled = vc_LoopMeta.Available >= c;
                string id = k.id; act = () => Buy("lv2:" + id);
            }
            else if (owned) { label = carried ? "Carried" : "Owned"; variant = new[] { "small" }; }
            else if (k.fromEvent) { label = "Event"; variant = new[] { "small" }; }
            else if (!unlocked) { label = "Locked"; variant = new[] { "small" }; }
            else
            {
                int c = vc_LoopMeta.Price(k.id);
                label = $"★{c}"; variant = new[] { "small", "gold" }; enabled = vc_LoopMeta.Available >= c;
                string id = k.id; act = () => Buy(id);
            }

            VisualElement row = Row(Icon(k.id, 56, lv2), unlocked ? k.name : "???", sub, label, act, enabled, variant,
                unlocked ? null : vc_UITheme.CreamLo);
            if (!unlocked) row.Q<VisualElement>("row-icon").style.unityBackgroundImageTintColor = new Color(vc_UITheme.Ink.r, vc_UITheme.Ink.g, vc_UITheme.Ink.b, 0.85f);
            if (lv2) AddBadge(row, "Lv2", "sn-badge--gold");
            else if (carried) AddBadge(row, "In pocket", null, vc_UITheme.Violet);
            row.name = "item-" + k.id;
            _shopList.Add(row);
        }
    }

    // ── Pockets ───────────────────────────────────────────────────────────

    private void BuildPockets(LoopMetaResponse meta)
    {
        int have = vc_LoopMeta.Pockets;
        _shopList.Add(Note("More pockets, more keepsakes."));
        var slots = new VisualElement();
        slots.style.flexDirection = FlexDirection.Row;
        slots.style.justifyContent = Justify.Center;
        slots.style.marginBottom = 14;
        for (int i = 0; i < meta.max_pockets; i++)
        {
            bool open = i < have;
            string id = i < meta.carried_mementos.Count ? meta.carried_mementos[i] : null;
            slots.Add(PocketSlot(open ? id : null, open, 84));
        }
        _shopList.Add(slots);

        if (have >= meta.max_pockets) { _shopList.Add(Note("Your bag is full size!")); return; }
        int next = have + 1;
        int cost = vc_LoopMeta.Price(vc_LoopMeta.NextPocketId);
        _shopList.Add(Row(PocketSlot(null, true, 56), $"Pocket {next}", $"Carry {next} keepsakes each loop.",
            $"★{cost}", () => Buy(vc_LoopMeta.NextPocketId), vc_LoopMeta.Available >= cost, new[] { "small", "gold" }));
    }

    private VisualElement PocketSlot(string keepsake, bool open, float size)
    {
        var slot = new VisualElement { pickingMode = PickingMode.Ignore };
        slot.AddToClassList("sn-card");
        slot.style.width = size; slot.style.height = size;
        slot.style.marginLeft = 6; slot.style.marginRight = 6;
        slot.style.paddingLeft = 0; slot.style.paddingRight = 0; slot.style.paddingTop = 0; slot.style.paddingBottom = 0;
        slot.style.alignItems = Align.Center; slot.style.justifyContent = Justify.Center;
        Texture2D bag = Resources.Load<Texture2D>(open ? "Shop/pocket_open" : "Shop/pocket_locked");
        if (bag != null)
        {
            bag.filterMode = FilterMode.Point;
            slot.style.backgroundImage = new StyleBackground(bag);
            slot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            slot.style.backgroundColor = Color.clear;
            vc_UITheme.Border(slot, 0f, Color.clear);
        }
        else if (!open) slot.style.backgroundColor = vc_UITheme.CreamLo;
        if (!string.IsNullOrEmpty(keepsake)) slot.Add(Icon(keepsake, size * 0.6f, vc_LoopMeta.IsLv2(keepsake)));
        return slot;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void BuildHelpers(LoopMetaResponse meta)
    {
        _shopList.Add(Note("One of each per loop."));
        foreach (vc_LoopMeta.ShopGood g in vc_LoopMeta.Helpers)
        {
            bool held = vc_LoopMeta.HoldsHelper(g.id);
            bool used = meta.helpers_used_loop.Contains(g.id);
            bool bought = meta.helpers_bought_loop.Contains(g.id);
            int cost = vc_LoopMeta.Price(g.id);
            string label = held ? "Ready" : used ? "Used" : bought ? "Next loop" : $"★{cost}";
            bool can = !held && !bought && vc_LoopMeta.Available >= cost;
            string id = g.id;
            _shopList.Add(Row(GoodIcon(g.id, 56), g.name, g.blurb, label, can ? () => Buy(id) : null, can,
                held || used || bought ? new[] { "small" } : new[] { "small", "gold" }));
        }
    }

    // ── Style ─────────────────────────────────────────────────────────────

    private void BuildStyle(LoopMetaResponse meta)
    {
        string[] groups = { "outfit", "hat", "trail", "pet", "home" };
        string[] heads = { "Outfits", "Hats", "Trails", "Pet", "Home add-ons" };
        for (int gi = 0; gi < groups.Length; gi++)
        {
            string type = groups[gi];
            _shopList.Add(SubHead(heads[gi]));
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            foreach (vc_LoopMeta.ShopGood g in vc_LoopMeta.Cosmetics)
                if (g.type == type) grid.Add(StyleTile(g, meta));
            _shopList.Add(grid);
        }
    }

    private VisualElement StyleTile(vc_LoopMeta.ShopGood g, LoopMetaResponse meta)
    {
        bool owned = vc_LoopMeta.OwnsCosmetic(g.id);
        bool on = g.type == "home" ? vc_LoopMeta.HomeAddOn(g.id) : vc_LoopMeta.Equipped(g.type) == g.id;
        int cost = vc_LoopMeta.Price(g.id);

        var tile = new VisualElement { name = "item-" + g.id };
        tile.AddToClassList("sn-card");
        tile.style.width = 196;
        tile.style.alignItems = Align.Center;
        tile.style.marginLeft = 6; tile.style.marginRight = 6; tile.style.marginBottom = 10;
        tile.style.paddingTop = 10; tile.style.paddingBottom = 10;
        if (on) { vc_UITheme.Border(tile, 4f, vc_UITheme.Gold); tile.style.borderBottomColor = vc_UITheme.GoldDark; }
        VisualElement icon = GoodIcon(g.id, 72);
        icon.name = "row-icon";
        tile.Add(icon);
        var name = Text(g.name, "caption", vc_UITheme.Ink);
        name.AddToClassList("sn-semibold");
        name.style.marginTop = 4;
        tile.Add(name);
        var blurb = Text(g.blurb, "caption", vc_UITheme.InkMuted);
        blurb.style.fontSize = 16;
        tile.Add(blurb);

        string id = g.id, type = g.type;
        Button btn;
        if (!owned)
        {
            bool can = vc_LoopMeta.Available >= cost;
            btn = MakeButton($"★{cost}", () => Buy(id), "small", "gold");
            btn.SetEnabled(can);
        }
        else if (type == "home") btn = MakeButton(on ? "Shown" : "Show", () => Equip("home", id), "small", on ? "primary" : null);
        else btn = MakeButton(on ? (type == "outfit" || type == "hat" ? "Wearing" : "On") : (type == "outfit" || type == "hat" ? "Wear" : "Use"),
            () => Equip(type, on ? "" : id), "small", on ? "primary" : null);
        btn.style.minWidth = 140;
        btn.style.marginTop = 6;
        tile.Add(btn);
        return tile;
    }

    private void Equip(string slot, string id)
    {
        if (_shopBusy) return;
        vc_Sfx.Play("click", 0.6f);
        // offline: applied now, saved with the run (vc_LoopMeta.Offline)
        if (!vc_LoopMeta.Equip(slot, id)) { ShowShopError("Couldn't change that."); return; }
        if (!string.IsNullOrEmpty(id))
        {
            vc_Sfx.Play("step_done", 0.6f);
            if (slot == "outfit" || slot == "hat") vc_PlayerCosmetics.SpinPlayer();
            Say(EquipLine(slot));
        }
        BuildShop();
        VisualElement tile = _shopList?.Q<VisualElement>("item-" + id);
        if (tile != null) { vc_UIAnim.Punch(tile, 0.12f, 0.3f); Burst(tile, vc_UITheme.Gold, 10); }
    }

    // ── Buying: works offline (saved with the run), then the beat ─────────

    private void Buy(string itemId)
    {
        if (_shopBusy || string.IsNullOrEmpty(itemId)) return;
        vc_Sfx.Play("click", 0.6f);
        _shopError.text = "";
        int before = vc_LoopMeta.Available;
        string why = vc_LoopMeta.Buy(itemId);
        if (why != null) { ShowShopError(why); return; }
        _shopBusy = true;
        string key = itemId.StartsWith("lv2:") ? itemId.Substring(4) : itemId.StartsWith("pocket") ? null : itemId;
        VisualElement target = key != null ? _shopList?.Q<VisualElement>("item-" + key) : null;
        StartCoroutine(BuyBeat(target, Mathf.Max(0, before - vc_LoopMeta.Available), itemId));
    }

    private void ShowShopError(string text)
    {
        if (_shopError == null) return;
        _shopError.text = text;
        vc_UIAnim.Shake(_shopError);
        Say("Hmm. Not today, anak.");
    }

    /// <summary>Stars fly from your purse into the item, it pops and sparkles, Aling Tess says a line.</summary>
    private IEnumerator BuyBeat(VisualElement target, int spent, string itemId)
    {
        if (_shopStars != null && target != null)
        {
            Vector2 from = _root.WorldToLocal(_shopStars.worldBound.center);
            Vector2 to = _root.WorldToLocal(target.worldBound.center);
            int coins = Mathf.Clamp(spent / 10, 3, 10);
            var flying = new List<VisualElement>();
            Texture2D star = Resources.Load<Texture2D>("Shop/coin_star");
            for (int i = 0; i < coins; i++)
            {
                var c = new VisualElement { pickingMode = PickingMode.Ignore };
                c.style.position = Position.Absolute;
                c.style.width = 20; c.style.height = 20;
                if (star != null) { star.filterMode = FilterMode.Point; c.style.backgroundImage = new StyleBackground(star); }
                else c.style.backgroundColor = vc_UITheme.Gold;
                c.style.left = from.x - 10; c.style.top = from.y - 10;
                _root.Add(c);
                flying.Add(c);
            }
            for (float t = 0f; t < 0.55f + coins * 0.04f; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < flying.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * 0.04f) / 0.55f);
                    float e = k * k * (3f - 2f * k);
                    Vector2 p = Vector2.Lerp(from, to, e) + new Vector2(0f, -90f * Mathf.Sin(e * Mathf.PI));
                    flying[i].style.left = Mathf.Round(p.x - 10); flying[i].style.top = Mathf.Round(p.y - 10);
                    flying[i].style.opacity = k >= 1f ? 0f : 1f;
                }
                if (Mathf.Repeat(t, 0.08f) < Time.unscaledDeltaTime) vc_Sfx.Play("click", 0.25f, 0.2f);
                yield return null;
            }
            foreach (var c in flying) c.RemoveFromHierarchy();
        }
        vc_Sfx.Play("step_done", 0.85f);
        if (_shopStars != null) vc_UIAnim.Punch(_shopStars, 0.12f, 0.3f);
        BuildShop();
        string key = itemId.StartsWith("lv2:") ? itemId.Substring(4) : itemId;
        VisualElement fresh = _shopList?.Q<VisualElement>("item-" + key);
        if (fresh != null)
        {
            VisualElement icon = fresh.Q<VisualElement>("row-icon") ?? fresh;
            vc_UIAnim.Punch(icon, 0.35f, 0.45f);
            Burst(icon, vc_UITheme.Gold, 14);
            Burst(icon, Color.white, 6);
        }
        Say(BuyLine(itemId));
        _shopBusy = false;
    }

    // ── Aling Tess ────────────────────────────────────────────────────────

    private void Say(string line)
    {
        if (_keeperLine == null) return;
        _keeperLine.text = $"{KeeperName}: \"{line}\"";
        vc_UIAnim.Punch(_keeperLine.parent, 0.05f, 0.25f);
    }

    private static string Greeting()
    {
        if (vc_LoopMeta.HasNewInShop()) return "Something new came in. Somebody you helped left it here for you.";
        if (vc_LoopMeta.IsRepeatLoop) return "You again! ...Funny, I feel like I've said that before. Look around.";
        return "Welcome, anak! Stars only. Everything here was somebody's once.";
    }

    private static string BuyLine(string itemId)
    {
        if (itemId.StartsWith("lv2:")) return "Look at that shine. Friendship does that to old things.";
        if (itemId.StartsWith("pocket")) return "There. Sewn on tight. Room for one more treasure.";
        if (vc_LoopMeta.KeepsakeById(itemId) != null) return "Take good care of it. It remembers more than you think.";
        vc_LoopMeta.ShopGood g = vc_LoopMeta.GoodById(itemId);
        if (g == null) return "Salamat!";
        return g.type switch
        {
            "helper" => "Use it well. One per loop, ha? Even I can't make more.",
            "outfit" => "Bagay sa'yo! Try it on.",
            "hat" => "Ay, ang cute! Mind the wind.",
            "trail" => "Now everyone will know where you've been.",
            "pet" => "Muning likes you. She doesn't like anyone.",
            "home" => "Your home will look lovely. Go see!",
            _ => "Salamat!",
        };
    }

    private static string EquipLine(string slot) => slot switch
    {
        "outfit" => "Ikot! Spin for me. Perfect.",
        "hat" => "Tilt it a bit. There.",
        "trail" => "Walk around, let me see it.",
        "pet" => "Muning's coming with you.",
        "home" => "Go home and look!",
        _ => "There you go.",
    };

    // ── rows ──────────────────────────────────────────────────────────────

    private VisualElement Row(VisualElement icon, string title, string sub, string button, Action act, bool enabled,
                              string[] variants, Color? background = null)
    {
        var row = new VisualElement();
        row.AddToClassList("sn-card");
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 10;
        if (background.HasValue) row.style.backgroundColor = background.Value;

        icon.name = "row-icon";
        icon.style.marginRight = 14;
        icon.style.flexShrink = 0;
        row.Add(icon);

        var body = new VisualElement();
        body.style.flexGrow = 1; body.style.flexShrink = 1;
        var t = Text(title, "body");
        t.AddToClassList("sn-semibold");
        t.style.unityTextAlign = TextAnchor.MiddleLeft;
        body.Add(t);
        var s = Text(sub, "caption");
        s.style.unityTextAlign = TextAnchor.MiddleLeft;
        body.Add(s);
        row.Add(body);

        Button btn = MakeButton(button, act ?? (() => { }), variants);
        btn.style.minWidth = 150;
        btn.style.flexShrink = 0;
        btn.style.marginRight = 0;
        if (act == null || !enabled) btn.SetEnabled(false);
        row.Add(btn);
        return row;
    }

    private static void AddBadge(VisualElement row, string text, string cls, Color? color = null)
    {
        var b = new Label(text) { pickingMode = PickingMode.Ignore };
        b.AddToClassList("sn-badge");
        if (cls != null) b.AddToClassList(cls);
        if (color.HasValue) { b.style.backgroundColor = color.Value; b.style.color = Color.white; }
        b.style.position = Position.Absolute;
        b.style.left = 8; b.style.top = -10;
        row.Add(b);
    }

    private static Label Note(string text)
    {
        var n = Text(text, "caption", vc_UITheme.InkMuted);
        n.style.marginBottom = 10;
        return n;
    }

    private static Label SubHead(string text)
    {
        var h = Text(text, "body", vc_UITheme.Ink);
        h.AddToClassList("sn-bold");
        h.style.marginTop = 6; h.style.marginBottom = 6;
        return h;
    }

    private static VisualElement GoodIcon(string id, float size)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = size; icon.style.height = size;
        Texture2D tex = vc_LoopMeta.GoodIcon(id);
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            icon.style.backgroundImage = new StyleBackground(tex);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        else
        {
            icon.style.backgroundColor = vc_UITheme.CreamLo;
            vc_UITheme.Radius(icon, 8f);
        }
        return icon;
    }
}
