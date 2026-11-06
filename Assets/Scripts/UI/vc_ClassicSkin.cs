using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// Classic UI style art (2026-11-07). In Classic, buttons, panels and cards wear hand-shaded
    /// pixel 9-slice art (Resources/UIClassic, made by Art/UI/Panels/make_classic_skin.py)
    /// instead of the flat V2 fills: chunky keys with a lit edge and a deep lip, framed paper
    /// panels with corner rivets. Modern keeps the flat V2 look. Same palette.
    ///
    /// Applied at runtime by vc_RadiusClamp's pass (every UIDocument, also UI built in code),
    /// so no screen needs editing. Layout is kept: the old border widths move into padding.
    /// Pressed = the lip-less "down" sprite. Switching back to Modern clears everything.
    /// </summary>
    public static class vc_ClassicSkin
    {
        private const float Scale = 3f;   // art pixel -> 3 UI units (chunky, crisp)

        private enum Kind { None, Button, Panel, PanelDark, Card, Chip, Note, Badge }

        private sealed class State
        {
            public string sprite;            // current base sprite name
            public bool down;
            public float bt, bb, bl, br;     // border widths before skinning
            public float pt, pb, pl, pr;     // padding before skinning
        }

        private static readonly ConditionalWeakTable<VisualElement, State> Skinned = new ConditionalWeakTable<VisualElement, State>();
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Tex =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>Skin (classic) or unskin (modern) one element. True = it's skinned now (skip radius work).</summary>
        public static bool Apply(VisualElement ve, bool classic)
        {
            bool had = Skinned.TryGetValue(ve, out State st);
            Kind kind = classic ? KindOf(ve) : Kind.None;
            if (kind == Kind.None)
            {
                if (had) Clear(ve, st);
                return false;
            }

            string sprite = SpriteFor(ve, kind);
            if (!had)
            {
                IResolvedStyle r = ve.resolvedStyle;
                st = new State
                {
                    bt = r.borderTopWidth, bb = r.borderBottomWidth, bl = r.borderLeftWidth, br = r.borderRightWidth,
                    pt = r.paddingTop, pb = r.paddingBottom, pl = r.paddingLeft, pr = r.paddingRight,
                };
                Skinned.Add(ve, st);
                ve.style.borderTopWidth = 0; ve.style.borderBottomWidth = 0;
                ve.style.borderLeftWidth = 0; ve.style.borderRightWidth = 0;
                ve.style.paddingTop = st.pt + st.bt;
                ve.style.paddingBottom = st.pb + st.bb + (kind == Kind.Button ? 4f : 0f);   // lift text off the lip
                ve.style.paddingLeft = st.pl + st.bl;
                ve.style.paddingRight = st.pr + st.br;
                ve.style.borderTopLeftRadius = 0; ve.style.borderTopRightRadius = 0;
                ve.style.borderBottomLeftRadius = 0; ve.style.borderBottomRightRadius = 0;
                ve.style.backgroundColor = Color.clear;
                SetSlices(ve, kind);
                if (kind == Kind.Button)
                {
                    ve.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
                    ve.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
                    ve.RegisterCallback<PointerLeaveEvent>(OnLeave);
                    ve.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                }
            }
            if (st.sprite != sprite)
            {
                st.sprite = sprite;
                SetImage(ve, st);
            }
            return true;
        }

        private static Kind KindOf(VisualElement ve)
        {
            if (ve.ClassListContains("sn-btn") || ve.ClassListContains("sl-button") || ve.ClassListContains("sl-button-wide"))
                return ve.ClassListContains("sn-btn--ghost") ? Kind.None : Kind.Button;
            if (ve.ClassListContains("qb-card")) return Kind.Note;              // board posters: pinned notes
            if (ve.ClassListContains("sn-badge")) return Kind.Badge;
            if (ve.ClassListContains("sn-panel--dark") || ve.ClassListContains("sl-panel-dark")) return Kind.PanelDark;
            if (ve.ClassListContains("sn-panel") || ve.ClassListContains("sl-panel-wood") || ve.ClassListContains("sl-dialog")) return Kind.Panel;
            if (ve.ClassListContains("sn-card") || ve.ClassListContains("sl-panel-cream") || ve.ClassListContains("sl-slot")) return Kind.Card;
            if (ve.ClassListContains("sn-chip") || ve.ClassListContains("sl-chip")) return Kind.Chip;
            return Kind.None;
        }

        private static bool IsOn(VisualElement ve)
        {
            foreach (string c in ve.GetClasses())
                if (c.EndsWith("--on") || c.EndsWith("--active") || c.EndsWith("--selected") || c == "sn-attn") return true;
            return false;
        }

        private static string SpriteFor(VisualElement ve, Kind kind)
        {
            switch (kind)
            {
                case Kind.Panel: return "panel";
                case Kind.PanelDark: return "panel_dark";
                case Kind.Card: return IsOn(ve) ? "card_sel" : "card";
                case Kind.Chip: return "chip";
                case Kind.Note: return "note";
                case Kind.Badge:
                    return ve.ClassListContains("sn-badge--gold") ? "badge_gold"
                         : ve.ClassListContains("sn-badge--silver") ? "badge_silver" : "badge_green";
            }
            if (ve.ClassListContains("sn-btn--primary")) return "btn_green";
            if (ve.ClassListContains("sn-btn--gold")) return "btn_gold";
            if (ve.ClassListContains("sn-btn--danger")) return "btn_red";
            if (ve.ClassListContains("sn-btn--night")) return "btn_night";
            if (ve.ClassListContains("sn-attn")) return "btn_gold";
            if (IsOn(ve)) return "btn_green";      // chosen option in a choice row / tab
            return "btn_cream";
        }

        private static void SetSlices(VisualElement ve, Kind kind)
        {
            int l, t, r, b;
            switch (kind)
            {
                case Kind.Button: l = 4; t = 4; r = 4; b = 7; break;
                case Kind.Card:
                case Kind.Chip: l = 4; t = 4; r = 4; b = 5; break;
                case Kind.Note: l = 5; t = 5; r = 5; b = 5; break;
                case Kind.Badge: l = 3; t = 3; r = 3; b = 4; break;
                default: l = 6; t = 6; r = 6; b = 9; break;
            }
            ve.style.unitySliceLeft = l; ve.style.unitySliceTop = t;
            ve.style.unitySliceRight = r; ve.style.unitySliceBottom = b;
            ve.style.unitySliceScale = kind == Kind.Badge ? 2f : Scale;
        }

        private static void SetImage(VisualElement ve, State st)
        {
            string name = st.down && st.sprite.StartsWith("btn_") ? st.sprite + "_down" : st.sprite;
            Texture2D t = Load(name);
            if (t == null) return;
            ve.style.backgroundImage = new StyleBackground(t);
            ve.style.unityBackgroundImageTintColor = Color.white;
        }

        private static Texture2D Load(string name)
        {
            if (Tex.TryGetValue(name, out Texture2D t) && t != null) return t;
            t = Resources.Load<Texture2D>("UIClassic/" + name);
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            Tex[name] = t;
            return t;
        }

        private static void Clear(VisualElement ve, State st)
        {
            Skinned.Remove(ve);
            ve.style.backgroundImage = StyleKeyword.Null;
            ve.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            ve.style.backgroundColor = StyleKeyword.Null;
            ve.style.borderTopWidth = StyleKeyword.Null; ve.style.borderBottomWidth = StyleKeyword.Null;
            ve.style.borderLeftWidth = StyleKeyword.Null; ve.style.borderRightWidth = StyleKeyword.Null;
            ve.style.paddingTop = StyleKeyword.Null; ve.style.paddingBottom = StyleKeyword.Null;
            ve.style.paddingLeft = StyleKeyword.Null; ve.style.paddingRight = StyleKeyword.Null;
            ve.style.borderTopLeftRadius = StyleKeyword.Null; ve.style.borderTopRightRadius = StyleKeyword.Null;
            ve.style.borderBottomLeftRadius = StyleKeyword.Null; ve.style.borderBottomRightRadius = StyleKeyword.Null;
            ve.style.unitySliceLeft = StyleKeyword.Null; ve.style.unitySliceTop = StyleKeyword.Null;
            ve.style.unitySliceRight = StyleKeyword.Null; ve.style.unitySliceBottom = StyleKeyword.Null;
            ve.style.unitySliceScale = StyleKeyword.Null;
            ve.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            ve.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            ve.UnregisterCallback<PointerLeaveEvent>(OnLeave);
            ve.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        // ── pressed look ──────────────────────────────────────────────────
        private static void OnDown(PointerDownEvent e) => SetDown(e.currentTarget as VisualElement, true);
        private static void OnUp(PointerUpEvent e) => SetDown(e.currentTarget as VisualElement, false);
        private static void OnLeave(PointerLeaveEvent e) => SetDown(e.currentTarget as VisualElement, false);
        private static void OnCaptureOut(PointerCaptureOutEvent e) => SetDown(e.currentTarget as VisualElement, false);

        private static void SetDown(VisualElement ve, bool down)
        {
            if (ve == null || !Skinned.TryGetValue(ve, out State st) || st.down == down) return;
            if (down && !ve.enabledInHierarchy) return;
            st.down = down;
            SetImage(ve, st);
        }
    }
}
