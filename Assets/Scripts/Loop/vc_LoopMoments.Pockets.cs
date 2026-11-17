using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

/// <summary>
/// Two small loop moments:
///  - Pockets: at Go Back, after "What will you remember?", choose which owned keepsakes come
///    along this loop (2 pockets to start, more at the stall). Only carried keepsakes work.
///  - Study Notes: the one-use stall helper, +3 XP to a skill you choose (from the night pick).
/// </summary>
public partial class vc_LoopMoments
{
    private static readonly Color PocketFill = new Color32(255, 244, 214, 255);

    /// <summary>
    /// "What will you carry?" Keepsakes as cards; tap to put up to <paramref name="pockets"/>
    /// in your pockets. <paramref name="confirm"/> gets the chosen ids and a reply callback
    /// (null = success, else an error to show); <paramref name="done"/> runs after the outro.
    /// </summary>
    public void ShowPockets(List<vc_LoopMeta.Keepsake> owned, int pockets, List<string> preselected,
                            Action<List<string>, Action<string>> confirm, Action done)
    {
        StopAllCoroutines();
        BeginStage(NightSky, stars: true);

        var held = new List<string>();
        foreach (string id in preselected ?? new List<string>())
            if (held.Count < pockets && owned.Exists(k => k.id == id) && !held.Contains(id)) held.Add(id);

        var title = Text("What will you carry?", "title", vc_UITheme.Gold);
        title.style.marginBottom = 4;
        _stage.Add(title);
        var sub = Text("", "body", vc_UITheme.CreamHi);
        sub.style.marginBottom = 18;
        _stage.Add(sub);

        // the pockets themselves, filling up as you choose
        var bag = Row();
        bag.style.marginBottom = 18;
        _stage.Add(bag);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;
        grid.style.maxWidth = Length.Percent(94);
        _stage.Add(grid);

        var cards = new List<(vc_LoopMeta.Keepsake k, VisualElement card)>();
        void Refresh()
        {
            sub.text = $"{held.Count} / {pockets} pockets";
            bag.Clear();
            for (int i = 0; i < pockets; i++)
            {
                VisualElement slot = PocketSlot(i < held.Count ? held[i] : null, true, 64);
                bag.Add(slot);
            }
            foreach (var (k, card) in cards)
            {
                bool on = held.Contains(k.id);
                vc_UITheme.Border(card, on ? 4f : 2f, on ? vc_UITheme.Gold : vc_UITheme.PaperLine);
                card.style.borderBottomColor = on ? vc_UITheme.GoldDark : vc_UITheme.PaperLine;
                card.style.borderBottomWidth = on ? 7f : 4f;
                card.style.backgroundColor = on ? PocketFill : vc_UITheme.CreamHi;
                // the classic theme draws cards as images: tint them too, and show a badge
                card.style.unityBackgroundImageTintColor = on ? new Color(1f, 0.9f, 0.62f, 1f) : Color.white;
                card.style.opacity = on || held.Count < pockets ? 1f : 0.55f;
                Label mark = card.Q<Label>("pocket-mark");
                if (mark != null) mark.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        foreach (vc_LoopMeta.Keepsake k in owned)
        {
            var card = new VisualElement { pickingMode = PickingMode.Position };
            card.AddToClassList("sn-card");
            card.style.width = 200;
            card.style.alignItems = Align.Center;
            card.style.marginLeft = 8; card.style.marginRight = 8; card.style.marginBottom = 12;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            bool lv2 = vc_LoopMeta.IsLv2(k.id);
            card.Add(Icon(k.id, 64, lv2));
            var n = Text(lv2 ? k.name + " Lv2" : k.name, "caption", vc_UITheme.Ink);
            n.AddToClassList("sn-semibold");
            n.style.marginTop = 4;
            card.Add(n);
            var e = Text(lv2 ? k.lv2Effect : k.effect, "caption", vc_UITheme.InkMuted);
            e.style.fontSize = 16;
            card.Add(e);
            var badge = new Label("In pocket") { name = "pocket-mark" };
            badge.AddToClassList("sn-badge");
            badge.AddToClassList("sn-badge--gold");
            badge.style.position = Position.Absolute;
            badge.style.top = -14;
            badge.style.display = DisplayStyle.None;
            card.Add(badge);
            foreach (VisualElement child in card.Children()) child.pickingMode = PickingMode.Ignore;
            vc_LoopMeta.Keepsake kk = k;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (held.Remove(kk.id)) { vc_Sfx.Play("click", 0.5f, 0.05f); Refresh(); return; }
                if (held.Count >= pockets) { vc_Sfx.Play("click", 0.3f); vc_UIAnim.Shake(card); return; }
                held.Add(kk.id);
                vc_Sfx.Play("page_next", 0.7f, 0.05f);
                Burst(card, vc_UITheme.Gold, 10);
                vc_UIAnim.Punch(card, 0.1f, 0.3f);
                Refresh();
            });
            grid.Add(card);
            cards.Add((k, card));
        }
        Refresh();
        vc_UIAnim.Stagger(grid.Children(), vc_UIAnim.Enter.Pop, 0.05f, 0.15f);

        var error = Text("", "caption", ErrorOnDark);
        error.style.marginTop = 6;
        _stage.Add(error);

        Button go = null;
        bool busy = false;
        go = MakeButton("Carry these", () =>
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
                StartCoroutine(PocketsOutro(cards, held, done));
            });
        }, "primary");
        go.style.marginTop = 10;
        _stage.Add(go);
        vc_UIAnim.Show(go, vc_UIAnim.Enter.SlideUp, 0.3f);
    }

    private IEnumerator PocketsOutro(List<(vc_LoopMeta.Keepsake k, VisualElement card)> cards, List<string> held, Action done)
    {
        vc_Sfx.Play("cut_whoosh", 0.6f);
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / 0.7f);
            foreach (var (ks, card) in cards)
            {
                bool keep = held.Contains(ks.id);
                card.style.opacity = keep ? 1f - k * k : 1f - k;
                card.style.translate = new Translate(0, keep ? -100f * k * k : 50f * k);
            }
            yield return null;
        }
        yield return HideStage();
        EndStage();
        done?.Invoke();
    }

    // ══ Study Notes (stall helper) ═══════════════════════════════════════════

    public void ShowStudyNotes(List<vc_SkillData> skills, Action closed)
    {
        if (_root == null || skills == null || skills.Count == 0) { closed?.Invoke(); return; }
        var veil = new VisualElement();
        Fill(veil);
        veil.style.backgroundColor = vc_UITheme.Scrim;
        veil.style.alignItems = Align.Center; veil.style.justifyContent = Justify.Center;
        veil.pickingMode = PickingMode.Position;

        var panel = new VisualElement();
        panel.AddToClassList("sn-panel");
        panel.style.maxWidth = Length.Percent(94);
        panel.style.alignItems = Align.Center;
        panel.Add(Text("Study Notes", "title"));
        var hint = Text($"+{StudyXp} XP to which skill?", "body", vc_UITheme.InkMuted);
        hint.style.marginBottom = 12;
        panel.Add(hint);
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        panel.Add(row);
        bool busy = false;
        void Close() { veil.RemoveFromHierarchy(); closed?.Invoke(); }

        foreach (vc_SkillData s in skills)
        {
            VisualElement card = SkillCard(s, 150);
            card.style.marginLeft = 6; card.style.marginRight = 6; card.style.marginBottom = 10;
            card.pickingMode = PickingMode.Position;
            vc_SkillData skill = s;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (busy) return;
                busy = true;
                vc_Sfx.Play("click", 0.6f);
                vc_LoopMeta.UseHelper("studynotes", ok =>
                {
                    if (!ok) { busy = false; vc_UIAnim.Shake(card); return; }
                    vc_SkillLevels.AddXp(skill, StudyXp);
                    string scene = vc_TownStage.Current != null ? vc_TownStage.Current.gameObject.scene.name : null;
                    vc_QuestRoom.SaveProgressIds(null, scene);
                    vc_Sfx.Play("step_done", 0.8f);
                    Burst(card, vc_UITheme.Gold, 14);
                    vc_UIAnim.Punch(card, 0.2f, 0.35f);
                    vc_QuestHUD.Instance?.ShowFeedbackTimed($"{skill.DisplayName} +{StudyXp} XP", 2.5f);
                    StartCoroutine(CloseLater(Close, 0.6f));
                });
            });
            row.Add(card);
        }
        Button cancel = MakeButton("Not now", () => { if (!busy) { vc_Sfx.Play("click", 0.5f); Close(); } }, "small");
        cancel.style.marginTop = 8;
        panel.Add(cancel);
        veil.Add(panel);
        _root.Add(veil);
        vc_UIAnim.Show(panel, vc_UIAnim.Enter.Pop);
    }

    public const int StudyXp = 3;

    private static IEnumerator CloseLater(Action close, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        close?.Invoke();
    }
}
