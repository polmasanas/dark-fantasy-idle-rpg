using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Characters
{
    public class ProgressionTests
    {
        private static ProgressionSystem Prog() => new ProgressionSystem(TestConfig.Load());
        private static GameFormulas F() => new GameFormulas(TestConfig.Load());
        private static CharacterState Fighter() => CharacterState.Create(ClassId.Fighter);

        private static double XpFromLevel1To(int level)
        {
            double sum = 0;
            for (int l = 1; l < level; l++) sum += F().XpToNext(l);
            return sum;
        }

        [Test]
        public void AddXp_LevelsUpAtThreshold()
        {
            var c = Fighter();
            Assert.AreEqual(0, Prog().AddXp(c, F().XpToNext(1) - 0.001));
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(1, Prog().AddXp(c, 0.001));
            Assert.AreEqual(2, c.Level);
            Assert.AreEqual(0.0, c.Xp, 1e-9);
        }

        [Test]
        public void AddXp_ManyLevelsAtOnce_PaysAllPoints()
        {
            var c = Fighter();
            Assert.AreEqual(49, Prog().AddXp(c, XpFromLevel1To(50) + 1e-6));
            Assert.AreEqual(50, c.Level);
            int auto = 0;
            foreach (var v in c.AutoPoints.Values) auto += v;
            Assert.AreEqual(147, auto);
            Assert.AreEqual(49, c.UnspentPoints);
        }

        [Test]
        public void AutoPoints_FollowFighterPattern()
        {
            var c = Fighter();
            Prog().AddXp(c, XpFromLevel1To(5) + 1e-6);
            Assert.AreEqual(5, c.Level);
            Assert.AreEqual(6, c.AutoPoints[AttributeId.Str]);
            Assert.AreEqual(4, c.AutoPoints[AttributeId.Vit]);
            Assert.AreEqual(1, c.AutoPoints[AttributeId.Def]);
            Assert.AreEqual(1, c.AutoPoints[AttributeId.Counter]);
        }

        [Test]
        public void SpendPoint_RequiresUnspent()
        {
            var c = Fighter();
            Assert.IsFalse(Prog().SpendPoint(c, AttributeId.Str));
            c.UnspentPoints = 1;
            Assert.IsTrue(Prog().SpendPoint(c, AttributeId.Str));
            Assert.AreEqual(1, c.ManualPoints[AttributeId.Str]);
            Assert.AreEqual(0, c.UnspentPoints);
        }

        [Test]
        public void AutoAllocate_Balanced_SplitsByShares()
        {
            var c = Fighter();
            c.UnspentPoints = 8;
            c.Preset = "balanced";
            Assert.AreEqual(8, Prog().AutoAllocate(c));
            Assert.AreEqual(4, c.ManualPoints[AttributeId.Str]);
            Assert.AreEqual(2, c.ManualPoints[AttributeId.Vit]);
            Assert.AreEqual(2, c.ManualPoints[AttributeId.CritChance]);
            Assert.AreEqual(0, c.UnspentPoints);
        }

        [Test]
        public void AutoAllocate_NoPoints_NoChange()
        {
            var c = Fighter();
            Assert.AreEqual(0, Prog().AutoAllocate(c));
            c.UnspentPoints = 3;
            c.Preset = "does-not-exist";
            Assert.AreEqual(0, Prog().AutoAllocate(c));
            Assert.AreEqual(3, c.UnspentPoints);
        }

        [Test]
        public void Total_SumsAutoAndManual()
        {
            var c = Fighter();
            c.AutoPoints[AttributeId.Str] = 6;
            c.ManualPoints[AttributeId.Str] = 2;
            Assert.AreEqual(8, Prog().Total(c, AttributeId.Str));
        }

        [Test]
        public void Build_MapsAttributesToStatInputs()
        {
            var c = Fighter();
            Prog().AddXp(c, XpFromLevel1To(5) + 1e-6);
            c.ManualPoints[AttributeId.Str] = 2;
            var i = new CharacterStatsBuilder(TestConfig.Load()).Build(c);
            Assert.AreEqual(ClassId.Fighter, i.Class);
            Assert.AreEqual(5, i.Level);
            Assert.AreEqual(8, i.Primary);
            Assert.AreEqual(4, i.Vit);
            Assert.AreEqual(1, i.Def);
            Assert.AreEqual(1, i.CounterPoints);
            Assert.AreEqual(10.0, i.WeaponDamage, 1e-9); // starter weapon: Common, item level 1
        }

        [Test]
        public void Build_WeaponScalesWithItemLevel()
        {
            var c = Fighter();
            c.WeaponItemLevel = 11;
            var i = new CharacterStatsBuilder(TestConfig.Load()).Build(c);
            Assert.AreEqual(10.0 * System.Math.Pow(1.07, 10), i.WeaponDamage, 1e-9);
        }
    }
}
