using System.Collections.Generic;
using System.IO;
using DarkFantasy.Core.Look;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Look
{
    /// <summary>Every sprite named in look_config.json exists (built by Tools/ArtBuild/build_art.py).</summary>
    public class LookAssetTests
    {
        private const string ArtDir = "Assets/_Game/Resources/Art/";

        private static readonly string[] Scenery =
            { "bg_sky_gradient", "bg_far_walls", "bg_far_hills", "bg_near_yard", "bg_near_hills", "fx_glow" };

        [Test]
        public void EveryBodyAndBackgroundHasAPng()
        {
            var c = LookConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.LookConfigPath));
            var bodies = new HashSet<string>();
            foreach (var kit in c.Kits.Values) bodies.UnionWith(kit);
            foreach (var r in c.Regions) bodies.Add(r.Boss.Body);
            bodies.Add(c.Fallback.Boss.Body);
            bodies.Add(c.TrainingBoss.Body);
            foreach (var cls in c.Classes.Values) bodies.Add(cls.Body);

            string dir = Path.GetDirectoryName(TestPaths.RepoFile(TestPaths.LookConfigPath));
            string art = Path.GetFullPath(Path.Combine(dir, "../Resources/Art"));
            var missing = new List<string>();
            foreach (var b in bodies)
            {
                if (!File.Exists(Path.Combine(art, b + ".png"))) missing.Add(b + ".png");
                if (!File.Exists(Path.Combine(art, b + "_glow.png"))) missing.Add(b + "_glow.png");
            }
            foreach (var s in Scenery)
                if (!File.Exists(Path.Combine(art, s + ".png"))) missing.Add(s + ".png");
            CollectionAssert.IsEmpty(missing, "Run python3 Tools/ArtBuild/build_art.py (" + ArtDir + ")");
        }
    }
}
