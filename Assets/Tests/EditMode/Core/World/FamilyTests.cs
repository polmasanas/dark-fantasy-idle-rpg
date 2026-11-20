using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.World
{
    public class FamilyTests
    {
        private static EnemyFactory Factory() => new EnemyFactory(TestConfig.Load());
        private static EnemySnapshot Make(int region, int sub, bool boss = false, bool training = false, ulong seed = 1) =>
            Factory().Create(new Encounter(region, sub, region * 10 + sub, boss, training), new Pcg32(seed));

        [Test]
        public void Region1_IsNeutral()
        {
            for (int sub = 1; sub <= 5; sub++)
            {
                var e = Make(1, sub);
                Assert.AreEqual(Element.Physical, e.Element);
                Assert.AreEqual(Archetype.None, e.Archetype);
                Assert.AreEqual(0, e.Evasion);
                Assert.AreEqual("training", e.Family);
            }
            Assert.AreEqual(Archetype.None, Make(1, 5, boss: true).Archetype);
            Assert.AreEqual(Archetype.None, Make(1, 1, boss: true, training: true).Archetype);
        }

        [Test]
        public void Region2_ArchetypesFollowSubzone()
        {
            var expected = new[] { Archetype.Brute, Archetype.Marksman, Archetype.Caster, Archetype.Marksman, Archetype.Brute };
            for (int sub = 1; sub <= 5; sub++) Assert.AreEqual(expected[sub - 1], Make(2, sub).Archetype);
            Assert.AreEqual(Archetype.Brute, Make(2, 5, boss: true).Archetype);
            Assert.AreEqual("humanoids", Make(2, 1).Family);
        }

        [Test]
        public void Region3_Beasts_HaveEvasion()
        {
            var e = Make(3, 2);
            Assert.AreEqual("beasts", e.Family);
            Assert.AreEqual(TestConfig.Load().Families["beasts"].Evasion, e.Evasion, 1e-9);
            Assert.That(e.Evasion, Is.GreaterThan(0));
        }

        [Test]
        public void Region4_Elementals_RollFireNatureWater_NeverLightOrDark()
        {
            var factory = Factory();
            var rng = new Pcg32(5);
            int n = 10000, water = 0;
            for (int i = 0; i < n; i++)
            {
                var e = factory.Create(new Encounter(4, 1 + i % 4, 40, false), rng);
                Assert.AreEqual("elementals", e.Family);
                CollectionAssert.Contains(new[] { Element.Fire, Element.Nature, Element.Water }, e.Element); // (Is.AnyOf is missing from Unity's NUnit)
                Assert.AreEqual(0.75, e.Resist[(int)e.Element], 1e-9);
                Assert.AreEqual(3.0, e.ElementalLineMult, 1e-9);
                if (e.Element == Element.Water) water++;
            }
            Assert.AreEqual(0.5, water / (double)n, 0.03);
        }

        [Test]
        public void Region4_Subzone5_Wizards_Light()
        {
            var e = Make(4, 5);
            Assert.AreEqual("wizards", e.Family);
            Assert.AreEqual(Element.Light, e.Element);
            Assert.AreEqual(0.75, e.Resist[(int)Element.Light], 1e-9);
            Assert.AreEqual(Archetype.Caster, e.Archetype);
            Assert.AreEqual(3.0, e.ElementalLineMult, 1e-9);
        }

        [Test]
        public void Region4_Boss_IsAnElemental()
        {
            var e = Make(4, 5, boss: true);
            Assert.AreEqual("elementals", e.Family);
            Assert.AreEqual(Archetype.Caster, e.Archetype);
        }

        [Test]
        public void Region5_Undead_Dark()
        {
            var e = Make(5, 3);
            Assert.AreEqual("undead", e.Family);
            Assert.AreEqual(Element.Dark, e.Element);
            Assert.AreEqual(1.0, e.ElementalLineMult, 1e-9);
        }

        [Test]
        public void RegionBeyondList_UsesFallbackFamily()
        {
            Assert.AreEqual("humanoids", Make(9, 2).Family);
            Assert.AreEqual("humanoids", Make(400, 5, boss: true).Family);
        }

        [Test]
        public void Loader_UnknownFamily_ThrowsNamedError()
        {
            var o = JObject.Parse(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            o["regions"]["subzone_families"][1][0] = "goblins";
            var ex = Assert.Throws<BalanceConfigException>(() => BalanceConfigLoader.FromJson(o.ToString()));
            StringAssert.Contains("regions.subzone_families[1][0]", ex.Message);
        }

        [Test]
        public void Region1To3_FightRngUnchanged_ByFamilies()
        {
            // Only families with an element pool consume extra randomness, so earlier regions replay exactly.
            var a = new Pcg32(9);
            Factory().Create(new Encounter(3, 1, 30, false), a);
            var b = new Pcg32(9);
            b.Range(0, 1); // the variance roll only
            Assert.AreEqual(b.NextUInt(), a.NextUInt());
        }
    }
}
