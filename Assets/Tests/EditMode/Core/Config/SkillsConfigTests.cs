using System.Linq;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Config
{
    public class SkillsConfigTests
    {
        private static SkillsConfig Load() => TestConfig.Load().Skills;

        [Test]
        public void Loads_36Nodes_12PerClass()
        {
            var s = Load();
            Assert.AreEqual(36, s.Nodes.Count);
            foreach (var c in new[] { ClassId.Fighter, ClassId.Archer, ClassId.Mage })
                Assert.AreEqual(12, s.For(c).Count(), c.ToString());
        }

        [Test]
        public void EachClass_4Tiers_OneActiveTwoPassivesPerTier()
        {
            var s = Load();
            foreach (var c in new[] { ClassId.Fighter, ClassId.Archer, ClassId.Mage })
                for (int tier = 1; tier <= 4; tier++)
                {
                    var nodes = s.For(c).Where(n => n.Tier == tier).ToList();
                    Assert.AreEqual(1, nodes.Count(n => n.Kind == SkillKind.Active), c + " tier " + tier);
                    Assert.AreEqual(2, nodes.Count(n => n.Kind == SkillKind.Passive), c + " tier " + tier);
                }
        }

        [Test]
        public void ActivesMaxRank3_PassivesMaxRank5()
        {
            foreach (var n in Load().Nodes)
                Assert.AreEqual(n.Kind == SkillKind.Active ? 3 : 5, n.MaxRank, n.Id);
        }

        [Test]
        public void Actives_HavePositiveCooldown()
        {
            foreach (var n in Load().Nodes.Where(n => n.Kind == SkillKind.Active))
                Assert.Greater(n.CooldownS, 0, n.Id);
        }

        [Test]
        public void SuggestedOrder_ReferencesOnlyOwnClassNodes()
        {
            var s = Load();
            foreach (var c in new[] { ClassId.Fighter, ClassId.Archer, ClassId.Mage })
            {
                Assert.IsTrue(s.SuggestedOrder.ContainsKey(c), c.ToString());
                Assert.IsNotEmpty(s.SuggestedOrder[c]);
                foreach (var id in s.SuggestedOrder[c])
                {
                    var node = s.Find(id);
                    Assert.IsNotNull(node, id);
                    Assert.AreEqual(c, node.Class, id);
                }
            }
        }

        [Test]
        public void UnityCopy_ByteIdentical()
        {
            string tools = TestPaths.ReadRepoFile(TestPaths.SkillsConfigPath).Replace("\r\n", "\n");
            string unity = TestPaths.ReadRepoFile("Assets/_Game/Data/skills_config.json").Replace("\r\n", "\n");
            Assert.AreEqual(tools, unity,
                "Assets/_Game/Data/skills_config.json is out of date. Copy Tools/BalanceSim/skills_config.json over it.");
        }

        [Test]
        public void SkillRules_Loaded()
        {
            var r = TestConfig.Load().SkillRules;
            Assert.AreEqual(5, r.TierPoints);
            Assert.AreEqual(0.5, r.CooldownReductionCap, 1e-12);
            Assert.AreEqual(1.0, r.DotTickS, 1e-12);
            Assert.Greater(r.RespecBase, 0);
            Assert.Greater(r.RespecGrowth, 1);
            Assert.Greater(r.HeroDodgeCap, 0);
        }

        [Test]
        public void SkillRules_IsLastSection()
        {
            var root = JObject.Parse(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            Assert.AreEqual("skill_rules", root.Properties().Last().Name);
        }

        [Test]
        public void HeavySwing_Rank3_Percent3_5()
        {
            var hit = TestConfig.LoadWithSpecSkills().Skills.Find("fighter_heavy_swing").Effects.First(e => e.Kind == EffectKind.Hit);
            Assert.AreEqual(2.5, hit.At("percent", 1), 1e-12);
            Assert.AreEqual(3.5, hit.At("percent", 3), 1e-12);
        }

        [Test]
        public void Firebolt_IsFire()
        {
            var hit = Load().Find("mage_firebolt").Effects.First(e => e.Kind == EffectKind.Hit);
            Assert.AreEqual(DarkFantasy.Core.Combat.Element.Fire, hit.Element);
        }

        [Test]
        public void BadKind_ThrowsWithPath()
        {
            const string json = "{\"nodes\":[{\"id\":\"x\",\"class\":\"fighter\",\"tier\":1,\"kind\":\"active\",\"max_rank\":3," +
                                "\"cooldown_s\":8,\"rank_cdr\":0.05,\"effects\":[{\"kind\":\"zap\"}]}],\"suggested_order\":{}}";
            var e = Assert.Throws<BalanceConfigException>(() => SkillsConfigLoader.FromJson(json));
            StringAssert.Contains("nodes[0].effects[0].kind", e.Message);
        }

        [Test]
        public void DuplicateId_Throws()
        {
            const string node = "{\"id\":\"x\",\"class\":\"fighter\",\"tier\":1,\"kind\":\"passive\",\"max_rank\":5,\"effects\":[]}";
            string json = "{\"nodes\":[" + node + "," + node + "],\"suggested_order\":{}}";
            Assert.Throws<BalanceConfigException>(() => SkillsConfigLoader.FromJson(json));
        }
    }
}
