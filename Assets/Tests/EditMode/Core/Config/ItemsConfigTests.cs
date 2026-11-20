using System;
using System.Linq;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Items;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Config
{
    public class ItemsConfigTests
    {
        [Test]
        public void EveryKind_HasThreeTypes_MainHandAlsoBowAndStaff()
        {
            var items = TestConfig.Load().Items;
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
                Assert.AreEqual(kind == ItemKind.MainHand ? 5 : 3, items.TypesFor(kind).Count, kind.ToString());
            CollectionAssert.AreEquivalent(new[] { "sword", "greataxe", "dagger", "bow", "staff" },
                items.TypesFor(ItemKind.MainHand).Select(t => t.Id));
            CollectionAssert.AreEquivalent(new[] { "heavy", "medium", "light" },
                items.TypesFor(ItemKind.Gloves).Select(t => t.Id));
        }

        [Test]
        public void AttributePool_MatchesDecisionM3_3_PlusElementalLines()
        {
            var pool = TestConfig.Load().Items.Attributes;
            Assert.AreEqual(12 + 5, pool.Count); // M3-3 pool + five elemental lines (M5-1)
            var crit = pool.Single(a => a.Id == "crit_points");
            Assert.AreEqual(GearStat.CritPoints, crit.Stat);
            Assert.AreEqual(3.0, crit.Min);
            Assert.AreEqual(8.0, crit.Max);
            CollectionAssert.AreEquivalent(new[] { ItemKind.Gloves, ItemKind.Ring, ItemKind.Amulet }, crit.Kinds);
            Assert.AreEqual(0.1, pool.Single(a => a.Id == "vit").PerItemLevel, 1e-12);
        }

        [Test]
        public void ClassAndRollSettings_Loaded()
        {
            var items = TestConfig.Load().Items;
            Assert.AreEqual(0.30, items.ClassItemShare, 1e-12);
            Assert.AreEqual(0.08, items.ClassBonusPctDamage, 1e-12);
            Assert.AreEqual(0.25, items.RarityRollBias, 1e-12);
        }

        [Test]
        public void Loader_RejectsUnknownStat()
        {
            var json = JObject.Parse(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            json["items"]["attributes"][0]["stat"] = "flying";
            var ex = Assert.Throws<BalanceConfigException>(() => BalanceConfigLoader.FromJson(json.ToString()));
            StringAssert.Contains("flying", ex.Message);
        }
    }
}
