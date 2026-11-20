using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Look
{
    /// <summary>One body part of a character sprite: drawn on the full canvas, rotating around its pivot.</summary>
    public sealed class RigPart
    {
        public string Id;

        /// <summary>Pivot as a share of the canvas width / height (0 = left / top).</summary>
        public double PivotX;
        public double PivotY;
        public bool Glow;
    }

    public sealed class Rig
    {
        public double Aspect;

        /// <summary>Padding of each part's aura canvas on every side, as a share of the character height.</summary>
        public double AuraPad;

        /// <summary>Parts back to front.</summary>
        public RigPart[] Parts;
    }

    /// <summary>Reads Resources/Art/rigs.json, written by Tools/ArtBuild/build_art.py.</summary>
    public sealed class RigLibrary
    {
        private readonly Dictionary<string, Rig> _rigs = new Dictionary<string, Rig>();

        public Rig Get(string body) => body != null && _rigs.TryGetValue(body, out var r) ? r : null;

        public static RigLibrary FromJson(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new LookConfigException("rigs.json is not valid JSON: " + e.Message);
            }
            var lib = new RigLibrary();
            foreach (var prop in root.Properties())
            {
                string path = "rigs." + prop.Name;
                if (!(prop.Value is JObject o) || !(o["parts"] is JArray parts) || o["aspect"] == null || o["aura_pad"] == null)
                    throw new LookConfigException(path + ": needs aspect, aura_pad and parts");
                var list = new List<RigPart>();
                foreach (var p in parts)
                {
                    if (!(p is JObject po) || !(po["pivot"] is JArray pivot) || pivot.Count != 2 || po["id"] == null)
                        throw new LookConfigException(path + ".parts: each part needs id and pivot [x, y]");
                    list.Add(new RigPart { Id = (string)po["id"], PivotX = (double)pivot[0], PivotY = (double)pivot[1], Glow = (bool?)po["glow"] ?? false });
                }
                lib._rigs[prop.Name] = new Rig { Aspect = (double)o["aspect"], AuraPad = (double)o["aura_pad"], Parts = list.ToArray() };
            }
            return lib;
        }
    }
}
