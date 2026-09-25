using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    // Port of Assets/Art/MenuReference/stardew_bg_animated.js, plus the fly-up intro for the menu elements.
    [RequireComponent(typeof(UIDocument))]
    public class vc_MainMenuBackground : MonoBehaviour
    {
        [SerializeField] private float introStartDelayMs = 80f;
        [SerializeField] private float introStaggerMs = 90f;
        [SerializeField] private float introDurationMs = 700f;

        private StardewSkyElement _sky;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>().rootVisualElement;
            var root = doc.Q<VisualElement>("root") ?? doc;

            _sky = new StardewSkyElement();
            _sky.AddToClassList("menu-bg");
            root.Insert(0, _sky);

            // Removing intro-hidden before the panel's first layout skips the transition,
            // so wait until the root has real geometry.
            if (root.resolvedStyle.width > 0f && root.panel != null)
                PlayIntro(root);
            else
                root.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
        }

        private void OnFirstLayout(GeometryChangedEvent evt)
        {
            var root = (VisualElement)evt.currentTarget;
            if (root.resolvedStyle.width <= 0f) return;
            root.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
            PlayIntro(root);
        }

        private void OnDisable()
        {
            _sky?.RemoveFromHierarchy();
            _sky = null;
        }

        private void PlayIntro(VisualElement root)
        {
            var items = root.Query<VisualElement>(className: "intro-item").ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                long showAt = (long)(introStartDelayMs + i * introStaggerMs);
                item.schedule.Execute(() => item.RemoveFromClassList("intro-hidden")).StartingIn(showAt);
                // Drop the transition afterwards so button press feedback stays instant.
                item.schedule.Execute(() => item.RemoveFromClassList("intro-item")).StartingIn(showAt + (long)introDurationMs);
            }
        }

        private class StardewSkyElement : VisualElement
        {
            private static readonly Color[] SkyStops =
            {
                Hex(0x0b2a5c), Hex(0x2f6fb0), Hex(0x8fc7e8), Hex(0xd9f0e6)
            };
            private static readonly float[] SkyPos = { 0f, 0.45f, 0.75f, 1f };

            private static readonly Vector3[] Clouds =
            {
                new Vector3(0.12f, 0.18f, 1.6f), new Vector3(0.32f, 0.10f, 1.1f),
                new Vector3(0.62f, 0.22f, 1.8f), new Vector3(0.82f, 0.12f, 1.3f),
                new Vector3(0.48f, 0.30f, 1.0f), new Vector3(0.05f, 0.55f, 1.4f),
                new Vector3(0.78f, 0.62f, 1.6f), new Vector3(0.92f, 0.42f, 1.0f),
            };

            private static readonly Vector4[] CloudBlocks =
            {
                new Vector4(-4, 0, 10, 2), new Vector4(-3, -1, 8, 2), new Vector4(-1, -2, 5, 2),
                new Vector4(-5, 1, 12, 2), new Vector4(-6, 2, 14, 2),
            };

            private const int SkyBands = 48;
            private const int StarCount = 40;

            public StardewSkyElement()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                schedule.Execute(MarkDirtyRepaint).Every(33);
            }

            private void Draw(MeshGenerationContext mgc)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w <= 0f || h <= 0f) return;

                var p = mgc.painter2D;
                float t = Time.realtimeSinceStartup * 1000f;

                // sky gradient (banded, pixel-art style)
                float bandH = h / SkyBands;
                for (int i = 0; i < SkyBands; i++)
                    Rect(p, 0, i * bandH, w, bandH + 1f, SkyAt((i + 0.5f) / SkyBands));

                // twinkling stars in the top half
                for (int i = 0; i < StarCount; i++)
                {
                    float x = HashRand(i) * w;
                    float y = HashRand(i + 100) * h * 0.5f;
                    float twinkle = 0.5f + 0.5f * Mathf.Sin(t / 600f + i * 12.9f);
                    Rect(p, x, y, 2f, 2f, new Color(1f, 1f, 1f, 0.3f + twinkle * 0.5f));
                }

                // sun with rotating rays
                float cx = w * 0.78f, cy = h * 0.22f, r = Mathf.Min(w, h) * 0.05f;
                float angle = t / 6000f;
                var rayColor = new Color(1f, 0.957f, 0.761f, 0.35f);
                for (int i = 0; i < 8; i++)
                {
                    float a = angle + i / 8f * Mathf.PI * 2f;
                    Vector2 tip1 = Rotate(new Vector2(-r * 0.35f, -r * 2.4f), a);
                    Vector2 tip2 = Rotate(new Vector2(r * 0.35f, -r * 2.4f), a);
                    p.fillColor = rayColor;
                    p.BeginPath();
                    p.MoveTo(new Vector2(cx, cy));
                    p.LineTo(new Vector2(cx + tip1.x, cy + tip1.y));
                    p.LineTo(new Vector2(cx + tip2.x, cy + tip2.y));
                    p.ClosePath();
                    p.Fill();
                }
                Rect(p, cx - r, cy - r, r * 2f, r * 2f, Hex(0xffe28a));
                Rect(p, cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f, Hex(0xfff1b8));
                Rect(p, cx - r * 0.35f, cy - r * 0.35f, r * 0.7f, r * 0.7f, Color.white);

                // blocky clouds
                float unit = w / 220f;
                foreach (var c in Clouds)
                {
                    float ccx = c.x * w, ccy = c.y * h, s = c.z * unit;
                    foreach (var b in CloudBlocks)
                        Rect(p, ccx + b.x * s, ccy + b.y * s, b.z * s, b.w * s, Color.white);
                    Rect(p, ccx - 6f * s, ccy + 3f * s, 12f * s, 1.5f * s, Hex(0xd7e9f2));
                }
            }

            private static void Rect(Painter2D p, float x, float y, float w, float h, Color color)
            {
                x = Mathf.Round(x); y = Mathf.Round(y); w = Mathf.Round(w); h = Mathf.Round(h);
                p.fillColor = color;
                p.BeginPath();
                p.MoveTo(new Vector2(x, y));
                p.LineTo(new Vector2(x + w, y));
                p.LineTo(new Vector2(x + w, y + h));
                p.LineTo(new Vector2(x, y + h));
                p.ClosePath();
                p.Fill();
            }

            private static Color SkyAt(float f)
            {
                for (int i = 1; i < SkyPos.Length; i++)
                {
                    if (f <= SkyPos[i])
                        return Color.Lerp(SkyStops[i - 1], SkyStops[i], Mathf.InverseLerp(SkyPos[i - 1], SkyPos[i], f));
                }
                return SkyStops[SkyStops.Length - 1];
            }

            private static float HashRand(float seed)
            {
                float x = Mathf.Sin(seed * 999.9f) * 43758.5453f;
                return x - Mathf.Floor(x);
            }

            private static Vector2 Rotate(Vector2 v, float a)
            {
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
            }

            private static Color Hex(int rgb) =>
                new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
        }
    }
}
