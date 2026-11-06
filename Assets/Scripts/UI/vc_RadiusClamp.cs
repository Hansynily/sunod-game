using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// UI V2 helper (2026-11-07). UI Toolkit draws a corner radius bigger than half the
    /// element's size as a squashed oval instead of capping it like CSS. The theme uses big
    /// radii for pills and circles ("as round as possible"), so this pass caps every element's
    /// radius at half its smaller side: pills and circles come out exact at any size.
    /// Runs a few times a second over every UIDocument; only touches elements that need it.
    /// </summary>
    public class vc_RadiusClamp : MonoBehaviour
    {
        private static vc_RadiusClamp _instance;
        private readonly List<UIDocument> _docs = new List<UIDocument>();
        private float _next;
        private int _tick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[UI RadiusClamp]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<vc_RadiusClamp>();
            SceneManager.sceneLoaded += (_, __) => _instance?.Refresh();
            _instance.Refresh();
        }

        private void Refresh()
        {
            _docs.Clear();
            _docs.AddRange(FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            if (++_tick % 10 == 0) Refresh();   // UIDocuments created mid-scene
            for (int i = _docs.Count - 1; i >= 0; i--)
            {
                UIDocument d = _docs[i];
                if (d == null) { _docs.RemoveAt(i); continue; }
                if (d.isActiveAndEnabled && d.rootVisualElement != null) Walk(d.rootVisualElement);
            }
        }

        private static void Walk(VisualElement ve)
        {
            if (ve.resolvedStyle.display == DisplayStyle.None) return;
            Clamp(ve);
            int n = ve.hierarchy.childCount;
            for (int i = 0; i < n; i++) Walk(ve.hierarchy[i]);
        }

        // the radius the stylesheet asked for, remembered before the first clamp
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Wanted> Asked =
            new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Wanted>();
        private sealed class Wanted { public float tl, tr, bl, br; }

        private static void Clamp(VisualElement ve)
        {
            Rect r = ve.layout;
            if (r.width <= 1f || r.height <= 1f) return;
            float half = Mathf.Min(r.width, r.height) * 0.5f;
            IResolvedStyle s = ve.resolvedStyle;
            if (!Asked.TryGetValue(ve, out Wanted w))
            {
                if (s.borderTopLeftRadius <= half + 0.5f && s.borderTopRightRadius <= half + 0.5f &&
                    s.borderBottomLeftRadius <= half + 0.5f && s.borderBottomRightRadius <= half + 0.5f) return;
                w = new Wanted { tl = s.borderTopLeftRadius, tr = s.borderTopRightRadius, bl = s.borderBottomLeftRadius, br = s.borderBottomRightRadius };
                Asked.Add(ve, w);
            }
            Set(s.borderTopLeftRadius, Mathf.Min(w.tl, half), v => ve.style.borderTopLeftRadius = v);
            Set(s.borderTopRightRadius, Mathf.Min(w.tr, half), v => ve.style.borderTopRightRadius = v);
            Set(s.borderBottomLeftRadius, Mathf.Min(w.bl, half), v => ve.style.borderBottomLeftRadius = v);
            Set(s.borderBottomRightRadius, Mathf.Min(w.br, half), v => ve.style.borderBottomRightRadius = v);
        }

        private static void Set(float now, float want, System.Action<float> apply)
        {
            if (Mathf.Abs(now - want) > 0.5f) apply(want);
        }
    }
}
