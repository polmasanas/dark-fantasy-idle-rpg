using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Stats
{
    public class StatCalculatorTests
    {
        private static StatCalculator Calc() => new StatCalculator(TestConfig.Load());

        private static StatInputs Inputs(ClassId cls = ClassId.Fighter) =>
            new StatInputs { Class = cls, Level = 1, WeaponDamage = 10 };

        [TestCase(ClassId.Fighter, 3.0)]
        [TestCase(ClassId.Archer, 2.2)]
        [TestCase(ClassId.Mage, 3.4)]
        public void AttackInterval_ClassBases(ClassId cls, double expected)
        {
            Assert.AreEqual(expected, Calc().Compute(Inputs(cls)).AttackInterval, 1e-12);
        }

        [Test]
        public void AttackInterval_RespectsFloor_AndHandlesZeroSpeed()
        {
            var i = Inputs();
            i.AttackSpeedPct = 100.0; // +10 000 %
            Assert.AreEqual(0.35, Calc().Compute(i).AttackInterval, 1e-12);
            i.AttackSpeedPct = 0.0;
            Assert.AreEqual(3.0, Calc().Compute(i).AttackInterval, 1e-12);
            i.AttackSpeedPct = -5.0; // a debuff larger than -100 % must not divide by zero or go negative
            var interval = Calc().Compute(i).AttackInterval;
            Assert.That(interval, Is.GreaterThan(3.0).And.LessThan(double.PositiveInfinity));
        }

        [Test]
        public void Haste_Adds5PercentPerLevel()
        {
            var i = Inputs();
            i.HasteLevel = 20; // +100 % attack speed
            Assert.AreEqual(1.5, Calc().Compute(i).AttackInterval, 1e-12);
        }

        [Test]
        public void HitDamage_ScaledByClassInterval()
        {
            // Fighter: 10 weapon damage x (3.0 / 1.6), no other bonuses
            Assert.AreEqual(10 * 3.0 / 1.6, Calc().Compute(Inputs()).HitDamage, 1e-9);
            // Archer: x (2.2 / 1.6)
            Assert.AreEqual(10 * 2.2 / 1.6, Calc().Compute(Inputs(ClassId.Archer)).HitDamage, 1e-9);
        }

        [Test]
        public void HitDamage_UsesPrimaryPctDamageAndMight()
        {
            var i = Inputs();
            i.Primary = 100;      // +150 %
            i.PctDamage = 0.5;    // +50 %
            i.MightLevel = 2;     // x1.21
            double expected = 10 * (3.0 / 1.6) * 2.5 * 1.5 * 1.21;
            Assert.AreEqual(expected, Calc().Compute(i).HitDamage, 1e-9);
        }

        [Test]
        public void CritChance_ClampsAtOne()
        {
            var i = Inputs();
            i.CritPoints = 1e9;
            Assert.AreEqual(1.0, Calc().Compute(i).CritChance, 1e-12);
            Assert.AreEqual(0.05, Calc().Compute(Inputs()).CritChance, 1e-12);
        }

        [Test]
        public void Counter_ClampsAt40Percent()
        {
            var i = Inputs();
            i.CounterPoints = 25; // 25 x 0.4 % = 10 %
            Assert.AreEqual(0.10, Calc().Compute(i).CounterChance, 1e-12);
            i.CounterPoints = 10000;
            Assert.AreEqual(0.40, Calc().Compute(i).CounterChance, 1e-12);
        }

        [Test]
        public void Hp_UsesVitAndBulwark()
        {
            var i = Inputs();
            i.GearHp = 100;       // (100 base + 100) = 200
            i.Vit = 50;           // x2.0
            i.BulwarkLevel = 1;   // x1.1
            Assert.AreEqual(200 * 2.0 * 1.1, Calc().Compute(i).MaxHp, 1e-9);
        }

        [Test]
        public void CritDamage_FromCritPointsAndCritDamagePoints()
        {
            var i = Inputs();
            i.CritPoints = 100;       // +0.5 (half rate)
            i.CritDamagePoints = 30;  // +0.3
            Assert.AreEqual(1.5 + 0.5 + 0.3, Calc().Compute(i).CritDamage, 1e-12);
        }

        [Test]
        public void NoWeapon_UsesUnarmedFloor()
        {
            var i = Inputs();
            i.WeaponDamage = 0;
            Assert.AreEqual(3.0 * 3.0 / 1.6, Calc().Compute(i).HitDamage, 1e-9);
        }
    }
}
