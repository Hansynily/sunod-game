using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

namespace SunodGame.Cutscene
{
    /// <summary>
    /// Drives the UI Toolkit intro cutscene: builds each cut's layers from
    /// vc_CutsceneData, evaluates their CSS-keyframe-equivalent tracks every frame,
    /// and runs the tap-through visual-novel dialog on top.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class vc_CutscenePlayer : MonoBehaviour
    {
        private const float StageWidth = 320f;
        private const float StageHeight = 180f;

        [SerializeField] private vc_CutsceneData data;

        private VisualElement _root;
        private VisualElement _cutsceneRoot;
        private VisualElement _stage;
        private VisualElement _fadeOverlay;
        private VisualElement _dialogBox;
        private Label _dialogText;
        private VisualElement _nextIndicator;
        private Button _btnSkip;

        private readonly List<LayerRuntime> _layerRuntimes = new List<LayerRuntime>();
        private readonly List<(VisualElement element, Rect rect)> _frameElements = new List<(VisualElement, Rect)>();

        private int _cutIndex = -1;
        private int _pageIndex;
        private bool _typing;
        private bool _transitioning;
        private bool _finished;
        private float _cutStartTime;
        private float _stageScale = 1f;
        private Coroutine _typeRoutine;
        private Coroutine _fadeRoutine;

        private class LayerRuntime
        {
            public vc_CutsceneData.Layer data;
            public VisualElement container; // clip wrapper if clipCircle, else same as element
            public VisualElement element;
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;

            _cutsceneRoot = _root.Q<VisualElement>("cutscene-root");
            _stage = _root.Q<VisualElement>("stage");
            _fadeOverlay = _root.Q<VisualElement>("fade-overlay");
            _dialogBox = _root.Q<VisualElement>("dialog-box");
            _dialogText = _root.Q<Label>("dialog-text");
            _nextIndicator = _root.Q<VisualElement>("next-indicator");
            _btnSkip = _root.Q<Button>("btn-skip");

            _dialogBox?.RegisterCallback<PointerUpEvent>(OnTap);
            _btnSkip?.RegisterCallback<ClickEvent>(_ => Finish());
            _cutsceneRoot?.RegisterCallback<GeometryChangedEvent>(_ => LayoutStage());
        }

        private void OnDisable()
        {
            _dialogBox?.UnregisterCallback<PointerUpEvent>(OnTap);
        }

        private void Start()
        {
            if (data == null || data.cuts == null || data.cuts.Count == 0)
            {
                Debug.LogWarning("[vc_CutscenePlayer] No cutscene data assigned.");
                return;
            }

            ShowCut(0);
        }

        private void Update()
        {
            EvaluateLayers();

            // Editor/desktop testing helpers (brief: Space/Enter = tap, Escape = Skip).
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                    OnTap(null);
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    Finish();
            }

            if (_nextIndicator != null && !_nextIndicator.ClassListContains("hidden"))
            {
                float bob = Mathf.Floor(Mathf.Repeat(Time.unscaledTime, 0.8f) / 0.4f) * -2f; // steps(2), 0/-2px
                _nextIndicator.style.translate = new Translate(0, bob);
            }
        }

        // ── Stage layout ─────────────────────────────────────────────────────

        private void LayoutStage()
        {
            if (_stage == null || _cutsceneRoot == null) return;

            float availW = _cutsceneRoot.resolvedStyle.width;
            float availH = _cutsceneRoot.resolvedStyle.height;
            if (float.IsNaN(availW) || float.IsNaN(availH) || availW <= 0f || availH <= 0f) return;

            // Fixed 16:9 stage, scaled to fit and centered - blended bars (stageColor) fill
            // the rest on off-ratio screens (T24: 20:9 phones).
            float stageW = availW;
            float stageH = stageW * (StageHeight / StageWidth);
            if (stageH > availH)
            {
                stageH = availH;
                stageW = stageH * (StageWidth / StageHeight);
            }

            _stage.style.width = stageW;
            _stage.style.height = stageH;
            _stageScale = stageW / StageWidth;

            foreach (LayerRuntime lr in _layerRuntimes)
                PositionLayer(lr);

            foreach ((VisualElement element, Rect rect) in _frameElements)
            {
                element.style.left = rect.x * _stageScale;
                element.style.top = rect.y * _stageScale;
                element.style.width = rect.width * _stageScale;
                element.style.height = rect.height * _stageScale;
            }
        }

        private void PositionLayer(LayerRuntime lr)
        {
            vc_CutsceneData.Layer data = lr.data;

            if (data.clipCircle)
            {
                Rect clip = data.clipRect;
                lr.container.style.left = clip.x * _stageScale;
                lr.container.style.top = clip.y * _stageScale;
                lr.container.style.width = clip.width * _stageScale;
                lr.container.style.height = clip.height * _stageScale;

                lr.element.style.left = (data.rect.x - clip.x) * _stageScale;
                lr.element.style.top = (data.rect.y - clip.y) * _stageScale;
                lr.element.style.width = data.rect.width * _stageScale;
                lr.element.style.height = data.rect.height * _stageScale;
            }
            else
            {
                lr.element.style.left = data.rect.x * _stageScale;
                lr.element.style.top = data.rect.y * _stageScale;
                lr.element.style.width = data.rect.width * _stageScale;
                lr.element.style.height = data.rect.height * _stageScale;
            }

            Vector2 localPivotPx = new Vector2(
                (data.pivot.x - data.rect.x) * _stageScale,
                (data.pivot.y - data.rect.y) * _stageScale);
            lr.element.style.transformOrigin = new TransformOrigin(localPivotPx.x, localPivotPx.y);
        }

        // ── Cut build / flow ──────────────────────────────────────────────────

        private void Build(vc_CutsceneData.Cut cut)
        {
            _stage.Clear();
            _layerRuntimes.Clear();
            _frameElements.Clear();

            _cutsceneRoot.style.backgroundColor = cut.stageColor;

            if (cut.layers == null) return;

            foreach (vc_CutsceneData.Layer layerData in cut.layers)
            {
                if (layerData.frameSprite != null)
                {
                    var frame = new VisualElement { name = "cut-layer-frame", pickingMode = PickingMode.Ignore };
                    frame.style.position = Position.Absolute;
                    frame.style.backgroundImage = new StyleBackground(layerData.frameSprite);
                    frame.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                    frame.style.unitySliceLeft = 6;
                    frame.style.unitySliceTop = 6;
                    frame.style.unitySliceRight = 6;
                    frame.style.unitySliceBottom = 8;
                    frame.style.unitySliceScale = new StyleFloat(2f);
                    _stage.Add(frame);
                    _frameElements.Add((frame, layerData.frameRect));
                }

                if (layerData.image == null) continue;

                var element = new VisualElement { name = "cut-layer", pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.backgroundImage = new StyleBackground(layerData.image);
                element.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

                var runtime = new LayerRuntime { data = layerData, element = element };

                if (layerData.clipCircle)
                {
                    var clipContainer = new VisualElement { name = "cut-layer-clip", pickingMode = PickingMode.Ignore };
                    clipContainer.style.position = Position.Absolute;
                    clipContainer.style.overflow = Overflow.Hidden;
                    clipContainer.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
                    clipContainer.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
                    clipContainer.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
                    clipContainer.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
                    clipContainer.Add(element);
                    runtime.container = clipContainer;
                    _stage.Add(clipContainer);
                }
                else
                {
                    runtime.container = element;
                    _stage.Add(element);
                }

                _layerRuntimes.Add(runtime);
            }

            LayoutStage();
            _cutStartTime = Time.unscaledTime;
        }

        private void ShowCut(int index)
        {
            if (data.cuts == null || index < 0 || index >= data.cuts.Count) return;

            _cutIndex = index;
            vc_CutsceneData.Cut cut = data.cuts[index];

            if (index == 0)
            {
                Build(cut);
                if (cut.layers == null || cut.layers.Count == 0)
                    Debug.LogWarning($"[vc_CutscenePlayer] Cut '{cut.name}' has no layers - showing stageColor only.");
                ShowPage(0);
                return;
            }

            _transitioning = true;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTransition(cut));
        }

        private IEnumerator FadeTransition(vc_CutsceneData.Cut cut)
        {
            yield return Fade(0f, 1f);
            Build(cut);
            if (cut.layers == null || cut.layers.Count == 0)
                Debug.LogWarning($"[vc_CutscenePlayer] Cut '{cut.name}' has no layers - showing stageColor only.");
            yield return Fade(1f, 0f);
            _transitioning = false;
            ShowPage(0);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_fadeOverlay == null) yield break;
            float duration = Mathf.Max(0.01f, data.fadeDuration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _fadeOverlay.style.opacity = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _fadeOverlay.style.opacity = to;
        }

        // ── Dialog / typing ────────────────────────────────────────────────────

        private void ShowPage(int page)
        {
            _pageIndex = page;
            vc_CutsceneData.Cut cut = data.cuts[_cutIndex];

            _nextIndicator?.AddToClassList("hidden");

            string[] pages = cut.pages;
            string line = (pages != null && page >= 0 && page < pages.Length) ? pages[page] : string.Empty;

            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = StartCoroutine(TypeLine(line));
        }

        private IEnumerator TypeLine(string line)
        {
            _typing = true;
            if (_dialogText != null) _dialogText.text = string.Empty;

            float charDelay = data.charsPerSecond > 0f ? 1f / data.charsPerSecond : 0f;
            int shown = 0;

            while (shown < line.Length)
            {
                shown++;
                if (_dialogText != null) _dialogText.text = line.Substring(0, shown);

                char c = line[shown - 1];
                float wait = charDelay;
                if (c == '.' || c == ',' || c == '!' || c == '?') wait += data.punctuationPause;

                float elapsed = 0f;
                while (elapsed < wait)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            FinishTyping();
        }

        private void FinishTyping()
        {
            if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }

            vc_CutsceneData.Cut cut = data.cuts[_cutIndex];
            string[] pages = cut.pages;
            string line = (pages != null && _pageIndex >= 0 && _pageIndex < pages.Length) ? pages[_pageIndex] : string.Empty;
            if (_dialogText != null) _dialogText.text = line;

            if (_dialogBox != null && _dialogText != null)
            {
                // Content overflowing the box is a data problem (shorten the page), not
                // something to silently auto-shrink away.
                if (_dialogText.resolvedStyle.height > _dialogBox.resolvedStyle.height - 4f)
                    Debug.LogWarning($"[vc_CutscenePlayer] Page overflow - cut {_cutIndex} ('{cut.name}') page {_pageIndex}.");
            }

            _typing = false;
            _nextIndicator?.RemoveFromClassList("hidden");
        }

        private void OnTap(PointerUpEvent evt)
        {
            if (_transitioning || _finished) return;

            if (_typing)
            {
                FinishTyping();
                return;
            }

            vc_CutsceneData.Cut cut = data.cuts[_cutIndex];
            int pageCount = cut.pages != null ? cut.pages.Length : 0;

            if (_pageIndex + 1 < pageCount)
            {
                ShowPage(_pageIndex + 1);
            }
            else if (_cutIndex + 1 < data.cuts.Count)
            {
                ShowCut(_cutIndex + 1);
            }
            else
            {
                Finish();
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            vc_SessionTelemetry.Instance?.StartSession();
            SceneLoader.GoToPlay();
        }

        // ── Track evaluation ───────────────────────────────────────────────────

        private void EvaluateLayers()
        {
            float cutTime = Time.unscaledTime - _cutStartTime;

            foreach (LayerRuntime lr in _layerRuntimes)
                ApplyLayer(lr, cutTime);
        }

        private void ApplyLayer(LayerRuntime lr, float cutTime)
        {
            vc_CutsceneData.Layer data = lr.data;
            if (data.tracks == null || data.tracks.Count == 0) return;

            float translateX = 0f, translateY = 0f, opacity = 1f, rotate = 0f, scale = 1f;
            bool txSet = false, tySet = false, opSet = false, rotSet = false, scaleSet = false;

            foreach (vc_CutsceneData.Track track in data.tracks)
            {
                bool alreadyTouched = track.property switch
                {
                    vc_CutsceneData.TrackProperty.TranslateX => txSet,
                    vc_CutsceneData.TrackProperty.TranslateY => tySet,
                    vc_CutsceneData.TrackProperty.Opacity => opSet,
                    vc_CutsceneData.TrackProperty.Rotate => rotSet,
                    vc_CutsceneData.TrackProperty.Scale => scaleSet,
                    _ => false
                };

                float local = cutTime - track.delay;
                bool started = local >= 0f;

                // A track that hasn't started yet and isn't the first one touching this
                // property just doesn't apply - whatever the earlier (already-active) track
                // wrote for this frame stands. This is what lets a play-once "entrance" track
                // (rise, pop) hand off cleanly to a later looping track (breathe, bob) on the
                // same property once its own delay elapses.
                if (!started && alreadyTouched) continue;

                float value = EvaluateTrack(track, local, started);

                switch (track.property)
                {
                    case vc_CutsceneData.TrackProperty.TranslateX: translateX = value; txSet = true; break;
                    case vc_CutsceneData.TrackProperty.TranslateY: translateY = value; tySet = true; break;
                    case vc_CutsceneData.TrackProperty.Opacity: opacity = value; opSet = true; break;
                    case vc_CutsceneData.TrackProperty.Rotate: rotate = value; rotSet = true; break;
                    case vc_CutsceneData.TrackProperty.Scale: scale = value; scaleSet = true; break;
                }
            }

            if (data.clipCircle && lr.container != lr.element)
            {
                // Clipped layers (e.g. the planet disk): TranslateY/Opacity move or fade the
                // whole clipped assembly (the container - mask position). TranslateX/Rotate
                // instead move the content WITHIN the fixed mask (e.g. the surface scrolling
                // inside a disk that isn't itself sliding sideways).
                lr.container.style.opacity = opacity;
                lr.container.style.translate = new Translate(0, translateY * _stageScale);
                lr.container.style.scale = new Scale(new Vector3(scale, scale, 1f));

                lr.element.style.translate = new Translate(translateX * _stageScale, 0);
                lr.element.style.rotate = new Rotate(new Angle(rotate, AngleUnit.Degree));
            }
            else
            {
                lr.element.style.opacity = opacity;
                lr.element.style.translate = new Translate(translateX * _stageScale, translateY * _stageScale);
                lr.element.style.rotate = new Rotate(new Angle(rotate, AngleUnit.Degree));
                lr.element.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }
        }

        private static float EvaluateTrack(vc_CutsceneData.Track track, float local, bool started)
        {
            if (track.keys == null || track.keys.Count == 0) return 0f;
            if (!started) return track.keys[0].value;

            float duration = Mathf.Max(0.0001f, track.duration);
            float phase;
            bool oddCycle = false;

            if (track.loop)
            {
                phase = Mathf.Repeat(local, duration);
                if (track.alternate)
                {
                    int cycle = Mathf.FloorToInt(local / duration);
                    oddCycle = cycle % 2 != 0;
                }
            }
            else
            {
                phase = Mathf.Min(local, duration);
            }

            float p = phase / duration;
            if (oddCycle) p = 1f - p;

            if (track.steps > 0)
                p = Mathf.Floor(p * track.steps) / track.steps;

            return InterpolateKeys(track.keys, p, track.easeInOut);
        }

        private static float InterpolateKeys(List<vc_CutsceneData.Key> keys, float p, bool easeInOut)
        {
            p = Mathf.Clamp01(p);

            if (keys.Count == 1) return keys[0].value;

            for (int i = 0; i < keys.Count - 1; i++)
            {
                vc_CutsceneData.Key a = keys[i];
                vc_CutsceneData.Key b = keys[i + 1];
                if (p < a.t01 || p > b.t01) continue;

                float span = b.t01 - a.t01;
                float local01 = span > 0f ? (p - a.t01) / span : 0f;
                if (easeInOut) local01 = Mathf.SmoothStep(0f, 1f, local01);
                return Mathf.Lerp(a.value, b.value, local01);
            }

            return p <= keys[0].t01 ? keys[0].value : keys[keys.Count - 1].value;
        }
    }
}
