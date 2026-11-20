using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Items
{
    public class ElementalGearTests
    {
        private static ItemStats Stats() => new ItemStats(TestConfig.Load());

        private static ItemInstance Weapon(string type, ClassId? cls = null) =>
            new ItemInstance { Kind = ItemKind.MainHand, TypeId = type, ItemLevel = 1, Rarity = Rarity.Common, ClassBonus = cls };

        [Test]
        public void Bow_HasAttackSpeedLine()
        {
            var c = Stats().Contributions(Weapon("bow"), ClassId.Archer).ToList();
            Assert.That(c.Any(kv => kv.Key == GearStat.WeaponDamage && kv.Value > 0));
            Assert.That(c.Any(kv => kv.Key == GearStat.AttackSpeed && kv.Value > 0));
        }

        [Test]
        public void Staff_HasElementalLine()
        {
            var c = Stats().Contributions(Weapon("staff"), ClassId.Mage).ToList();
            Assert.That(c.Any(kv => kv.Key == GearStat.WeaponDamage && kv.Value > 0));
            Assert.That(c.Any(kv => kv.Key == GearStat.PctElemental && kv.Value > 0));
        }

        [Test]
        public void AddsFire_FlowsToCombatStats()
        {
            var cfg = TestConfig.Load();
            var item = Weapon("sword");
            item.Attributes.Add(new RolledAttribute("adds_fire", GearStat.AddsFire, 0.12));
            var inputs = new StatInputs { Class = ClassId.Fighter };
            new GearStats(cfg).Apply(inputs, new[] { item }, ClassId.Fighter);
            var stats = new StatCalculator(cfg).Compute(inputs);
            Assert.AreEqual(0.12, stats.ElementShare[(int)Element.Fire], 1e-12);
            Assert.AreEqual(ClassId.Fighter, stats.Class);
        }

        [Test]
        public void MageClassItem_GivesPctElemental_OnlyToTheMage()
        {
            var cfg = TestConfig.Load();
            var item = Weapon("sword", ClassId.Mage);
            Assert.That(Stats().Contributions(item, ClassId.Mage).Any(kv => kv.Key == GearStat.PctElemental && kv.Value == cfg.Items.ClassBonusPctElemental));
            Assert.That(!Stats().Contributions(item, ClassId.Fighter).Any(kv => kv.Key == GearStat.PctElemental));
            Assert.That(!Stats().Contributions(Weapon("sword", ClassId.Archer), ClassId.Archer).Any(kv => kv.Key == GearStat.PctElemental));
        }

        /// <summary>Share of main-hand items whose first rolled line is elemental.</summary>
        private static double FirstLineElementalShare(double mult)
        {
            var loot = new LootSystem(TestConfig.Load());
            var rng = new Pcg32(21);
            int n = 0, hits = 0;
            while (n < 20000)
            {
                var item = loot.Roll(10, rng, Rarity.Rare, 0, mult);
                if (item.Kind != ItemKind.MainHand || item.Attributes.Count == 0) continue;
                n++;
                var s = item.Attributes[0].Stat;
                if (s >= GearStat.AddsFire && s <= GearStat.AddsDark) hits++;
            }
            return hits / (double)n;
        }

        [TestCase(1.0)]
        [TestCase(3.0)]
        public void ElementalDrops_FirstLineFollowsWeights(double mult)
        {
            int all = TestConfig.Load().Items.Attributes.Count(a => a.Kinds.Contains(ItemKind.MainHand));
            const int elemental = 5;
            double expected = elemental * mult / (all - elemental + elemental * mult);
            Assert.AreEqual(expected, FirstLineElementalShare(mult), 0.02);
        }

        [Test]
        public void StarterWeapon_PerClass()
        {
            var loot = new LootSystem(TestConfig.Load());
            Assert.AreEqual("sword", loot.StarterWeaponType(ClassId.Fighter));
            Assert.AreEqual("bow", loot.StarterWeaponType(ClassId.Archer));
            Assert.AreEqual("staff", loot.StarterWeaponType(ClassId.Mage));
        }

        [Test]
        public void Config_AttributesAppendedAtEnd()
        {
            var ids = TestConfig.Load().Items.Attributes.Select(a => a.Id).ToList();
            CollectionAssert.AreEqual(new[] { "pct_damage", "primary", "crit_points", "crit_damage_points", "attack_speed", "counter", "pct_hp", "pct_armour", "vit", "gold_find", "luck", "drop_chance" }, ids.Take(12));
            CollectionAssert.AreEqual(new[] { "adds_fire", "adds_nature", "adds_water", "adds_light", "adds_dark" }, ids.Skip(12));
        }
    }
}
