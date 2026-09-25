using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.Cutscene
{
    [CreateAssetMenu(menuName = "SUNOD/Cutscene Data")]
    public class vc_CutsceneData : ScriptableObject
    {
        public float charsPerSecond = 40f;
        public float punctuationPause = 0.12f;
        public float fadeDuration = 0.35f;

        public List<Cut> cuts = new List<Cut>();

        [Serializable]
        public class Cut
        {
            public string name;

            [Tooltip("Fills the letterbox bars on non-16:9 screens. Sample from the background's edge color.")]
            public Color stageColor = Color.black;

            [Tooltip("Drawn in list order, first = back.")]
            public List<Layer> layers = new List<Layer>();

            [TextArea(2, 4)]
            public string[] pages;
        }

        [Serializable]
        public class Layer
        {
            public VectorImage image;

            [Tooltip("Position/size in 320x180 stage units = the SVG's viewBox. Full-canvas layers = 0,0,320,180.")]
            public Rect rect = new Rect(0, 0, 320, 180);

            public bool clipCircle;

            [Tooltip("Bounding box of the clip circle, in stage units.")]
            public Rect clipRect;

            [Tooltip("Stage-unit pivot used for Rotate tracks.")]
            public Vector2 pivot;

            [Tooltip("Optional square 9-slice frame (e.g. the shared sl_button icon frame) drawn behind this layer, static - never animated by tracks. Leave null for no frame.")]
            public Sprite frameSprite;

            [Tooltip("Stage-unit rect for the frame (independent of this layer's own rect/animation).")]
            public Rect frameRect;

            public List<Track> tracks = new List<Track>();
        }

        public enum TrackProperty { TranslateX, TranslateY, Opacity, Rotate, Scale }

        [Serializable]
        public class Key
        {
            [Range(0f, 1f)] public float t01;
            public float value;

            public Key() { }
            public Key(float t, float v) { t01 = t; value = v; }
        }

        [Serializable]
        public class Track
        {
            public TrackProperty property;
            public List<Key> keys = new List<Key>();

            public float duration = 1f;

            [Tooltip("Seconds. Negative values behave like CSS negative delays - the track starts partway through its first cycle.")]
            public float delay;

            public bool loop;
            public bool alternate;

            [Tooltip("0 = smooth/linear. N = CSS steps(n) - the value jumps to the Nth step instead of interpolating.")]
            public int steps;

            public bool easeInOut;

            [Tooltip("CSS fill-mode 'both' for a play-once track: holds the last key's value once it finishes instead of reverting.")]
            public bool holdLastKey = true;
        }
    }
}
