using System.Collections.Generic;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Items
{
    public class GearStatsTests
    {
        private static GearStats Gear() => new GearStats(TestConfig.Load());

        private static ItemInstance Item(ItemKind kind, string type, int ilvl = 1, Rarity rarity = Rarity.Common, int upgrade = 0) =>
            new ItemInstance { Kind = kind, TypeId = type, ItemLevel = ilvl, Rarity = rarity, Upgrade = upgrade };

        [Test]
        public void Sword_ExponentialDamage_WithRarityAndUpgrade()
        {
            var i = new StatInputs { Class = ClassId.Fighter };
            Gear().Apply(i, new[] { Item(ItemKind.MainHand, "sword", 11, Rarity.Rare, 2) }, ClassId.Fighter);
            Assert.AreEqual(10 * System.Math.Pow(1.07, 10) * 1.45 * 1.10, i.WeaponDamage, 1e-9);
        }

        [Test]
        public void Greataxe_FixedAttackSpeedPenalty_IgnoresRarity()
        {
            var i = new StatInputs { Class = ClassId.Fighter };
            Gear().Apply(i, new[] { Item(ItemKind.MainHand, "greataxe", 30, Rarity.Mythical, 10) }, ClassId.Fighter);
            Assert.AreEqual(-0.10, i.AttackSpeedPct, 1e-12);
        }

        [Test]
        public void SumsArmourAndHp_FromSeveralPieces()
        {
            var i = new StatInputs { Class = ClassId.Fighter };
            Gear().Apply(i, new[] { Item(ItemKind.Chest, "heavy"), Item(ItemKind.Boots, "medium") }, ClassId.Fighter);
            Assert.AreEqual(7.8 + 6.0, i.GearArmour, 1e-9);
            Assert.AreEqual(9.6 + 12.0, i.GearHp, 1e-9);
        }

        [Test]
        public void Attributes_AddToTheirStats()
        {
            var item = Item(ItemKind.Amulet, "ward");
            item.Attributes.Add(new RolledAttribute("luck", GearStat.Luck, 7));
            item.Attributes.Add(new RolledAttribute("primary", GearStat.Primary, 4));
            var i = new StatInputs { Class = ClassId.Fighter, Primary = 10 };
            Gear().Apply(i, new[] { item }, ClassId.Fighter);
            Assert.AreEqual(7, i.Luck, 1e-12);
            Assert.AreEqual(14, i.Primary, 1e-12);
            Assert.AreEqual(0.05, i.PctHp, 1e-12); // Ward base line
        }

        [Test]
        public void ClassBonus_OnlyForItsClass()
        {
            var item = Item(ItemKind.Helmet, "medium");
            item.ClassBonus = ClassId.Fighter;
            var fighter = new StatInputs { Class = ClassId.Fighter };
            Gear().Apply(fighter, new[] { item }, ClassId.Fighter);
            Assert.AreEqual(0.08, fighter.PctDamage, 1e-12);
            var mage = new StatInputs { Class = ClassId.Mage };
            Gear().Apply(mage, new[] { item }, ClassId.Mage);
            Assert.AreEqual(0.0, mage.PctDamage, 1e-12);
        }

        [Test]
        public void PctArmour_IncreasesArmour()
        {
            var calc = new StatCalculator(TestConfig.Load());
            var baseArmour = calc.Compute(new StatInputs { Class = ClassId.Fighter, GearArmour = 100 }).Armour;
            var more = calc.Compute(new StatInputs { Class = ClassId.Fighter, GearArmour = 100, PctArmour = 0.1 }).Armour;
            Assert.AreEqual(baseArmour * 1.1, more, 1e-9);
        }

        [Test]
        public void PowerScore_PrefersStrongerWeaponAndMoreHp()
        {
            var cfg = TestConfig.Load();
            var calc = new StatCalculator(cfg);
            var score = new PowerScore(cfg);
            double weak = score.Of(calc.Compute(new StatInputs { Class = ClassId.Fighter, WeaponDamage = 10 }), 10);
            double strong = score.Of(calc.Compute(new StatInputs { Class = ClassId.Fighter, WeaponDamage = 15 }), 10);
            double tanky = score.Of(calc.Compute(new StatInputs { Class = ClassId.Fighter, WeaponDamage = 10, GearHp = 200 }), 10);
            Assert.That(strong, Is.GreaterThan(weak));
            Assert.That(tanky, Is.GreaterThan(weak));
        }
    }
}
