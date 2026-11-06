using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// UI V2 motion (2026-11-07). Every panel, popup and menu enters and leaves with motion,
    /// never an instant pop. Tweens run on unscaled time, so they also play while the game is
    /// paused (Time.timeScale = 0). One tween per element: starting a new one cancels the old.
    ///
    ///   vc_UIAnim.Show(panel);                       // pop in (fade + scale up with overshoot)
    ///   vc_UIAnim.Show(panel, vc_UIAnim.Enter.SlideUp);
    ///   vc_UIAnim.Hide(panel, () => ...);            // fade + shrink, then display:none
    ///   vc_UIAnim.Stagger(list.Children());          // children cascade in one after another
    ///   vc_UIAnim.Punch(label);                      // quick scale bounce (reward, value change)
    ///   vc_UIAnim.Shake(field);                      // horizontal shake (error)
    ///   var p = vc_UIAnim.Pulse(button); p.Stop();   // breathing attention loop
    ///   vc_UIAnim.Press(button);                     // squish on press, spring back (any element)
    ///   vc_UIAnim.CountUp(label, 0, 12, "{0} stars");
    ///
    /// Rules (UI_V2.md): enter 0.28-0.36 s ease-out-back, exit 0.16-0.2 s ease-in, list
    /// stagger 0.04 s, presses 0.08 s. Scrims fade (Enter.Fade), sheets pop or slide.
    /// </summary>
    public static class vc_UIAnim
    {
        public enum Enter { Pop, Fade, SlideUp, SlideDown, SlideLeft, SlideRight, Drop }
        public enum Exit { Shrink, Fade, SlideDown, SlideUp, SlideLeft, SlideRight }

        public const float EnterTime = 0.32f;
        public const float ExitTime = 0.18f;

        // ── easing ────────────────────────────────────────────────────────
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        // ── core tween ────────────────────────────────────────────────────

        /// <summary>A running tween; Stop() cancels it without calling its done callback.</summary>
        public sealed class Handle
        {
            internal IVisualElementScheduledItem Item;
            internal VisualElement Target;
            internal bool Stopped;
            public bool Running => !Stopped;
            public void Stop()
            {
                if (Stopped) return;
                Stopped = true;
                Item?.Pause();
                if (Target != null && Running_.TryGetValue(Target, out Handle h) && h == this) Running_.Remove(Target);
            }
        }

        private static readonly Dictionary<VisualElement, Handle> Running_ = new Dictionary<VisualElement, Handle>();

        /// <summary>
        /// Run step(0..1 eased) over seconds on unscaled time, after delay. Cancels any tween
        /// already running on this element (pass exclusive:false for decorative side tweens).
        /// </summary>
        public static Handle Tween(VisualElement ve, float seconds, Func<float, float> ease, Action<float> step,
                                   Action done = null, float delay = 0f, bool exclusive = true)
        {
            var h = new Handle { Target = exclusive ? ve : null };
            if (ve == null) { done?.Invoke(); h.Stopped = true; return h; }
            if (exclusive)
            {
                if (Running_.TryGetValue(ve, out Handle old)) old.Stop();
                Running_[ve] = h;
            }
            if (ease == null) ease = OutCubic;
            float start = Time.unscaledTime + Mathf.Max(0f, delay);
            seconds = Mathf.Max(0.0001f, seconds);
            if (delay <= 0f) step(ease(0f));
            h.Item = ve.schedule.Execute(() =>
            {
                if (h.Stopped) return;
                float now = Time.unscaledTime;
                if (now < start) return;
                float t = Mathf.Clamp01((now - start) / seconds);
                step(ease(t));
                if (t >= 1f)
                {
                    h.Stop();
                    done?.Invoke();
                }
            }).Every(0);
            return h;
        }

        public static void StopAll(VisualElement ve)
        {
            if (ve != null && Running_.TryGetValue(ve, out Handle h)) h.Stop();
        }

        // ── show / hide ───────────────────────────────────────────────────

        /// <summary>Make visible (display:flex) and play an entrance.</summary>
        public static Handle Show(VisualElement ve, Enter kind = Enter.Pop, float delay = 0f, Action done = null, float seconds = -1f)
        {
            if (ve == null) return null;
            ve.style.display = DisplayStyle.Flex;
            ve.style.visibility = Visibility.Visible;
            float dur = seconds > 0f ? seconds : (kind == Enter.Fade ? 0.22f : EnterTime);
            Vector2 from = Offset(kind, 48f);
            bool pops = kind == Enter.Pop || kind == Enter.Drop;
            float s0 = kind == Enter.Pop ? 0.82f : kind == Enter.Drop ? 1.12f : 1f;
            // start state now, so nothing flashes at full size during the delay
            ve.style.opacity = 0f;
            ve.style.scale = new Scale(new Vector2(s0, s0));
            ve.style.translate = new Translate(from.x, from.y);
            return Tween(ve, dur, kind == Enter.Fade ? (Func<float, float>)OutCubic : OutBack, e =>
            {
                float lin = Mathf.Clamp01(e);
                ve.style.opacity = Mathf.Clamp01(lin * 1.6f);
                float s = pops ? Mathf.LerpUnclamped(s0, 1f, e) : 1f;
                ve.style.scale = new Scale(new Vector2(s, s));
                ve.style.translate = new Translate(Mathf.LerpUnclamped(from.x, 0f, e), Mathf.LerpUnclamped(from.y, 0f, e));
            }, () => { ClearMotion(ve); done?.Invoke(); }, delay);
        }

        /// <summary>Play an exit, then display:none (unless keepLayout, then visibility:hidden).</summary>
        public static Handle Hide(VisualElement ve, Action done = null, Exit kind = Exit.Shrink, float delay = 0f, bool keepLayout = false, float seconds = -1f)
        {
            if (ve == null) { done?.Invoke(); return null; }
            if (ve.resolvedStyle.display == DisplayStyle.None) { done?.Invoke(); return null; }
            float dur = seconds > 0f ? seconds : ExitTime;
            Vector2 to = kind switch
            {
                Exit.SlideDown => new Vector2(0f, 48f),
                Exit.SlideUp => new Vector2(0f, -48f),
                Exit.SlideLeft => new Vector2(-64f, 0f),
                Exit.SlideRight => new Vector2(64f, 0f),
                _ => Vector2.zero
            };
            float s1 = kind == Exit.Shrink ? 0.88f : 1f;
            float o0 = ve.resolvedStyle.opacity;
            return Tween(ve, dur, InCubic, e =>
            {
                ve.style.opacity = Mathf.Lerp(o0, 0f, e);
                float s = Mathf.Lerp(1f, s1, e);
                ve.style.scale = new Scale(new Vector2(s, s));
                ve.style.translate = new Translate(to.x * e, to.y * e);
            }, () =>
            {
                if (keepLayout) ve.style.visibility = Visibility.Hidden;
                else ve.style.display = DisplayStyle.None;
                ClearMotion(ve);
                ve.style.opacity = StyleKeyword.Null;
                done?.Invoke();
            }, delay);
        }

        /// <summary>Show or hide by bool (convenience for Toggle-style code).</summary>
        public static void SetShown(VisualElement ve, bool shown, Enter enter = Enter.Pop, Exit exit = Exit.Shrink)
        {
            if (ve == null) return;
            bool visible = ve.resolvedStyle.display != DisplayStyle.None && ve.style.display != DisplayStyle.None;
            if (shown && !visible) Show(ve, enter);
            else if (!shown && visible) Hide(ve, null, exit);
        }

        /// <summary>Cascade children in one after another.</summary>
        public static void Stagger(IEnumerable<VisualElement> items, Enter kind = Enter.SlideUp, float step = 0.04f, float delay = 0f)
        {
            if (items == null) return;
            int i = 0;
            foreach (VisualElement ve in items)
            {
                if (ve == null || ve.style.display == DisplayStyle.None) continue;
                Show(ve, kind, delay + step * i, null, kind == Enter.Pop ? 0.3f : 0.26f);
                i++;
            }
        }

        // ── accents ───────────────────────────────────────────────────────

        /// <summary>Quick scale bounce: 1 -> 1+amount -> 1 with a springy settle.</summary>
        public static Handle Punch(VisualElement ve, float amount = 0.18f, float seconds = 0.35f)
        {
            if (ve == null) return null;
            return Tween(ve, seconds, t => t, t =>
            {
                float s = 1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
                ve.style.scale = new Scale(new Vector2(s, s));
            }, () => ve.style.scale = StyleKeyword.Null, 0f, false);
        }

        /// <summary>Horizontal shake that settles (wrong input, can't do that).</summary>
        public static Handle Shake(VisualElement ve, float px = 10f, float seconds = 0.36f)
        {
            if (ve == null) return null;
            return Tween(ve, seconds, t => t, t =>
            {
                float x = Mathf.Sin(t * Mathf.PI * 6f) * px * (1f - t);
                ve.style.translate = new Translate(x, 0f);
            }, () => ve.style.translate = StyleKeyword.Null, 0f, false);
        }

        /// <summary>Breathing loop (scale 1..1+amount). Stop() the handle to end it.</summary>
        public static Handle Pulse(VisualElement ve, float amount = 0.06f, float period = 1.1f)
        {
            var h = new Handle();
            if (ve == null) { h.Stopped = true; return h; }
            float t0 = Time.unscaledTime;
            h.Item = ve.schedule.Execute(() =>
            {
                if (h.Stopped) return;
                float k = (Mathf.Sin((Time.unscaledTime - t0) / period * Mathf.PI * 2f) + 1f) * 0.5f;
                float s = 1f + amount * k;
                ve.style.scale = new Scale(new Vector2(s, s));
            }).Every(0);
            return h;
        }

        /// <summary>Stop a pulse and restore the element's scale.</summary>
        public static void StopPulse(ref Handle h, VisualElement ve)
        {
            if (h != null) h.Stop();
            h = null;
            if (ve != null) ve.style.scale = StyleKeyword.Null;
        }

        /// <summary>Squish on pointer down, springy release on up. Safe to call once per element.</summary>
        public static void Press(VisualElement ve, float squish = 0.92f)
        {
            if (ve == null || ve.ClassListContains("sn-press-bound")) return;
            ve.AddToClassList("sn-press-bound");
            ve.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!ve.enabledInHierarchy) return;
                Tween(ve, 0.08f, OutCubic, e =>
                {
                    float s = Mathf.Lerp(1f, squish, e);
                    ve.style.scale = new Scale(new Vector2(s, s));
                }, null, 0f, false);
            }, TrickleDown.TrickleDown);
            EventCallback<EventBase> release = _ =>
            {
                float from = ve.resolvedStyle.scale.value.x;
                Tween(ve, 0.32f, OutElastic, e =>
                {
                    float s = Mathf.LerpUnclamped(from, 1f, e);
                    ve.style.scale = new Scale(new Vector2(s, s));
                }, () => ve.style.scale = StyleKeyword.Null, 0f, false);
            };
            ve.RegisterCallback<PointerUpEvent>(e => release(e), TrickleDown.TrickleDown);
            ve.RegisterCallback<PointerLeaveEvent>(e => { if (ve.resolvedStyle.scale.value.x < 0.999f) release(e); });
        }

        /// <summary>Press() on every Button under root (call after building a screen).</summary>
        public static void PressAll(VisualElement root)
        {
            root?.Query<Button>().ForEach(b => Press(b));
        }

        /// <summary>Animate a number in a label (stars, XP, counts). format uses {0}.</summary>
        public static Handle CountUp(Label label, int from, int to, string format = "{0}", float seconds = 0.6f, float delay = 0f)
        {
            if (label == null) return null;
            return Tween(label, seconds, OutCubic, e =>
                label.text = string.Format(format, Mathf.RoundToInt(Mathf.Lerp(from, to, e))),
                () => { label.text = string.Format(format, to); Punch(label, 0.12f, 0.25f); }, delay, false);
        }

        /// <summary>Smoothly move a progress fill's width to pct (0..100).</summary>
        public static Handle FillTo(VisualElement fill, float pct, float seconds = 0.45f)
        {
            if (fill == null) return null;
            float from = fill.style.width.keyword == StyleKeyword.Undefined && fill.style.width.value.unit == LengthUnit.Percent
                ? fill.style.width.value.value : 0f;
            return Tween(fill, seconds, OutCubic, e => fill.style.width = Length.Percent(Mathf.Lerp(from, pct, e)), null, 0f, false);
        }

        // ── helpers ───────────────────────────────────────────────────────

        private static Vector2 Offset(Enter kind, float d) => kind switch
        {
            Enter.SlideUp => new Vector2(0f, d),
            Enter.SlideDown => new Vector2(0f, -d),
            Enter.SlideLeft => new Vector2(d * 1.3f, 0f),
            Enter.SlideRight => new Vector2(-d * 1.3f, 0f),
            Enter.Drop => new Vector2(0f, -d * 0.6f),
            _ => Vector2.zero
        };

        private static void ClearMotion(VisualElement ve)
        {
            ve.style.scale = StyleKeyword.Null;
            ve.style.translate = StyleKeyword.Null;
            ve.style.opacity = StyleKeyword.Null;
        }
    }
}
