using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Combat
{
    public class TriangleTests
    {
        private static TriangleTable Table() => new TriangleTable(TestConfig.Load());

        // D-017: Fighter > Caster, Archer > Brute, Mage > Marksman.
        [TestCase(ClassId.Fighter, Archetype.Caster, 1.2, 0.85)]
        [TestCase(ClassId.Fighter, Archetype.Marksman, 0.85, 1.2)]
        [TestCase(ClassId.Fighter, Archetype.Brute, 1.0, 1.0)]
        [TestCase(ClassId.Archer, Archetype.Brute, 1.2, 0.85)]
        [TestCase(ClassId.Archer, Archetype.Caster, 0.85, 1.2)]
        [TestCase(ClassId.Archer, Archetype.Marksman, 1.0, 1.0)]
        [TestCase(ClassId.Mage, Archetype.Marksman, 1.2, 0.85)]
        [TestCase(ClassId.Mage, Archetype.Brute, 0.85, 1.2)]
        [TestCase(ClassId.Mage, Archetype.Caster, 1.0, 1.0)]
        [TestCase(ClassId.Fighter, Archetype.None, 1.0, 1.0)]
        [TestCase(ClassId.Archer, Archetype.None, 1.0, 1.0)]
        [TestCase(ClassId.Mage, Archetype.None, 1.0, 1.0)]
        public void Triangle_DealtAndTaken(ClassId hero, Archetype enemy, double dealt, double taken)
        {
            Assert.AreEqual(dealt, Table().DealtBy(hero, enemy), 1e-12);
            Assert.AreEqual(taken, Table().TakenBy(hero, enemy), 1e-12);
        }

        [Test]
        public void Engine_AppliesTriangle_BothWays()
        {
            var engine = new CombatEngine(TestConfig.Load());
            var hero = new CombatStats { Class = ClassId.Archer, HitDamage = 100, AttackInterval = 1, MaxHp = 100, Accuracy = 100, BaseHitChance = 1, CritDamage = 2 };
            var brute = new EnemySnapshot { Hp = 1e9, Attack = 10, AttackInterval = 2, Archetype = Archetype.Brute };
            Assert.AreEqual(120, engine.HitAmount(hero, brute, 1.0, false), 1e-9);
            Assert.AreEqual(8.5, engine.DamageTaken(hero, brute), 1e-9);
        }
    }
}
