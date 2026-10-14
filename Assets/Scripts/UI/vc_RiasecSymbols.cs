using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SunodGame.UI
{
    /// <summary>
    /// RIASEC category symbols (cropped from the intro cutscene badges), used as the
    /// placeholder icon for every skill button. Loaded from Resources/Riasec/riasec_{letter}.
    /// </summary>
    public static class vc_RiasecSymbols
    {
        private static readonly Dictionary<string, VectorImage> Cache = new Dictionary<string, VectorImage>();

        public static VectorImage Get(string letter)
        {
            string key = (letter ?? string.Empty).Trim().ToUpperInvariant();
            if (key.Length == 0) return null;
            if (Cache.TryGetValue(key, out VectorImage cached)) return cached;

            VectorImage image = Resources.Load<VectorImage>($"Riasec/riasec_{key}");
            Cache[key] = image;
            return image;
        }

        public static StyleBackground Background(string letter)
        {
            VectorImage image = Get(letter);
            return image != null ? new StyleBackground(image) : new StyleBackground(StyleKeyword.None);
        }
    }
}
