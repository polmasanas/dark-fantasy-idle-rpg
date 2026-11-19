using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.World
{
    public class WorldSystemTests
    {
        private static WorldSystem Sys() => new WorldSystem(TestConfig.Load());

        private static void Win(WorldSystem s, WorldState w, double now, int times = 1)
        {
            for (int i = 0; i < times; i++)
                s.OnFightFinished(w, s.NextEncounter(w, now), FightOutcome.Won, now);
        }

        [Test]
        public void FirstEncounter_Region1Subzone1Level1()
        {
            var e = Sys().NextEncounter(new WorldState(), 0);
            Assert.AreEqual(1, e.Region);
            Assert.AreEqual(1, e.Subzone);
            Assert.AreEqual(1, e.Level);
            Assert.IsFalse(e.IsBoss);
        }

        [Test]
        public void Kills_UnlockNextSubzone()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 9);
            Assert.AreEqual(1, w.UnlockedSubzone);
            var e = s.NextEncounter(w, 0);
            Assert.AreEqual(WorldChange.SubzoneUnlocked, s.OnFightFinished(w, e, FightOutcome.Won, 0));
            Assert.AreEqual(2, w.UnlockedSubzone);
            Assert.AreEqual(3, s.NextEncounter(w, 0).Level); // subzone 2 of region 1 = level 3
        }

        [Test]
        public void BossAvailable_AfterKInLastSubzone()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 10 * 5 - 1);
            Assert.AreEqual(5, w.UnlockedSubzone);
            Assert.IsFalse(s.NextEncounter(w, 0).IsBoss);
            Win(s, w, 0);
            var e = s.NextEncounter(w, 0);
            Assert.IsTrue(e.IsBoss);
            Assert.AreEqual(10, e.Level);
        }

        [Test]
        public void BossToggleOff_NeverFightsBoss()
        {
            var s = Sys();
            var w = new WorldState { FightBossWhenAvailable = false };
            Win(s, w, 0, 80);
            var e = s.NextEncounter(w, 0);
            Assert.IsFalse(e.IsBoss);
            Assert.AreEqual(5, e.Subzone);
        }

        [Test]
        public void BossLoss_RetriesAfter60s()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 50);
            var boss = s.NextEncounter(w, 100);
            Assert.AreEqual(WorldChange.BossFailed, s.OnFightFinished(w, boss, FightOutcome.TimedOut, 100));
            Assert.IsFalse(s.NextEncounter(w, 159).IsBoss);
            Assert.IsTrue(s.NextEncounter(w, 160).IsBoss);
        }

        [Test]
        public void BossWin_AdvancesRegion()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 50);
            var boss = s.NextEncounter(w, 0);
            Assert.AreEqual(WorldChange.BossDefeated, s.OnFightFinished(w, boss, FightOutcome.Won, 0));
            Assert.AreEqual(2, w.Region);
            Assert.AreEqual(1, w.HighestBossDefeated);
            var e = s.NextEncounter(w, 0);
            Assert.AreEqual(1, e.Subzone);
            Assert.AreEqual(11, e.Level);
            Assert.AreEqual(0, w.KillsInSubzone(1));
            Assert.AreEqual(15, s.KillsToAdvance(w)); // region 2: 10 + 5
        }

        [Test]
        public void FiveEscapes_Retreat_ThenPushAfter60s()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 20); // subzone 3 unlocked
            Assert.AreEqual(3, s.NextEncounter(w, 100).Subzone);
            WorldChange last = WorldChange.None;
            for (int i = 0; i < 5; i++)
                last = s.OnFightFinished(w, s.NextEncounter(w, 100), FightOutcome.Escaped, 100);
            Assert.AreEqual(WorldChange.Retreated, last);
            Assert.AreEqual(2, s.NextEncounter(w, 100).Subzone);
            Assert.AreEqual(2, s.NextEncounter(w, 159).Subzone);
            Assert.AreEqual(3, s.NextEncounter(w, 160).Subzone);
        }

        [Test]
        public void Retreat_FromFirstSubzone_StaysPut()
        {
            var s = Sys();
            var w = new WorldState();
            for (int i = 0; i < 5; i++)
                s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.Escaped, 0);
            Assert.AreEqual(1, s.NextEncounter(w, 10).Subzone);
        }

        [Test]
        public void Win_ResetsEscapeCounter()
        {
            var s = Sys();
            var w = new WorldState();
            for (int i = 0; i < 4; i++)
                s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.Escaped, 0);
            Win(s, w, 0);
            Assert.AreEqual(WorldChange.Escaped, s.OnFightFinished(w, s.NextEncounter(w, 0), FightOutcome.Escaped, 0));
            Assert.AreEqual(1, w.ConsecutiveEscapes);
        }

        [Test]
        public void NoBossWhileRetreating()
        {
            var s = Sys();
            var w = new WorldState();
            Win(s, w, 0, 50);
            for (int i = 0; i < 5; i++)
                s.OnFightFinished(w, new Encounter(1, 5, 9, false), FightOutcome.Escaped, 0);
            Assert.IsFalse(s.NextEncounter(w, 30).IsBoss);
            Assert.IsTrue(s.NextEncounter(w, 60).IsBoss);
        }

        [Test]
        public void EnemyFactory_UsesFormulasAndVariance()
        {
            var f = new EnemyFactory(TestConfig.Load());
            var rng = new Pcg32(1);
            for (int i = 0; i < 200; i++)
            {
                var e = f.Create(new Encounter(1, 1, 1, false), rng);
                Assert.That(e.Hp, Is.GreaterThanOrEqualTo(27.0).And.LessThan(33.0)); // 30 x [0.9, 1.1)
                Assert.AreEqual(4.0, e.Attack, 1e-9);
                Assert.AreEqual(2.0, e.AttackInterval, 1e-9);
                Assert.IsFalse(e.IsBoss);
            }
            var boss = f.Create(new Encounter(1, 5, 10, true), new Pcg32(2));
            Assert.IsTrue(boss.IsBoss);
            Assert.AreEqual(10, boss.Level);
            Assert.That(boss.Hp, Is.GreaterThan(700));
        }
    }
}
