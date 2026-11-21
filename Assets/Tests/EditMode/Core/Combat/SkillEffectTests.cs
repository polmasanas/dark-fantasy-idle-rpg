using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Skills;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Combat
{
    public class SkillEffectTests
    {
        private static System.Collections.Generic.List<CombatEvent> Of(FightResult r, CombatEventType t) =>
            r.Events.Where(e => e.Type == t).ToList();

        [Test]
        public void Rend_BleedTicksEverySecond_Total100Pct()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_rend")));
            Assert.AreEqual(12.0, SkillFight.Skills(r)[0].Amount, 1e-9);
            var ticks = Of(r, CombatEventType.DotTick).Where(e => e.Time < 10).ToList();
            Assert.AreEqual(4, ticks.Count);
            CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0, 5.0 }, ticks.Select(e => System.Math.Round(e.Time, 9)).ToArray());
            Assert.IsTrue(ticks.All(e => System.Math.Abs(e.Amount - 2.5) < 1e-9 && e.SkillId == "fighter_rend"));
        }

        [Test]
        public void Dot_Recast_Refreshes_NotStacks()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_rend", cooldown: 2.0)), SkillFight.Enemy(hp: 1e6));
            // casts at 1, 3, 5, ...: never more than one tick per second
            var ticks = Of(r, CombatEventType.DotTick);
            Assert.AreEqual(ticks.Count, ticks.Select(e => System.Math.Round(e.Time, 6)).Distinct().Count());
            Assert.AreEqual(2.0, ticks[0].Time, 1e-9);
            Assert.AreEqual(4.0, ticks[2].Time - 0.0, 1e-9);
        }

        [Test]
        public void Dot_TickCanKill()
        {
            // 12 at t=1 leaves 2 HP; at t=2 the tick (2.5) comes before the attack and kills.
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_rend")), SkillFight.Enemy(hp: 14));
            Assert.AreEqual(FightOutcome.Won, r.Outcome);
            Assert.AreEqual(2.0, r.Duration, 1e-9);
            var lastDamage = r.Events.Last(e => e.Type != CombatEventType.EnemyDied && e.Type != CombatEventType.EnemyHit);
            Assert.AreEqual(CombatEventType.DotTick, lastDamage.Type);
        }

        [Test]
        public void Dot_EndsWithTheFight()
        {
            var cds = new double[1];
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_rend")), SkillFight.Enemy(hp: 30), cds: cds);
            Assert.IsTrue(r.Events.Last().Type == CombatEventType.EnemyDied);
        }

        [Test]
        public void WarCry_Plus30Pct_For6s()
        {
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("fighter_war_cry")));
            var buff = Of(r, CombatEventType.BuffStart).Single(e => e.Time < 2);
            Assert.AreEqual(1.0, buff.Time, 1e-9);
            var hits = Of(r, CombatEventType.PlayerHit);
            Assert.AreEqual(13.0, hits.First(e => System.Math.Abs(e.Time - 2) < 1e-9).Amount, 1e-9);
            Assert.AreEqual(13.0, hits.First(e => System.Math.Abs(e.Time - 6) < 1e-9).Amount, 1e-9);
            Assert.AreEqual(10.0, hits.First(e => System.Math.Abs(e.Time - 7) < 1e-9).Amount, 1e-9);
        }

        [Test]
        public void FrostLance_SlowsEnemyAttacks()
        {
            // Slow 30% for 4 s from t=1: intervals chosen while slowed are 2.6 s.
            var r = SkillFight.Run(SkillFight.Kit(SkillFight.Active("mage_frost_lance", cooldown: 100)), SkillFight.Enemy(interval: 2.0));
            var times = Of(r, CombatEventType.EnemyHit).Select(e => e.Time).Take(4).ToArray();
            Assert.AreEqual(2.0, times[0], 1e-9);
            Assert.AreEqual(4.6, times[1], 1e-9);
            Assert.AreEqual(7.2, times[2], 1e-9);
            Assert.AreEqual(9.2, times[3], 1e-9);
        }

        [Test]
        public void Dodge_NoDamageTaken()
        {
            var kit = SkillFight.Kit();
            kit.Dodge = 1.0;
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 5, interval: 1));
            Assert.IsEmpty(Of(r, CombatEventType.EnemyHit));
            Assert.Greater(Of(r, CombatEventType.Dodge).Count, 20);
            Assert.AreEqual(100.0, r.PlayerHpLeft, 1e-9);
        }

        [Test]
        public void DamageTakenCuts_Apply_BossCutOnlyOnBosses()
        {
            var kit = SkillFight.Kit();
            kit.DamageTakenCut = 0.1;
            kit.BossDamageTakenCut = 0.5;
            var normal = Of(SkillFight.Run(kit, SkillFight.Enemy(attack: 10)), CombatEventType.EnemyHit)[0].Amount;
            var boss = Of(SkillFight.Run(kit, SkillFight.Enemy(attack: 10, boss: true)), CombatEventType.EnemyHit)[0].Amount;
            Assert.AreEqual(9.0, normal, 1e-9);
            Assert.AreEqual(4.5, boss, 1e-9);
        }

        [Test]
        public void SecondWind_OncePerFight()
        {
            var kit = SkillFight.Kit();
            kit.HealBelowAt = 0.3;
            kit.HealBelowPct = 0.15;
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 40, interval: 1));
            var heals = Of(r, CombatEventType.Heal);
            Assert.AreEqual(1, heals.Count);
            Assert.AreEqual(15.0, heals[0].Amount, 1e-9);
        }

        [Test]
        public void Unbreakable_BossFight_SurvivesOnce_ThenDies()
        {
            var kit = SkillFight.Kit();
            kit.CheatDeathHealPct = 0.05;
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 60, interval: 1, boss: true));
            Assert.AreEqual(FightOutcome.Died, r.Outcome);
            Assert.AreEqual(3.0, r.Duration, 1e-9);
            Assert.AreEqual(1, Of(r, CombatEventType.Heal).Count);
        }

        [Test]
        public void Unbreakable_NotOnNormalFights()
        {
            var kit = SkillFight.Kit();
            kit.CheatDeathHealPct = 0.05;
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 60, interval: 1));
            Assert.IsEmpty(Of(r, CombatEventType.Heal));
        }

        [Test]
        public void Lifesteal_NeverAboveMaxHp()
        {
            var kit = SkillFight.Kit();
            kit.Lifesteal = 0.5;
            var with = SkillFight.Run(kit, SkillFight.Enemy(hp: 1e6, attack: 2, interval: 0.5, boss: true));
            var noKit = SkillFight.Engine().Resolve(SkillFight.Player(), SkillFight.Enemy(hp: 1e6, attack: 2, interval: 0.5, boss: true),
                new DarkFantasy.Core.Random.Pcg32(1), false);
            Assert.LessOrEqual(with.PlayerHpLeft, 100.0);
            Assert.Greater(with.PlayerHpLeft, noKit.PlayerHpLeft);
        }

        [Test]
        public void Momentum_StacksToMax_ResetsNextFight()
        {
            var kit = SkillFight.Kit();
            kit.MomentumPerHit = 0.1;
            kit.MomentumMaxStacks = 2;
            var hits = Of(SkillFight.Run(kit), CombatEventType.PlayerHit).Select(e => e.Time).ToArray();
            Assert.AreEqual(1.0, hits[0], 1e-9);
            Assert.AreEqual(1 + 1 / 1.1, hits[1], 1e-9);
            Assert.AreEqual(1 + 1 / 1.1 + 1 / 1.2, hits[2], 1e-9);
            Assert.AreEqual(1 + 1 / 1.1 + 2 / 1.2, hits[3], 1e-9);
            var again = Of(SkillFight.Run(kit), CombatEventType.PlayerHit);
            Assert.AreEqual(1.0, again[0].Time, 1e-9);
        }

        [Test]
        public void Echo_Chance1_FiresTwice_OneCooldown()
        {
            var kit = SkillFight.Kit(SkillFight.Active("fighter_heavy_swing"));
            kit.EchoChance = 1.0;
            var s = SkillFight.Skills(SkillFight.Run(kit));
            Assert.AreEqual(2, s.Count(e => e.Time < 1.5));
            Assert.AreEqual(9.0, s[2].Time, 1e-9);
        }

        [Test]
        public void HuntersMark_VsBeastBrute_TakesLarger_NotBoth()
        {
            var kit = SkillFight.Kit();
            kit.ConditionalDamage.Add(new ConditionalBonus { NodeId = "m", Condition = "vs_family:beasts", Value = 0.16 });
            kit.ConditionalDamage.Add(new ConditionalBonus { NodeId = "m", Condition = "vs_archetype:brute", Value = 0.16 });
            var enemy = SkillFight.Enemy();
            enemy.Family = "beasts";
            enemy.Archetype = Archetype.Brute;
            var hit = Of(SkillFight.Run(kit, enemy), CombatEventType.PlayerHit)[0];
            Assert.AreEqual(11.6, hit.Amount, 1e-9);
            var other = SkillFight.Enemy();
            other.Family = "undead";
            Assert.AreEqual(10.0, Of(SkillFight.Run(kit, other), CombatEventType.PlayerHit)[0].Amount, 1e-9);
        }

        [Test]
        public void SteadyAim_OnlyAboveHalfHp()
        {
            var kit = SkillFight.Kit();
            kit.ConditionalDamage.Add(new ConditionalBonus { NodeId = "s", Condition = "own_hp_above", Value = 0.5, Threshold = 0.5 });
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 30, interval: 1.5, boss: true));
            var hits = Of(r, CombatEventType.PlayerHit);
            Assert.AreEqual(15.0, hits[0].Amount, 1e-9);   // 100 HP
            Assert.AreEqual(15.0, hits[2].Amount, 1e-9);   // t=3: acts before the enemy's 3.0 hit, 70 HP
            Assert.AreEqual(10.0, hits[3].Amount, 1e-9);   // t=4: 40 HP
        }

        [Test]
        public void Unbreakable_ReportsTrueHpAfterTheSave()
        {
            var kit = SkillFight.Kit();
            kit.CheatDeathHealPct = 0.05;
            var r = SkillFight.Run(kit, SkillFight.Enemy(attack: 60, interval: 1, boss: true));
            var hp = Of(r, CombatEventType.PlayerHp);
            Assert.AreEqual(1, hp.Count);
            Assert.AreEqual(2.0, hp[0].Time, 1e-9);
            Assert.AreEqual(6.0, hp[0].Amount, 1e-9, "1 HP left + 5% of 100");
        }

        [Test]
        public void Lifesteal_ReportsHp_NeverAboveMax()
        {
            var kit = SkillFight.Kit();
            kit.Lifesteal = 0.5;
            var r = SkillFight.Run(kit, SkillFight.Enemy(hp: 1e6, attack: 2, interval: 0.5, boss: true));
            var hp = Of(r, CombatEventType.PlayerHp);
            Assert.Greater(hp.Count, 10);
            Assert.IsTrue(hp.All(e => e.Amount <= 100.0 + 1e-9 && e.Amount > 0));
            Assert.AreEqual(r.PlayerHpLeft, ViewHp(r, 100.0), 1e-9, "the bar rebuilt from events ends at the true HP");
        }

        [Test]
        public void Unbreakable_BarRebuiltFromEvents_MatchesTrueHp()
        {
            var kit = SkillFight.Kit();
            kit.CheatDeathHealPct = 0.5;
            var r = SkillFight.Run(kit, SkillFight.Enemy(hp: 1e6, attack: 60, interval: 1, boss: true));
            Assert.AreEqual(r.PlayerHpLeft, ViewHp(r, 100.0), 1e-9);
        }

        /// <summary>What CombatView's health bar shows: enemy hits subtract, PlayerHp events set the value.</summary>
        private static double ViewHp(FightResult r, double max)
        {
            double hp = max;
            foreach (var e in r.Events)
            {
                if (e.Type == CombatEventType.EnemyHit) hp -= e.Amount;
                else if (e.Type == CombatEventType.PlayerHp) hp = e.Amount;
                else if (e.Type == CombatEventType.PlayerDied) hp = 0;
            }
            return hp;
        }
    }
}
