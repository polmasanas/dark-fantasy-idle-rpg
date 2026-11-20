using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Loads sprites built by Tools/ArtBuild/build_art.py from Resources/Art. A missing file logs one
    /// warning per name and returns null; callers then draw a plain silhouette box instead of crashing.
    /// </summary>
    internal static class ArtLibrary
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var tex)) return tex;
            tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex == null) Debug.LogWarning("ArtLibrary: Resources/Art/" + name + ".png is missing (run Tools/ArtBuild/build_art.py).");
            Cache[name] = tex;
            return tex;
        }
    }
}
