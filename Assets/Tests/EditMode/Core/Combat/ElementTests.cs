using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Combat
{
    public class ElementTests
    {
        private static ElementTable Table() => new ElementTable(TestConfig.Load());

        // GDD section 4.4: rows = attack element, columns = target element (Physical = no element).
        [TestCase(Element.Fire, Element.Fire, 1.0)]
        [TestCase(Element.Fire, Element.Nature, 1.5)]
        [TestCase(Element.Fire, Element.Water, 0.75)]
        [TestCase(Element.Fire, Element.Light, 1.0)]
        [TestCase(Element.Fire, Element.Dark, 1.0)]
        [TestCase(Element.Fire, Element.Physical, 1.0)]
        [TestCase(Element.Nature, Element.Fire, 0.75)]
        [TestCase(Element.Nature, Element.Nature, 1.0)]
        [TestCase(Element.Nature, Element.Water, 1.5)]
        [TestCase(Element.Nature, Element.Light, 1.0)]
        [TestCase(Element.Nature, Element.Dark, 1.0)]
        [TestCase(Element.Nature, Element.Physical, 1.0)]
        [TestCase(Element.Water, Element.Fire, 1.5)]
        [TestCase(Element.Water, Element.Nature, 0.75)]
        [TestCase(Element.Water, Element.Water, 1.0)]
        [TestCase(Element.Water, Element.Light, 1.0)]
        [TestCase(Element.Water, Element.Dark, 1.0)]
        [TestCase(Element.Water, Element.Physical, 1.0)]
        [TestCase(Element.Light, Element.Fire, 1.0)]
        [TestCase(Element.Light, Element.Nature, 1.0)]
        [TestCase(Element.Light, Element.Water, 1.0)]
        [TestCase(Element.Light, Element.Light, 0.75)]
        [TestCase(Element.Light, Element.Dark, 1.5)]
        [TestCase(Element.Light, Element.Physical, 1.0)]
        [TestCase(Element.Dark, Element.Fire, 1.0)]
        [TestCase(Element.Dark, Element.Nature, 1.0)]
        [TestCase(Element.Dark, Element.Water, 1.0)]
        [TestCase(Element.Dark, Element.Light, 1.5)]
        [TestCase(Element.Dark, Element.Dark, 0.75)]
        [TestCase(Element.Dark, Element.Physical, 1.0)]
        [TestCase(Element.Physical, Element.Fire, 1.0)]
        [TestCase(Element.Physical, Element.Nature, 1.0)]
        [TestCase(Element.Physical, Element.Water, 1.0)]
        [TestCase(Element.Physical, Element.Light, 0.75)]
        [TestCase(Element.Physical, Element.Dark, 0.75)]
        [TestCase(Element.Physical, Element.Physical, 1.0)]
        public void Matrix_MatchesGddTable(Element attack, Element target, double expected)
        {
            Assert.AreEqual(expected, Table().Multiplier(attack, target), 1e-12);
        }

        private static CombatStats Hero(double hit = 100) => new CombatStats
        {
            HitDamage = hit, AttackInterval = 1, MaxHp = 100, Accuracy = 100, BaseHitChance = 1, CritDamage = 2,
        };

        [Test]
        public void MultiComponent_HitAgainstElemental_PerComponentMultipliers()
        {
            var engine = new CombatEngine(TestConfig.Load());
            var hero = Hero();
            hero.ElementShare[(int)Element.Water] = 0.5;
            var enemy = new EnemySnapshot { Hp = 1e9, Attack = 1, AttackInterval = 2, Element = Element.Water };
            enemy.Resist[(int)Element.Water] = 0.75;
            // Physical 100 (×1 vs Water) + Water 50 × 1.0 (Water vs Water) × (1 − 0.75) = 112.5
            Assert.AreEqual(112.5, engine.HitAmount(hero, enemy, 1.0, false), 1e-9);
        }

        [Test]
        public void PctElemental_BoostsOnlyElementalComponents()
        {
            var engine = new CombatEngine(TestConfig.Load());
            var hero = Hero();
            hero.ElementShare[(int)Element.Fire] = 0.2;
            hero.PctElemental = 0.5;
            var enemy = new EnemySnapshot { Hp = 1e9, Attack = 1, AttackInterval = 2, Element = Element.Nature };
            // Physical 100 + Fire 100 × 0.2 × 1.5 × 1.5 (strong) = 145
            Assert.AreEqual(145, engine.HitAmount(hero, enemy, 1.0, false), 1e-9);
        }

        [Test]
        public void Resist_ClampedToConfigRange_TotalNeverNegative()
        {
            var engine = new CombatEngine(TestConfig.Load());
            var hero = Hero();
            hero.ElementShare[(int)Element.Fire] = 1.0;
            var enemy = new EnemySnapshot { Hp = 1e9, Attack = 1, AttackInterval = 2 };
            enemy.Resist[(int)Element.Fire] = 5.0;   // clamped to 0.75
            enemy.Resist[(int)Element.Physical] = -9; // clamped to −0.5
            Assert.AreEqual(100 * 1.5 + 100 * 0.25, engine.HitAmount(hero, enemy, 1.0, false), 1e-9);
        }

        [Test]
        public void NeutralEnemy_DamageUnchanged()
        {
            var engine = new CombatEngine(TestConfig.Load());
            var enemy = new EnemySnapshot { Hp = 1e9, Attack = 1, AttackInterval = 2 };
            Assert.AreEqual(100, engine.HitAmount(Hero(), enemy, 1.0, false), 1e-9);
            Assert.AreEqual(120, engine.HitAmount(Hero(), enemy, 0.6, true), 1e-9); // counter 0.6 × crit 2
        }
    }
}
