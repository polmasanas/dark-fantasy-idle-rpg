using System;
using DarkFantasy.Core.Look;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Look
{
    public class LookTests
    {
        private static string Json() => TestPaths.ReadRepoFile(TestPaths.LookConfigPath);
        private static LookConfig Load() => LookConfigLoader.FromJson(Json());
        private static RegionLooks Regions() => new RegionLooks(Load());

        private static string Mutate(Action<JObject> change)
        {
            var o = JObject.Parse(Json());
            change(o);
            return o.ToString();
        }

        [Test]
        public void Loader_ReadsRepoLookConfig()
        {
            var c = Load();
            Assert.AreEqual(6, c.Biomes.Count);
            Assert.AreEqual("the Training Grounds", c.Regions[0].Name);
            Assert.AreEqual(0.6, c.Camera.BossZoom, 1e-12);
            Assert.AreEqual("#E8A24A", c.Classes[ClassId.Fighter].Glow);
        }

        [Test]
        public void Rgb_ParseAndScale()
        {
            Assert.AreEqual("#408000", Rgb.Parse("#80FF00").Scale(0.5).ToHex());
        }

        [Test]
        public void RegionLook_Region1_IsTrainingGroundsAtFullBrightness()
        {
            var r = Regions().For(1);
            Assert.AreEqual("the Training Grounds", r.Name);
            Assert.AreEqual("garrison", r.BiomeKey);
            Assert.AreEqual("training", r.Kit);
            Assert.AreEqual("Bram Hollis", r.Boss.Name);
            Assert.AreEqual(1.0, r.Depth, 1e-12);
            Assert.AreEqual("#5A5248", r.Palette.Far.ToHex());
        }

        [Test]
        public void RegionLook_DarkensWithDepth_AccentUnchanged()
        {
            var c = Load();
            var r = new RegionLooks(c).For(10);
            Assert.AreEqual(0.73, r.Depth, 1e-9);
            Assert.AreEqual(c.Biomes[r.BiomeKey].Accent.ToHex(), r.Palette.Accent.ToHex());
            Assert.AreNotEqual(c.Biomes[r.BiomeKey].Far.ToHex(), r.Palette.Far.ToHex());
        }

        [Test]
        public void RegionLook_DepthFloor()
        {
            Assert.AreEqual(0.45, Regions().For(40).Depth, 1e-12);
        }

        [Test]
        public void RegionLook_BeyondList_UsesFallbackAndCyclesBiome()
        {
            var looks = Regions();
            Assert.AreEqual("the Wilds, depth 7", looks.For(7).Name);
            Assert.AreEqual("ash", looks.For(7).BiomeKey);
            Assert.AreEqual("moss", looks.For(8).BiomeKey);
            Assert.AreEqual("humanoid", looks.For(7).Kit);
            Assert.DoesNotThrow(() => looks.For(400));
        }

        [Test]
        public void RegionLook_RegionZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Regions().For(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Regions().For(-3));
        }

        [Test]
        public void Loader_UnknownBiome_ThrowsNamedError()
        {
            var json = Mutate(o => o["regions"][0]["biome"] = "lava");
            var ex = Assert.Throws<LookConfigException>(() => LookConfigLoader.FromJson(json));
            StringAssert.Contains("regions[0].biome", ex.Message);
        }

        [Test]
        public void Loader_UnknownKit_ThrowsNamedError()
        {
            var json = Mutate(o => o["regions"][0]["kit"] = "goblins");
            var ex = Assert.Throws<LookConfigException>(() => LookConfigLoader.FromJson(json));
            StringAssert.Contains("regions[0].kit", ex.Message);
        }

        [Test]
        public void Loader_BadHex_ThrowsNamedError()
        {
            var json = Mutate(o => o["biomes"]["ash"]["far"] = "#12");
            var ex = Assert.Throws<LookConfigException>(() => LookConfigLoader.FromJson(json));
            StringAssert.Contains("biomes.ash.far", ex.Message);
        }
    }
}
