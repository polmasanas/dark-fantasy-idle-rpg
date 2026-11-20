using System;
using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Items
{
    public class LootSystemTests
    {
        private static LootSystem Loot() => new LootSystem(TestConfig.Load());

        [Test]
        public void Roll_SameSeed_SameItem()
        {
            var a = Loot().Roll(40, new Pcg32(5));
            var b = Loot().Roll(40, new Pcg32(5));
            Assert.AreEqual(a.Kind, b.Kind);
            Assert.AreEqual(a.TypeId, b.TypeId);
            Assert.AreEqual(a.Rarity, b.Rarity);
            CollectionAssert.AreEqual(a.Attributes.Select(x => x.Value), b.Attributes.Select(x => x.Value));
            Assert.AreEqual(40, a.ItemLevel);
        }

        [Test]
        public void RarityDistribution_MatchesWeights_ChiSquare()
        {
            var loot = Loot();
            var rng = new Pcg32(11);
            double[] weights = TestConfig.Load().Gear.RarityWeights;
            double total = weights.Sum();
            const int n = 200000;
            var counts = new int[6];
            for (int i = 0; i < n; i++) counts[(int)loot.RollRarity(rng, Rarity.Common, 0)]++;
            double chi2 = 0;
            for (int r = 0; r < 6; r++)
            {
                double expected = n * weights[r] / total;
                chi2 += (counts[r] - expected) * (counts[r] - expected) / expected;
            }
            Assert.That(chi2, Is.LessThan(20.5), "chi-square, 5 degrees of freedom, p = 0.001");
        }

        [Test]
        public void MinRarity_Respected()
        {
            var loot = Loot();
            var rng = new Pcg32(3);
            for (int i = 0; i < 5000; i++)
                Assert.That(loot.Roll(10, rng, Rarity.Rare).Rarity, Is.GreaterThanOrEqualTo(Rarity.Rare));
        }

        [Test]
        public void AttributeCount_MatchesRarity_NoDuplicates_FromKindPool()
        {
            var cfg = TestConfig.Load();
            var loot = Loot();
            var rng = new Pcg32(21);
            for (int i = 0; i < 20000; i++)
            {
                var item = loot.Roll(50, rng);
                Assert.AreEqual(cfg.Gear.RandomAffixes[(int)item.Rarity], item.Attributes.Count);
                Assert.AreEqual(item.Attributes.Count, item.Attributes.Select(a => a.AttributeId).Distinct().Count());
                foreach (var a in item.Attributes)
                    Assert.IsTrue(cfg.Items.Attributes.Single(p => p.Id == a.AttributeId).Kinds.Contains(item.Kind));
            }
        }

        [Test]
        public void AttributeValues_WithinRange()
        {
            var cfg = TestConfig.Load();
            var loot = Loot();
            var rng = new Pcg32(8);
            for (int i = 0; i < 20000; i++)
            {
                var item = loot.Roll(60, rng, Rarity.Rare);
                foreach (var a in item.Attributes)
                {
                    var p = cfg.Items.Attributes.Single(x => x.Id == a.AttributeId);
                    double extra = p.PerItemLevel * 60;
                    Assert.That(a.Value, Is.InRange(p.Min + extra, p.Max + extra));
                }
            }
        }

        [Test]
        public void HigherRarity_RollsHigherOnAverage()
        {
            var loot = Loot();
            double rare = AverageQuality(loot, Rarity.Rare), mythical = AverageQuality(loot, Rarity.Mythical);
            Assert.That(mythical, Is.GreaterThan(rare + 0.05));
        }

        private static double AverageQuality(LootSystem loot, Rarity rarity)
        {
            var rng = new Pcg32(99);
            double sum = 0;
            for (int i = 0; i < 20000; i++) sum += loot.RollQuality(rng, rarity);
            return sum / 20000;
        }

        [Test]
        public void MainHand_DropsThreeTimesAsOften()
        {
            var loot = Loot();
            var rng = new Pcg32(4);
            var counts = new Dictionary<ItemKind, int>();
            const int n = 60000;
            for (int i = 0; i < n; i++)
            {
                var k = loot.RollKind(rng);
                counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;
            }
            // weights: main hand 3, off-hand 1, 6 armour x 1, ring 2 (two slots), amulet 1 -> total 13
            Assert.AreEqual(3.0 / 13, counts[ItemKind.MainHand] / (double)n, 0.01);
            Assert.AreEqual(2.0 / 13, counts[ItemKind.Ring] / (double)n, 0.01);
            Assert.AreEqual(1.0 / 13, counts[ItemKind.Gloves] / (double)n, 0.01);
        }

        [Test]
        public void ClassItems_AboutThirtyPercent()
        {
            var loot = Loot();
            var rng = new Pcg32(6);
            int classItems = 0;
            const int n = 30000;
            for (int i = 0; i < n; i++) if (loot.Roll(20, rng).ClassBonus.HasValue) classItems++;
            Assert.AreEqual(0.30, classItems / (double)n, 0.01);
        }

        [Test]
        public void Luck_ShiftsRarityUpward()
        {
            var loot = Loot();
            int rareNoLuck = 0, rareLuck = 0;
            var a = new Pcg32(1);
            var b = new Pcg32(1);
            for (int i = 0; i < 50000; i++)
            {
                if (loot.RollRarity(a, Rarity.Common, 0) >= Rarity.Rare) rareNoLuck++;
                if (loot.RollRarity(b, Rarity.Common, 200) >= Rarity.Rare) rareLuck++;
            }
            // 200 Luck: weights above Common x1.5 -> Rare+ share 15% -> 18.75% (x1.25)
            Assert.That(rareLuck, Is.GreaterThan(rareNoLuck * 1.18));
        }
    }
}
