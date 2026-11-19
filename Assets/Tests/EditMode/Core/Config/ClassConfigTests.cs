using System.IO;
using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Config
{
    public class ClassConfigTests
    {
        [Test]
        public void Classes_PatternsHave12PointsIn4Rows()
        {
            var c = TestConfig.Load();
            foreach (ClassId cls in new[] { ClassId.Fighter, ClassId.Archer, ClassId.Mage })
            {
                var pattern = c.Classes[cls].GrowthPattern;
                Assert.AreEqual(4, pattern.Length, cls.ToString());
                Assert.IsTrue(pattern.All(row => row.Length == 3), cls.ToString());
            }
        }

        [Test]
        public void Fighter_PatternTotals_6Str4Vit1Def1Counter()
        {
            var all = TestConfig.Load().Classes[ClassId.Fighter].GrowthPattern.SelectMany(r => r).ToList();
            Assert.AreEqual(6, all.Count(a => a == AttributeId.Str));
            Assert.AreEqual(4, all.Count(a => a == AttributeId.Vit));
            Assert.AreEqual(1, all.Count(a => a == AttributeId.Def));
            Assert.AreEqual(1, all.Count(a => a == AttributeId.Counter));
        }

        [Test]
        public void Primaries_MatchClasses()
        {
            var c = TestConfig.Load();
            Assert.AreEqual(AttributeId.Str, c.Classes[ClassId.Fighter].Primary);
            Assert.AreEqual(AttributeId.Dex, c.Classes[ClassId.Archer].Primary);
            Assert.AreEqual(AttributeId.Int, c.Classes[ClassId.Mage].Primary);
        }

        [Test]
        public void Presets_Loaded()
        {
            var presets = TestConfig.Load().AllocationPresets;
            CollectionAssert.IsSupersetOf(presets.Keys, new[] { "primary", "balanced", "tank" });
            Assert.AreEqual(0.5, presets["balanced"]["primary"], 1e-12);
            Assert.AreEqual(0.25, presets["tank"]["counter"], 1e-12);
        }

        [Test]
        public void ConfigCopy_MatchesToolsConfig()
        {
            // Unity loads Assets/_Game/Data/balance_config.json; the source of truth is Tools/BalanceSim.
            string tools = TestPaths.ReadRepoFile(TestPaths.ConfigPath).Replace("\r\n", "\n");
            string unity = TestPaths.ReadRepoFile("Assets/_Game/Data/balance_config.json").Replace("\r\n", "\n");
            Assert.AreEqual(tools, unity,
                "Assets/_Game/Data/balance_config.json is out of date. Copy Tools/BalanceSim/balance_config.json over it.");
        }
    }
}
