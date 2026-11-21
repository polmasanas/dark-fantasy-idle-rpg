using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Combat
{
    /// <summary>Shared builders for skill fights: hit chance 1, no crit, 10 damage per hit every 1 s.</summary>
    public static class SkillFight
    {
        public static CombatEngine Engine() => new CombatEngine(TestConfig.Load());

        public static CombatStats Player(double hp = 100, double crit = 0, double counter = 0) => new CombatStats
        {
            HitDamage = 10, AttackInterval = 1.0, MaxHp = hp, Armour = 0, Accuracy = 100, BaseHitChance = 1.0,
            CritChance = crit, CritDamage = 2.0, CounterChance = counter, CounterDamage = 0.6,
        };

        public static EnemySnapshot Enemy(double hp = 1000, double attack = 1, double interval = 2.0, bool boss = false) =>
            new EnemySnapshot { Hp = hp, Attack = attack, AttackInterval = interval, Level = 1, IsBoss = boss };

        public static ActiveSkill Active(string id, int rank = 1, int slot = 0, double? cooldown = null)
        {
            var node = TestConfig.Load().Skills.Find(id);
            return new ActiveSkill { Node = node, Rank = rank, Slot = slot, Cooldown = cooldown ?? node.CooldownS };
        }

        public static ActiveSkill Custom(string id, double percent, double cooldown, int slot)
        {
            var node = new SkillNode { Id = id, Kind = SkillKind.Active, MaxRank = 3, CooldownS = cooldown, Tier = 1 };
            node.Effects.Add(new SkillEffect { Kind = EffectKind.Hit, Percent = percent });
            return new ActiveSkill { Node = node, Rank = 1, Slot = slot, Cooldown = cooldown };
        }

        public static SkillKit Kit(params ActiveSkill[] actives)
        {
            var k = new SkillKit();
            k.Actives.AddRange(actives);
            return k;
        }

        public static FightResult Run(SkillKit kit, EnemySnapshot enemy = null, CombatStats player = null, double[] cds = null, ulong seed = 1) =>
            Engine().Resolve(player ?? Player(), enemy ?? Enemy(), new Pcg32(seed), true, kit, cds ?? new double[kit.Actives.Count]);

        public static List<CombatEvent> Skills(FightResult r) =>
            r.Events.Where(e => e.Type == CombatEventType.SkillHit || e.Type == CombatEventType.SkillCrit).ToList();
    }

    public class SkillCombatTests
    {
        [Test]
        public void NoKit_IdenticalToM5()
        {
            var p = SkillFight.Player(crit: 0.3, counter: 0.3);
            p.BaseHitChance = 0.8;
            foreach (var e in new[] { SkillFight.Enemy(hp: 300, attack: 3), SkillFight.Enemy(hp: 900, attack: 8, boss: true) })
            {
                var a = SkillFight.Engine().Resolve(p, e, new Pcg32(77), true);
                var b = SkillFight.Engine().Resolve(p, e, new Pcg32(77), true, SkillKit.None, new double[0]);
                Assert.AreEqual(a.Outcome, b.Outcome);
                Assert.AreEqual(a.Duration, b.Duration);
                Assert.AreEqual(a.PlayerHpLeft, b.PlayerHpLeft);
                CollectionAssert.AreEqual(a.Events.ToArray(), b.Events.ToArray());
            }
        }

        [Test]
        public void HeavySwing_ReplacesFirstAttack_250Pct()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_heavy_swing")));
            var first = r.Events.First(e => e.Type != CombatEventType.EnemyHit);
            Assert.AreEqual(CombatEventType.SkillHit, first.Type);
            Assert.AreEqual(1.0, first.Time, 1e-9);
            Assert.AreEqual(25.0, first.Amount, 1e-9);
            Assert.AreEqual("fighter_heavy_swing", first.SkillId);
            var second = r.Events.First(e => e.Time > 1.0 + 1e-9 && e.Type != CombatEventType.EnemyHit);
            Assert.AreEqual(CombatEventType.PlayerHit, second.Type);
            Assert.AreEqual(2.0, second.Time, 1e-9);
            Assert.IsNull(second.SkillId);
            Assert.AreEqual(9.0, SkillFight.Skills(r)[1].Time, 1e-9, "ready again 8 s after the cast");
        }

        [Test]
        public void Priority_Slot1BeforeSlot2_WhenBothReady()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Custom("a", 2.0, 30, 0), SkillFight.Custom("b", 3.0, 30, 1)));
            var s = SkillFight.Skills(r);
            Assert.AreEqual("a", s[0].SkillId);
            Assert.AreEqual(20.0, s[0].Amount, 1e-9);
            Assert.AreEqual("b", s[1].SkillId);
            Assert.AreEqual(2.0, s[1].Time, 1e-9);
        }

        [Test]
        public void Cooldown_CarriedIn_SkillWaits()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_heavy_swing")), cds: new[] { 3.0 });
            Assert.AreEqual(3.0, SkillFight.Skills(r)[0].Time, 1e-9);
        }

        [Test]
        public void CooldownLeft_WrittenBack()
        {
            // 25 at t=1, then 10 at t=2,3,4 -> 55 >= 50: dies at t=4. Cast at 1 with 8 s cooldown -> 5 s left.
            var cds = new double[1];
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_heavy_swing")), SkillFight.Enemy(hp: 50), cds: cds);
            Assert.AreEqual(FightOutcome.Won, r.Outcome);
            Assert.AreEqual(4.0, r.Duration, 1e-9);
            Assert.AreEqual(5.0, cds[0], 1e-9);
        }

        [Test]
        public void CooldownLeft_NeverNegative()
        {
            var cds = new[] { 0.5 };
            SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_executioner")), SkillFight.Enemy(hp: 10), cds: cds);
            Assert.AreEqual(0.0, cds[0], 1e-12);
        }

        [Test]
        public void ConditionNeverMet_HeroKeepsAttacking()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_executioner")), SkillFight.Enemy(hp: 1000, attack: 0));
            var hits = r.Events.Where(e => e.Type == CombatEventType.PlayerHit).ToList();
            Assert.AreEqual(30, hits.Count, "one normal attack per second until the 30 s escape timer");
            Assert.IsEmpty(SkillFight.Skills(r));
            Assert.AreEqual(1.0, hits[0].Time, 1e-9);
            Assert.AreEqual(2.0, hits[1].Time, 1e-9);
        }

        [Test]
        public void Executioner_FiresBelow30Pct()
        {
            // 100 HP: after 8 hits 20 HP left (< 30%), so the 9th attack is Executioner (50) and kills.
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_executioner")), SkillFight.Enemy(hp: 100));
            var s = SkillFight.Skills(r);
            Assert.AreEqual(1, s.Count);
            Assert.AreEqual(9.0, s[0].Time, 1e-9);
            Assert.AreEqual(50.0, s[0].Amount, 1e-9);
            Assert.AreEqual(FightOutcome.Won, r.Outcome);
        }

        [Test]
        public void AimedShot_AlwaysCrits()
        {
            var s = SkillFight.Skills(SkillFight.Run(SkillFight.Kit(SkillFight.Active("archer_aimed_shot"))));
            Assert.AreEqual(CombatEventType.SkillCrit, s[0].Type);
            Assert.AreEqual(2.2 * 10 * 2.0, s[0].Amount, 1e-9);
        }

        [Test]
        public void SkillCritDamage_AddsToSkillCritsOnly()
        {
            var kit = SkillFight.Kit(SkillFight.Active("archer_aimed_shot"));
            kit.SkillCritDamage = 0.5;
            var s = SkillFight.Skills(SkillFight.Run(kit));
            Assert.AreEqual(2.2 * 10 * 2.5, s[0].Amount, 1e-9);
        }

        [Test]
        public void PiercingArrow_HitsEvasiveEnemy()
        {
            var enemy = SkillFight.Enemy();
            enemy.Evasion = 1e9;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var s = SkillFight.Skills(SkillFight.Run(SkillFight.Kit(SkillFight.Active("archer_piercing_arrow")), enemy, seed: seed));
                Assert.AreEqual(40.0, s[0].Amount, 1e-9);
                Assert.AreEqual(1.0, s[0].Time, 1e-9);
            }
        }

        [Test]
        public void Volley_FiveHits()
        {
            var s = SkillFight.Skills(SkillFight.Run(SkillFight.Kit(SkillFight.Active("archer_volley"))));
            var first = s.Where(e => e.Time < 1.0 + 1e-9).ToList();
            Assert.AreEqual(5, first.Count);
            Assert.IsTrue(first.All(e => System.Math.Abs(e.Amount - 6.0) < 1e-9));
        }

        [Test]
        public void Firebolt_FireReplacesPhysical()
        {
            var enemy = SkillFight.Enemy();
            enemy.Element = Element.Nature;
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("mage_firebolt")), enemy);
            var s = SkillFight.Skills(r);
            Assert.AreEqual(2.6 * 10 * 1.5, s[0].Amount, 1e-9);
            Assert.AreEqual(Element.Fire, s[0].Element);
            var normal = r.Events.First(e => e.Type == CombatEventType.PlayerHit);
            Assert.AreEqual(10.0, normal.Amount, 1e-9);
        }

        [Test]
        public void NormalEnemy_StillNeverKills_WithSkills()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_heavy_swing")),
                SkillFight.Enemy(hp: 1e9, attack: 1e9, interval: 0.5), SkillFight.Player(hp: 10));
            Assert.AreNotEqual(FightOutcome.Died, r.Outcome);
            Assert.AreEqual(FightOutcome.Escaped, r.Outcome);
        }

        [Test]
        public void Boss_TimesOut_WithSkills()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_heavy_swing")), SkillFight.Enemy(hp: 1e9, attack: 0, boss: true));
            Assert.AreEqual(FightOutcome.TimedOut, r.Outcome);
            Assert.AreEqual(TestConfig.Load().Enemy.BossTimeLimitS, r.Duration, 1e-9);
        }
    }
}
