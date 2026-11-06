using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// UI V2 radial skill menu (2026-11-07). A round hub (open-hand icon) in the bottom-right;
    /// the six equipped skills (R I A S E C) fan out around it as petals.
    ///
    ///  - Tap the hub: the petals spring out and stay open. Tap a petal to use it (the menu stays
    ///    open, so "x3" presses are quick). Tap the hub again to fold them away.
    ///  - Hold the hub and drag: the petals spring out, the one under your thumb grows; let go
    ///    on it to use it (the menu then folds away). Aiming is forgiving: past the hub, the
    ///    direction alone picks the petal.
    ///
    /// Pure view: GameHUDUIToolkit feeds it the slot data and handles PetalActivated / PetalHeld.
    /// Petal buttons are named slot-btn-{i} (the tutorial pointer and vc_Sfx look for that).
    /// </summary>
    public class vc_SkillRadialMenu : VisualElement
    {
        public const int Count = 6;
        public const float Size = 480f;
        public const float HubSize = 140f;
        public const float PetalSize = 128f;
        public const float Radius = 172f;
        private const float DragStart = 22f;

        public event Action<int> PetalActivated;
        public event Action<int, bool> PetalHeld;
        public event Action<bool> OpenChanged;

        public bool IsOpen { get; private set; }
        public VisualElement Hub { get; }

        private readonly Button[] _petals = new Button[Count];
        private readonly VisualElement[] _faces = new VisualElement[Count];
        private readonly VisualElement[] _discs = new VisualElement[Count];
        private readonly VisualElement[] _icons = new VisualElement[Count];
        private readonly Label[] _names = new Label[Count];
        private readonly Label[] _badges = new Label[Count];
        private readonly VisualElement[] _shades = new VisualElement[Count];
        private readonly Label[] _cdLabels = new Label[Count];
        private readonly VisualElement[] _gems = new VisualElement[Count];
        private readonly bool[] _hasSkill = new bool[Count];
        private readonly bool[] _glow = new bool[Count];
        private readonly vc_UIAnim.Handle[] _glowPulse = new vc_UIAnim.Handle[Count];

        private readonly VisualElement _ring;
        private readonly VisualElement _burst;
        private readonly VisualElement _hand;
        private readonly Label _tag;
        private vc_UIAnim.Handle _hubPulse;

        private int _pointerId = -1;
        private Vector2 _downPos;
        private bool _dragging;
        private bool _wasOpen;
        private int _hover = -1;

        private static Vector2 Center => new Vector2(Size * 0.5f, Size * 0.5f);

        public static Vector2 PetalCenter(int i)
        {
            // slot 0 (R) on top, then clockwise: I, A, S (bottom), E, C
            float a = (-90f + 60f * i) * Mathf.Deg2Rad;
            return Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
        }

        public vc_SkillRadialMenu()
        {
            name = "skill-radial";
            AddToClassList("skill-radial");
            pickingMode = PickingMode.Ignore;

            for (int i = 0; i < Count; i++) BuildPetal(i);

            _ring = new VisualElement { name = "radial-ring", pickingMode = PickingMode.Ignore };
            _ring.AddToClassList("radial-ring");
            _ring.AddToClassList("sn-round");
            Add(_ring);

            _burst = new VisualElement { name = "radial-burst", pickingMode = PickingMode.Ignore };
            _burst.AddToClassList("radial-burst");
            _burst.AddToClassList("sn-round");
            Add(_burst);

            Hub = new VisualElement { name = "skill-hub" };
            Hub.AddToClassList("radial-hub");
            Hub.AddToClassList("sn-round");
            _hand = new vc_HandIcon { pickingMode = PickingMode.Ignore };
            _hand.AddToClassList("radial-hub-icon");
            Hub.Add(_hand);
            Add(Hub);

            _tag = new Label("Skills") { pickingMode = PickingMode.Ignore };
            _tag.AddToClassList("sn-badge");
            _tag.AddToClassList("radial-hub-tag");
            Add(_tag);

            Hub.RegisterCallback<PointerDownEvent>(OnHubDown);
            Hub.RegisterCallback<PointerMoveEvent>(OnHubMove);
            Hub.RegisterCallback<PointerUpEvent>(OnHubUp);
            Hub.RegisterCallback<PointerCaptureOutEvent>(_ => { if (_pointerId != -1) CancelGesture(); });

            SetClosedInstant();
        }

        // ── building ──────────────────────────────────────────────────────

        private void BuildPetal(int i)
        {
            int idx = i;
            var b = new Button { name = $"slot-btn-{i}", text = "" };
            b.AddToClassList("radial-petal");
            Vector2 c = PetalCenter(i);
            b.style.left = c.x - PetalSize * 0.5f;
            b.style.top = c.y - PetalSize * 0.5f;

            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("radial-face");
            face.AddToClassList("sn-round");
            b.Add(face);

            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.AddToClassList("radial-disc");
            disc.AddToClassList("sn-round");
            face.Add(disc);
            var icon = new VisualElement { name = $"slot-icon-{i}", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("radial-icon");
            disc.Add(icon);

            var nm = new Label("") { name = $"slot-name-{i}", pickingMode = PickingMode.Ignore };
            nm.AddToClassList("radial-name");
            face.Add(nm);

            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.AddToClassList("radial-shade");
            face.Add(shade);
            var cd = new Label("") { pickingMode = PickingMode.Ignore };
            cd.AddToClassList("radial-cd");
            face.Add(cd);

            var badge = new Label("Lv2") { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("sn-badge");
            badge.AddToClassList("radial-badge");
            b.Add(badge);

            var gem = new VisualElement { pickingMode = PickingMode.Ignore };
            gem.AddToClassList("radial-gem");
            b.Add(gem);

            b.clicked += () => { if (IsOpen && _pointerId == -1) Fire(idx, false); };
            b.RegisterCallback<PointerDownEvent>(_ => PetalHeld?.Invoke(idx, true), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => PetalHeld?.Invoke(idx, false), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerLeaveEvent>(_ => PetalHeld?.Invoke(idx, false));
            vc_UIAnim.Press(b, 0.9f);

            _petals[i] = b; _faces[i] = face; _discs[i] = disc; _icons[i] = icon; _names[i] = nm;
            _badges[i] = badge; _shades[i] = shade; _cdLabels[i] = cd; _gems[i] = gem;
            Add(b);
        }

        // ── data (fed by GameHUDUIToolkit) ────────────────────────────────

        public Button Petal(int i) => i >= 0 && i < Count ? _petals[i] : null;

        public void SetSlot(int i, string letter, StyleBackground symbol, string displayName, bool hasSkill, bool kept)
        {
            if (i < 0 || i >= Count) return;
            _hasSkill[i] = hasSkill;
            Color kind = vc_UITheme.Riasec(letter);
            vc_UITheme.Border(_discs[i], 4f, kind);
            _discs[i].style.backgroundColor = hasSkill ? (Color)vc_UITheme.CreamHi : vc_UITheme.CreamLo;
            _faces[i].style.borderBottomColor = kind;
            _icons[i].style.backgroundImage = symbol;
            _icons[i].style.opacity = hasSkill ? 1f : 0.35f;
            _names[i].text = hasSkill ? (displayName ?? "") : "";
            _gems[i].style.display = kept ? DisplayStyle.Flex : DisplayStyle.None;
            _petals[i].EnableInClassList("radial-petal--empty", !hasSkill);
        }

        public void SetLevel(int i, int level, bool max)
        {
            if (i < 0 || i >= Count) return;
            Label b = _badges[i];
            bool show = _hasSkill[i] && level >= 2;
            b.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            b.text = $"Lv{level}";
            b.EnableInClassList("sn-badge--gold", max);
            b.EnableInClassList("sn-badge--silver", !max);
        }

        public void LevelUpFlash(int i)
        {
            if (i < 0 || i >= Count) return;
            vc_UIAnim.Punch(_badges[i], 0.6f, 0.45f);
            vc_UIAnim.Punch(_petals[i], 0.15f, 0.4f);
        }

        public void SetCooldown(int i, float fraction, float secondsLeft)
        {
            if (i < 0 || i >= Count) return;
            bool cooling = fraction > 0f && _hasSkill[i];
            _shades[i].style.display = cooling ? DisplayStyle.Flex : DisplayStyle.None;
            _shades[i].style.height = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            _cdLabels[i].text = cooling && secondsLeft >= 1.05f ? Mathf.CeilToInt(secondsLeft).ToString() : "";
            _petals[i].EnableInClassList("radial-petal--cooling", cooling);
        }

        public void SetPetalEnabled(int i, bool on)
        {
            if (i < 0 || i >= Count || _petals[i].enabledSelf == on) return;
            _petals[i].SetEnabled(on);
        }

        /// <summary>The prep-unlocked skill: its petal breathes gold, and the folded hub glows too.</summary>
        public void SetGlow(int i, bool on)
        {
            if (i < 0 || i >= Count || _glow[i] == on) return;
            _glow[i] = on;
            _petals[i].EnableInClassList("radial-petal--glow", on);
            if (on) _glowPulse[i] = vc_UIAnim.Pulse(_faces[i], 0.07f, 0.8f);
            else vc_UIAnim.StopPulse(ref _glowPulse[i], _faces[i]);
            RefreshHubAlert();
        }

        private void RefreshHubAlert()
        {
            bool any = false;
            for (int i = 0; i < Count; i++) any |= _glow[i];
            bool alert = any && !IsOpen;
            Hub.EnableInClassList("radial-hub--alert", alert);
            if (alert && _hubPulse == null) _hubPulse = vc_UIAnim.Pulse(Hub, 0.08f, 0.8f);
            else if (!alert && _hubPulse != null) vc_UIAnim.StopPulse(ref _hubPulse, Hub);
        }

        // ── open / close ──────────────────────────────────────────────────

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            vc_UIAnim.StopPulse(ref _hubPulse, Hub);
            Hub.EnableInClassList("radial-hub--alert", false);
            Hub.AddToClassList("radial-hub--open");
            SunodGame.Core.vc_Sfx.Play("click", 0.55f, 0.05f);

            bool classic = vc_UIAnim.Classic;
            if (classic)
            {
                // Classic: the ring appears, petals step out a short way. Nothing else.
                vc_UIAnim.Show(_ring, vc_UIAnim.Enter.Fade, 0f, null, 0.1f);
                _tag.style.visibility = Visibility.Hidden;
                for (int i = 0; i < Count; i++)
                {
                    Button p = _petals[i];
                    p.pickingMode = PickingMode.Position;
                    p.style.visibility = Visibility.Visible;
                    Vector2 back = (Center - PetalCenter(i)) * 0.3f;
                    p.style.opacity = 0f;
                    vc_UIAnim.Tween(p, 0.14f, vc_UIAnim.OutCubic, e =>
                    {
                        p.style.translate = new Translate(back.x * (1f - e), back.y * (1f - e));
                        p.style.opacity = e;
                        p.style.scale = StyleKeyword.Null;
                    }, null, 0.012f * i);
                }
                OpenChanged?.Invoke(true);
                return;
            }

            // hub: quick squash + the hand tilts; gold ring fades in; a ring bursts outward
            vc_UIAnim.Tween(_hand, 0.3f, vc_UIAnim.OutBack, e =>
                _hand.style.rotate = new Rotate(new Angle(Mathf.LerpUnclamped(0f, -18f, e), AngleUnit.Degree)));
            vc_UIAnim.Show(_ring, vc_UIAnim.Enter.Pop, 0f, null, 0.28f);
            _burst.style.display = DisplayStyle.Flex;
            vc_UIAnim.Tween(_burst, 0.42f, vc_UIAnim.OutCubic, e =>
            {
                float s = Mathf.Lerp(0.9f, 1.9f, e);
                _burst.style.scale = new Scale(new Vector2(s, s));
                _burst.style.opacity = 0.8f * (1f - e);
            }, () => _burst.style.display = DisplayStyle.None);
            vc_UIAnim.Hide(_tag, null, vc_UIAnim.Exit.Fade, 0f, true, 0.12f);

            for (int i = 0; i < Count; i++)
            {
                Button p = _petals[i];
                p.pickingMode = PickingMode.Position;
                p.style.visibility = Visibility.Visible;
                Vector2 back = Center - PetalCenter(i);
                float delay = 0.028f * i;
                p.style.opacity = 0f;
                vc_UIAnim.Tween(p, 0.34f, vc_UIAnim.OutBack, e =>
                {
                    p.style.translate = new Translate(back.x * (1f - e), back.y * (1f - e));
                    float s = Mathf.LerpUnclamped(0.25f, 1f, e);
                    p.style.scale = new Scale(new Vector2(s, s));
                    p.style.opacity = Mathf.Clamp01(e * 2.2f);
                }, null, delay);
            }
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetHover(-1);
            Hub.RemoveFromClassList("radial-hub--open");
            if (vc_UIAnim.Classic)
            {
                _hand.style.rotate = StyleKeyword.Null;
                vc_UIAnim.Hide(_ring, null, vc_UIAnim.Exit.Fade, 0f, true, 0.08f);
                _tag.style.visibility = Visibility.Visible;
                _tag.style.opacity = StyleKeyword.Null;
                for (int i = 0; i < Count; i++)
                {
                    Button p = _petals[i];
                    p.pickingMode = PickingMode.Ignore;
                    Vector2 back = (Center - PetalCenter(i)) * 0.3f;
                    vc_UIAnim.Tween(p, 0.1f, vc_UIAnim.InCubic, e =>
                    {
                        p.style.translate = new Translate(back.x * e, back.y * e);
                        p.style.opacity = 1f - e;
                    }, () => p.style.visibility = Visibility.Hidden);
                }
                RefreshHubAlert();
                OpenChanged?.Invoke(false);
                return;
            }
            vc_UIAnim.Tween(_hand, 0.22f, vc_UIAnim.OutCubic, e =>
                _hand.style.rotate = new Rotate(new Angle(Mathf.Lerp(-18f, 0f, e), AngleUnit.Degree)));
            vc_UIAnim.Hide(_ring, null, vc_UIAnim.Exit.Shrink, 0f, true, 0.16f);
            vc_UIAnim.Show(_tag, vc_UIAnim.Enter.Fade, 0.1f);

            for (int i = 0; i < Count; i++)
            {
                Button p = _petals[i];
                p.pickingMode = PickingMode.Ignore;
                Vector2 back = Center - PetalCenter(i);
                float delay = 0.018f * (Count - 1 - i);
                vc_UIAnim.Tween(p, 0.18f, vc_UIAnim.InCubic, e =>
                {
                    p.style.translate = new Translate(back.x * e, back.y * e);
                    float s = Mathf.Lerp(1f, 0.25f, e);
                    p.style.scale = new Scale(new Vector2(s, s));
                    p.style.opacity = 1f - e;
                }, () => p.style.visibility = Visibility.Hidden, delay);
            }
            RefreshHubAlert();
            OpenChanged?.Invoke(false);
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        private void SetClosedInstant()
        {
            IsOpen = false;
            for (int i = 0; i < Count; i++)
            {
                Button p = _petals[i];
                p.pickingMode = PickingMode.Ignore;
                p.style.visibility = Visibility.Hidden;
                p.style.opacity = 0f;
            }
            _ring.style.visibility = Visibility.Hidden;
            _burst.style.display = DisplayStyle.None;
        }

        // ── gesture: tap = toggle, hold + drag = pick a petal and release to use it ──

        private void OnHubDown(PointerDownEvent e)
        {
            if (_pointerId != -1) return;
            _pointerId = e.pointerId;
            Hub.CapturePointer(e.pointerId);
            _downPos = this.WorldToLocal(e.position);
            _dragging = false;
            _wasOpen = IsOpen;
            vc_UIAnim.Tween(Hub, 0.08f, vc_UIAnim.OutCubic, k =>
            {
                float s = Mathf.Lerp(1f, vc_UIAnim.Classic ? 0.95f : 0.9f, k);
                Hub.style.scale = new Scale(new Vector2(s, s));
            }, null, 0f, false);
            if (!IsOpen) Open();
            e.StopPropagation();
        }

        private void OnHubMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointerId) return;
            Vector2 p = this.WorldToLocal(e.position);
            if (!_dragging && (p - _downPos).magnitude > DragStart) _dragging = true;
            if (_dragging) SetHover(PickPetal(p));
            e.StopPropagation();
        }

        private void OnHubUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointerId) return;
            Vector2 p = this.WorldToLocal(e.position);
            ReleaseHub();
            if (_dragging)
            {
                int pick = PickPetal(p);
                SetHover(-1);
                if (pick >= 0 && CanUse(pick)) Fire(pick, true);
                else Close();
            }
            else if (_wasOpen) Close();      // a tap on an open hub folds it away
            e.StopPropagation();
        }

        private void CancelGesture()
        {
            ReleaseHub();
            SetHover(-1);
        }

        private void ReleaseHub()
        {
            if (_pointerId != -1 && Hub.HasPointerCapture(_pointerId)) Hub.ReleasePointer(_pointerId);
            _pointerId = -1;
            float from = Hub.resolvedStyle.scale.value.x;
            bool classicRel = vc_UIAnim.Classic;
            vc_UIAnim.Tween(Hub, classicRel ? 0.08f : 0.34f, classicRel ? (System.Func<float, float>)vc_UIAnim.OutCubic : vc_UIAnim.OutElastic, k =>
            {
                float s = Mathf.LerpUnclamped(from, 1f, k);
                Hub.style.scale = new Scale(new Vector2(s, s));
            }, () => Hub.style.scale = StyleKeyword.Null, 0f, false);
        }

        private bool CanUse(int i) => _hasSkill[i] && _petals[i].enabledSelf;

        /// <summary>Petal under p (local coords): a direct hit, or past the hub, the direction.</summary>
        private int PickPetal(Vector2 p)
        {
            Vector2 d = p - Center;
            float dist = d.magnitude;
            if (dist < HubSize * 0.5f) return -1;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                float di = (p - PetalCenter(i)).magnitude;
                if (di < bestD) { bestD = di; best = i; }
            }
            if (bestD <= PetalSize * 0.7f) return best;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // -90 = up (UI y goes down)
            int sector = Mathf.RoundToInt((ang + 90f) / 60f);
            return ((sector % Count) + Count) % Count;
        }

        private void SetHover(int i)
        {
            if (i == _hover) return;
            if (_hover >= 0)
            {
                Button old = _petals[_hover];
                old.RemoveFromClassList("radial-petal--hover");
                float from = old.resolvedStyle.scale.value.x;
                vc_UIAnim.Tween(old, 0.14f, vc_UIAnim.OutCubic, e =>
                {
                    float s = Mathf.Lerp(from, 1f, e);
                    old.style.scale = new Scale(new Vector2(s, s));
                }, null, 0f, false);
            }
            _hover = i;
            if (i >= 0)
            {
                Button b = _petals[i];
                b.AddToClassList("radial-petal--hover");
                bool classicHover = vc_UIAnim.Classic;
                vc_UIAnim.Tween(b, classicHover ? 0.08f : 0.18f, classicHover ? (System.Func<float, float>)vc_UIAnim.OutCubic : vc_UIAnim.OutBack, e =>
                {
                    float s = Mathf.LerpUnclamped(1f, classicHover ? 1.07f : 1.16f, e);
                    b.style.scale = new Scale(new Vector2(s, s));
                }, null, 0f, false);
                SunodGame.Core.vc_Sfx.Play("click", 0.3f, 0.15f);
            }
        }

        private void Fire(int i, bool thenClose)
        {
            if (!CanUse(i)) { vc_UIAnim.Shake(_petals[i], 6f, 0.25f); return; }
            PetalActivated?.Invoke(i);

            // flash: the petal pops, a ring bursts from it
            Button b = _petals[i];
            b.style.scale = StyleKeyword.Null;
            vc_UIAnim.Punch(b, 0.22f, 0.32f);
            if (vc_UIAnim.Classic) { if (thenClose) schedule.Execute(Close).StartingIn(150); return; }
            var flash = new VisualElement { pickingMode = PickingMode.Ignore };
            flash.AddToClassList("radial-flash");
            Vector2 c = PetalCenter(i);
            flash.style.left = c.x - PetalSize * 0.5f;
            flash.style.top = c.y - PetalSize * 0.5f;
            vc_UITheme.Border(flash, 6f, vc_UITheme.Gold);
            Add(flash);
            vc_UIAnim.Tween(flash, 0.4f, vc_UIAnim.OutCubic, e =>
            {
                float s = Mathf.Lerp(0.95f, 1.55f, e);
                flash.style.scale = new Scale(new Vector2(s, s));
                flash.style.opacity = 1f - e;
            }, () => flash.RemoveFromHierarchy(), 0f, false);

            if (thenClose) schedule.Execute(Close).StartingIn(300);   // let the pop read first
        }
    }

    /// <summary>The hub's open-hand icon, drawn with Painter2D (crisp at any size, no texture).</summary>
    public class vc_HandIcon : VisualElement
    {
        public Color Fill = new Color32(253, 248, 236, 255);
        public Color Outline = new Color32(46, 108, 60, 255);
        public Color Heart = new Color32(244, 196, 80, 255);

        public vc_HandIcon()
        {
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            float k = Mathf.Min(r.width, r.height) / 100f;
            Vector2 o = new Vector2(r.x + (r.width - 100f * k) * 0.5f, r.y + (r.height - 100f * k) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * k;

            Painter2D p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            // (from, to, width) strokes: thumb, 4 fingers, palm
            var strokes = new (Vector2 a, Vector2 b, float w)[]
            {
                (P(27, 62), P(13, 43), 13f),
                (P(36, 50), P(35, 19), 12f),
                (P(48, 48), P(48, 11), 12f),
                (P(60, 49), P(61, 15), 12f),
                (P(71, 55), P(73, 27), 11f),
                (P(46, 69), P(60, 69), 38f),
            };
            // outline pass, then fill pass
            p.strokeColor = Outline;
            foreach (var s in strokes) Line(p, s.a, s.b, (s.w + 9f) * k);
            p.strokeColor = Fill;
            foreach (var s in strokes) Line(p, s.a, s.b, s.w * k);

            // a little gold heart in the palm: helping people
            p.fillColor = Heart;
            p.BeginPath();
            Vector2 h = P(53, 70);
            float hs = 9f * k;
            p.MoveTo(h + new Vector2(0f, hs * 0.9f));
            p.BezierCurveTo(h + new Vector2(-hs * 1.4f, -hs * 0.1f), h + new Vector2(-hs * 0.7f, -hs * 1.2f), h + new Vector2(0f, -hs * 0.35f));
            p.BezierCurveTo(h + new Vector2(hs * 0.7f, -hs * 1.2f), h + new Vector2(hs * 1.4f, -hs * 0.1f), h + new Vector2(0f, hs * 0.9f));
            p.ClosePath();
            p.Fill();
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, float w)
        {
            p.lineWidth = w;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }
    }
}
