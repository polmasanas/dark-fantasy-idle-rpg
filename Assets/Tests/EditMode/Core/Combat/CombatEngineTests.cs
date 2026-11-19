using System;
using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Combat
{
    public class CombatEngineTests
    {
        private static CombatEngine Engine() => new CombatEngine(TestConfig.Load());

        private static CombatStats Player(double hitDamage = 10, double interval = 1.0, double hp = 100,
            double crit = 0.0, double counter = 0.0) => new CombatStats
        {
            HitDamage = hitDamage, AttackInterval = interval, MaxHp = hp, Armour = 0,
            Accuracy = 100, BaseHitChance = 1.0, CritChance = crit, CritDamage = 2.0,
            CounterChance = counter, CounterDamage = 0.6,
        };

        private static EnemySnapshot Enemy(double hp = 50, double attack = 1, double interval = 2.0,
            bool boss = false, int level = 1) => new EnemySnapshot
        {
            Hp = hp, Attack = attack, AttackInterval = interval, Level = level, Evasion = 0, IsBoss = boss,
        };

        [Test]
        public void SameSeed_IdenticalLog()
        {
            var p = Player(crit: 0.3, counter: 0.3);
            p.BaseHitChance = 0.8;
            var e = Enemy(hp: 500, boss: true, attack: 5);
            var a = Engine().Resolve(p, e, new Pcg32(77), true);
            var b = Engine().Resolve(p, e, new Pcg32(77), true);
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.Duration, b.Duration);
            CollectionAssert.AreEqual(a.Events.ToArray(), b.Events.ToArray());
            Assert.That(a.Events.Count, Is.GreaterThan(10));
        }

        [Test]
        public void Normal_Won_DurationIsKillTime()
        {
            // 50 HP, 10 per hit every 1 s -> dies on the 5th hit at t = 5
            var r = Engine().Resolve(Player(), Enemy(), new Pcg32(1), false);
            Assert.AreEqual(FightOutcome.Won, r.Outcome);
            Assert.AreEqual(5.0, r.Duration, 1e-9);
        }

        [Test]
        public void Normal_NeverDies()
        {
            var r = Engine().Resolve(Player(hitDamage: 1), Enemy(hp: 1000, attack: 1e9, interval: 0.5), new Pcg32(3), true);
            Assert.AreNotEqual(FightOutcome.Died, r.Outcome);
            Assert.IsFalse(r.Events.Any(ev => ev.Type == CombatEventType.PlayerDied));
        }

        [Test]
        public void Normal_EscapesAfterTimer()
        {
            var r = Engine().Resolve(Player(hitDamage: 1), Enemy(hp: 1e6), new Pcg32(4), true);
            Assert.AreEqual(FightOutcome.Escaped, r.Outcome);
            Assert.AreEqual(30.0, r.Duration, 1e-9);
            Assert.AreEqual(CombatEventType.Escaped, r.Events.Last().Type);
        }

        [Test]
        public void Boss_TimesOutAt60()
        {
            var r = Engine().Resolve(Player(hitDamage: 1, hp: 1e9), Enemy(hp: 1e6, boss: true), new Pcg32(5), false);
            Assert.AreEqual(FightOutcome.TimedOut, r.Outcome);
            Assert.AreEqual(60.0, r.Duration, 1e-9);
        }

        [Test]
        public void Boss_DiesWhenOutdamaged()
        {
            // 100 HP, boss hits 40 every 2 s -> dead at the 3rd hit, t = 6
            var r = Engine().Resolve(Player(hitDamage: 1), Enemy(hp: 1e6, attack: 40, boss: true), new Pcg32(6), false);
            Assert.AreEqual(FightOutcome.Died, r.Outcome);
            Assert.AreEqual(6.0, r.Duration, 1e-9);
        }

        [Test]
        public void Boss_DeathAndTimeoutSameInstant_IsLoss()
        {
            // Boss hits 60 every 30 s: the second hit (t = 60) kills exactly at the 60 s limit.
            var r = Engine().Resolve(Player(hitDamage: 1), Enemy(hp: 1e6, attack: 60, interval: 30, boss: true),
                new Pcg32(7), false);
            Assert.AreEqual(FightOutcome.Died, r.Outcome);
            Assert.AreEqual(60.0, r.Duration, 1e-9);
        }

        [Test]
        public void Boss_PlayerKillAtSameInstantAsEnemyHit_PlayerActsFirst()
        {
            // Both act at t = 4: the player's killing blow lands before the boss's lethal hit.
            var r = Engine().Resolve(Player(hitDamage: 50, interval: 2.0, hp: 10),
                Enemy(hp: 100, attack: 1000, interval: 4.0, boss: true), new Pcg32(8), false);
            Assert.AreEqual(FightOutcome.Won, r.Outcome);
            Assert.AreEqual(4.0, r.Duration, 1e-9);
        }

        [Test]
        public void Counter_TriggersOnlyOnEnemyHits()
        {
            var r = Engine().Resolve(Player(hitDamage: 0.001, counter: 1.0), Enemy(hp: 1e6), new Pcg32(9), true);
            int enemyHits = r.Events.Count(ev => ev.Type == CombatEventType.EnemyHit);
            int counters = r.Events.Count(ev => ev.Type == CombatEventType.Counter);
            Assert.That(enemyHits, Is.GreaterThan(0));
            Assert.AreEqual(enemyHits, counters);
        }

        [Test]
        public void Counter_DealsSixtyPercentOfHit()
        {
            var r = Engine().Resolve(Player(hitDamage: 10, interval: 100, counter: 1.0), Enemy(hp: 1e6), new Pcg32(10), true);
            var counter = r.Events.First(ev => ev.Type == CombatEventType.Counter);
            Assert.AreEqual(6.0, counter.Amount, 1e-9);
        }

        [Test]
        public void CritRate_MatchesChance()
        {
            var rng = new Pcg32(11);
            int crits = 0, hits = 0;
            for (int i = 0; i < 2000; i++)
            {
                var r = Engine().Resolve(Player(crit: 0.3), Enemy(hp: 100), rng, true);
                crits += r.Events.Count(ev => ev.Type == CombatEventType.PlayerCrit);
                hits += r.Events.Count(ev => ev.Type == CombatEventType.PlayerHit);
            }
            Assert.AreEqual(0.3, crits / (double)(crits + hits), 0.01);
        }

        [Test]
        public void HitChance_ReducedByEvasion()
        {
            var p = Player();
            p.BaseHitChance = 0.92;
            var e = Enemy(hp: 1e9);
            e.Evasion = 100; // 0.92 x 100 / (100 + 25) = 0.736
            var r = Engine().Resolve(p, e, new Pcg32(12), true);
            int attempts = r.Events.Count(ev => ev.Type == CombatEventType.PlayerHit || ev.Type == CombatEventType.PlayerMiss);
            int misses = r.Events.Count(ev => ev.Type == CombatEventType.PlayerMiss);
            Assert.AreEqual(30, attempts);
            Assert.That(misses, Is.GreaterThan(0));
        }

        [Test]
        public void MeanTtk_MatchesPythonModelWithin20Percent()
        {
            // Python EV model: TTK = ceil(HP / avgHit) / hitChance x interval (GAME_BALANCE section 5)
            foreach (double ratio in new[] { 0.15, 0.35, 0.6, 1.2 })
            {
                double enemyHp = 1000, cc = 0.15, cd = 1.6, hit = 0.92, interval = 1.6;
                double baseHit = enemyHp * ratio / (1 + cc * (cd - 1));
                var p = Player(hitDamage: baseHit, interval: interval, crit: cc);
                p.CritDamage = cd;
                p.BaseHitChance = hit;
                var rng = new Pcg32(13);
                double total = 0;
                const int n = 2000;
                for (int i = 0; i < n; i++)
                    total += Engine().Resolve(p, Enemy(hp: enemyHp), rng, false).Duration;
                double model = Math.Ceiling(enemyHp / (baseHit * (1 + cc * (cd - 1)))) / hit * interval;
                Assert.AreEqual(model, total / n, model * 0.20, "ratio " + ratio);
            }
        }
    }
}
