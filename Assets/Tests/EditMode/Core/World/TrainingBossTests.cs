using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.World
{
    public class TrainingBossTests
    {
        private static WorldSystem Sys() => new WorldSystem(TestConfig.Load());

        private static void Win(WorldSystem s, WorldState w, int times)
        {
            for (int i = 0; i < times; i++)
                s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.Won, 0);
        }

        [Test]
        public void TrainingBoss_AfterFirstWave_BeforeSubzone2()
        {
            var s = Sys();
            var w = new WorldState { TrainingBossPending = true };
            Win(s, w, 10);
            Assert.AreEqual(1, w.UnlockedSubzone, "subzone 2 waits for the training boss");
            var e = s.NextEncounter(w, 0);
            Assert.IsTrue(e.IsTraining);
            Assert.IsTrue(e.IsBoss);
        }

        [Test]
        public void TrainingBoss_IgnoresBossToggle()
        {
            var s = Sys();
            var w = new WorldState { TrainingBossPending = true, FightBossWhenAvailable = false };
            Win(s, w, 10);
            Assert.IsTrue(s.NextEncounter(w, 0).IsTraining);
        }

        [Test]
        public void TrainingBoss_Won_UnlocksSubzone2_AndClearsPending()
        {
            var s = Sys();
            var w = new WorldState { TrainingBossPending = true };
            Win(s, w, 10);
            var change = s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.Won, 0);
            Assert.AreEqual(WorldChange.TrainingBossDefeated, change);
            Assert.IsFalse(w.TrainingBossPending);
            Assert.AreEqual(2, w.UnlockedSubzone);
            Assert.IsFalse(s.NextEncounter(w, 0).IsTraining);
        }

        [Test]
        public void TrainingBoss_Lost_RequiresNewWave()
        {
            var s = Sys();
            var w = new WorldState { TrainingBossPending = true };
            Win(s, w, 10);
            s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.TimedOut, 0);
            Assert.AreEqual(0, w.KillsInSubzone(1));
            Assert.IsFalse(s.NextEncounter(w, 0).IsTraining);
        }

        [Test]
        public void NotPending_NoTrainingBoss()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 10);
            Assert.AreEqual(2, w.UnlockedSubzone);
        }

        [Test]
        public void TrainingEnemy_CannotKill_AndIsWeak()
        {
            var cfg = TestConfig.Load();
            var f = new EnemyFactory(cfg);
            var e = f.Create(new Encounter(1, 1, 1, true, true), new Pcg32(1));
            Assert.IsTrue(e.IsBoss);
            Assert.IsFalse(e.CanKillPlayer);
            Assert.That(e.Hp, Is.InRange(30 * 3 * 0.9, 30 * 3 * 1.1));
            Assert.AreEqual(4.0, e.Attack, 1e-9);

            var engine = new CombatEngine(cfg);
            var weak = new CombatStats { HitDamage = 1, AttackInterval = 1, MaxHp = 1, BaseHitChance = 1, Accuracy = 100, CritDamage = 1.5 };
            var r = engine.Resolve(weak, e, new Pcg32(2), true);
            Assert.AreNotEqual(FightOutcome.Died, r.Outcome);
        }

        [Test]
        public void Region1Boss_UsesHpOverride_OtherRegionsDefault()
        {
            var cfg = TestConfig.Load();
            double mult = cfg.Onboarding.BossHpMultOverrides[1];
            Assert.That(mult, Is.LessThan(cfg.Enemy.BossHpMult));
            var f = new EnemyFactory(cfg);
            var r1 = f.Create(new Encounter(1, 5, 10, true), new Pcg32(3));
            double normal = 30 * System.Math.Pow(1.10, 9);
            Assert.That(r1.Hp, Is.InRange(normal * mult * 0.9, normal * mult * 1.1));
            var r2 = f.Create(new Encounter(2, 5, 20, true), new Pcg32(3));
            double normal2 = 30 * System.Math.Pow(1.10, 19);
            Assert.That(r2.Hp, Is.InRange(normal2 * 12 * 0.9, normal2 * 12 * 1.1));
        }

        [Test]
        public void Session_FirstRun_TrainingBossOnce_WithRareReward()
        {
            var bus = new EventBus();
            ItemDropped trainingDrop = null;
            int trainingWins = 0;
            bus.Subscribe<WorldChanged>(e => { if (e.Change == WorldChange.TrainingBossDefeated) trainingWins++; });
            bus.Subscribe<ItemDropped>(e => { if (e.FromBoss && trainingDrop == null) trainingDrop = e; });
            var state = GameState.NewRun(ClassId.Fighter);
            var s = new GameSession(TestConfig.Load(), state, new Pcg32(5), bus);
            s.Advance(600);
            Assert.AreEqual(1, trainingWins);
            Assert.IsTrue(state.Account.TrainingBossDone);
            Assert.That(trainingDrop.Item.Rarity, Is.GreaterThanOrEqualTo(Rarity.Rare));

            // a later run (e.g. after prestige) keeps the account flag: no training boss
            var next = GameState.NewRun(ClassId.Fighter);
            next.Account = state.Account;
            var s2 = new GameSession(TestConfig.Load(), next, new Pcg32(6), new EventBus());
            Assert.IsFalse(next.World.TrainingBossPending);
        }
    }
}
