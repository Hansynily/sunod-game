using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// UI V2 tokens for code-built UI (2026-11-07). Mirrors the :root variables in
    /// Art/UI/SunodTheme.tss. Prefer the sn-* USS classes; use these only when a colour has to
    /// be set from code (per-RIASEC tints, runtime highlights, world-space bits).
    /// </summary>
    public static class vc_UITheme
    {
        public static readonly Color Cream = new Color32(248, 238, 216, 255);
        public static readonly Color CreamHi = new Color32(253, 248, 236, 255);
        public static readonly Color CreamLo = new Color32(236, 220, 190, 255);
        public static readonly Color PaperLine = new Color32(222, 200, 162, 255);
        public static readonly Color Brown = new Color32(139, 98, 66, 255);
        public static readonly Color BrownDark = new Color32(94, 64, 44, 255);
        public static readonly Color Ink = new Color32(74, 52, 38, 255);
        public static readonly Color InkMuted = new Color32(140, 113, 92, 255);
        public static readonly Color Green = new Color32(84, 158, 94, 255);
        public static readonly Color GreenDark = new Color32(46, 108, 60, 255);
        public static readonly Color GreenHi = new Color32(200, 232, 186, 255);
        public static readonly Color Gold = new Color32(244, 196, 80, 255);
        public static readonly Color GoldDark = new Color32(186, 132, 34, 255);
        public static readonly Color Red = new Color32(210, 88, 74, 255);
        public static readonly Color RedDark = new Color32(146, 50, 42, 255);
        public static readonly Color Blue = new Color32(84, 140, 212, 255);
        public static readonly Color Night = new Color32(92, 96, 170, 255);
        public static readonly Color Violet = new Color32(150, 110, 255, 255);   // "remembered / carried over"
        public static readonly Color Scrim = new Color(0.18f, 0.12f, 0.08f, 0.55f);

        /// <summary>RIASEC kind colour (R I A S E C), cream for anything else.</summary>
        public static Color Riasec(string letter)
        {
            switch ((letter ?? "").Trim().ToUpperInvariant())
            {
                case "R": return new Color32(217, 72, 80, 255);
                case "I": return new Color32(236, 140, 46, 255);
                case "A": return new Color32(148, 88, 214, 255);
                case "S": return new Color32(62, 166, 96, 255);
                case "E": return new Color32(58, 118, 214, 255);
                case "C": return new Color32(36, 172, 190, 255);
                default: return Cream;
            }
        }

        public static void Border(VisualElement ve, float width, Color color)
        {
            ve.style.borderTopWidth = width; ve.style.borderBottomWidth = width;
            ve.style.borderLeftWidth = width; ve.style.borderRightWidth = width;
            ve.style.borderTopColor = color; ve.style.borderBottomColor = color;
            ve.style.borderLeftColor = color; ve.style.borderRightColor = color;
        }

        public static void Radius(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r; ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r; ve.style.borderBottomRightRadius = r;
        }

        /// <summary>A themed button in code: label + optional variant ("primary", "gold", "danger", "night", "ghost", "small", "icon").</summary>
        public static Button MakeButton(string text, System.Action onClick, params string[] variants)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("sn-btn");
            foreach (string v in variants) if (!string.IsNullOrEmpty(v)) b.AddToClassList("sn-btn--" + v);
            vc_UIAnim.Press(b);
            return b;
        }

        /// <summary>A label with a type role class ("title", "subtitle", "body", "caption", "display").</summary>
        public static Label MakeLabel(string text, string role = "body")
        {
            var l = new Label(text);
            l.AddToClassList("sn-" + role);
            return l;
        }
    }
}
