using System.Diagnostics;
using System.Linq;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Offline;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Offline
{
    public class OfflineTests
    {
        private const long TicksPerSecond = 10_000_000;
        private static OfflineCalculator Calc() => new OfflineCalculator(TestConfig.Load());

        [Test]
        public void TwoHoursAway_HalfCredited_WithReport()
        {
            var c = Calc().Compute(0, 100, 7200 * TicksPerSecond, 100 + 7200);
            Assert.AreEqual(7200, c.CountedSeconds, 1e-6);
            Assert.AreEqual(3600, c.CreditedSeconds, 1e-6);
            Assert.IsTrue(c.ShowReport);
            Assert.AreEqual(OfflineAdjustment.None, c.Adjustment);
        }

        [Test]
        public void ShortAbsence_CreditedSilently()
        {
            var c = Calc().Compute(0, 0, 90 * TicksPerSecond, 90);
            Assert.AreEqual(45, c.CreditedSeconds, 1e-6);
            Assert.IsFalse(c.ShowReport);
        }

        [Test]
        public void ClockRollback_NoCredit()
        {
            var c = Calc().Compute(1000 * TicksPerSecond, 50, 0, 60);
            Assert.AreEqual(0, c.CreditedSeconds);
            Assert.AreEqual(OfflineAdjustment.ClockRolledBack, c.Adjustment);
        }

        [Test]
        public void ClockJumpBeyondUptime_Clamped()
        {
            // same boot: uptime advanced 10 minutes, but the wall clock claims 10 hours
            var c = Calc().Compute(0, 1000, 36000 * TicksPerSecond, 1000 + 600);
            Assert.AreEqual(600, c.CountedSeconds, 1e-6);
            Assert.AreEqual(OfflineAdjustment.ClampedToUptime, c.Adjustment);
        }

        [Test]
        public void AfterReboot_WallClockTrusted_WithinCap()
        {
            // uptime is smaller than at save time: the device restarted, so uptime cannot be compared
            var c = Calc().Compute(0, 5000, 3 * 3600 * TicksPerSecond, 200);
            Assert.AreEqual(3 * 3600, c.CountedSeconds, 1e-6);
        }

        [Test]
        public void HugeAbsence_Capped()
        {
            var c = Calc().Compute(0, 0, long.MaxValue, -1);
            Assert.AreEqual(12 * 3600, c.CountedSeconds, 1e-6);
            Assert.AreEqual(6 * 3600, c.CreditedSeconds, 1e-6);
            Assert.AreEqual(OfflineAdjustment.Capped, c.Adjustment);
        }

        private static GameSession NewSession(ulong seed = 3) =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(seed), new EventBus());

        [Test]
        public void SimulateOffline_ReportMatchesStateChanges_AndHoldsLoot()
        {
            var s = NewSession();
            double gold0 = s.State.Gold;
            int level0 = s.State.Character.Level;
            int bag0 = s.State.Inventory.Items.Count;
            var equipped0 = s.State.Character.Equipped.ToDictionary(kv => kv.Key, kv => kv.Value);

            var r = s.SimulateOffline(2 * 3600);

            Assert.That(r.Kills, Is.GreaterThan(300));
            Assert.AreEqual(s.State.Gold - gold0, r.Gold, 1e-6);
            Assert.AreEqual(s.State.Character.Level - level0, r.LevelsGained);
            Assert.That(r.HeldItems, Is.GreaterThan(10));
            Assert.AreEqual(r.HeldItems, s.State.PendingOfflineLoot.Count);
            Assert.AreEqual(bag0, s.State.Inventory.Items.Count, "loot waits for Collect");
            CollectionAssert.AreEquivalent(equipped0, s.State.Character.Equipped, "offline never equips (R2-18)");
            Assert.AreEqual(2 * 3600, r.CreditedSeconds);
        }

        [Test]
        public void SimulateOffline_RespectsBossToggle()
        {
            var s = NewSession();
            s.State.World.FightBossWhenAvailable = false;
            s.State.Account.TrainingBossDone = true;
            s.State.World.TrainingBossPending = false;
            var r = s.SimulateOffline(3 * 3600);
            Assert.AreEqual(0, r.BossesDefeated + r.BossesFailed);
        }

        [Test]
        public void SimulateOffline_AppliesAutoSalvage()
        {
            var s = NewSession();
            s.State.Inventory.AutoSalvage = AutoSalvageMode.CommonAndUncommon;
            var r = s.SimulateOffline(2 * 3600);
            Assert.That(r.AutoSalvaged, Is.GreaterThan(0));
            Assert.That(r.Materials, Is.GreaterThan(0));
            Assert.IsTrue(s.State.PendingOfflineLoot.All(i => i.Rarity >= Rarity.Rare));
        }

        [Test]
        public void Collect_MovesLootIntoBag()
        {
            var s = NewSession();
            var r = s.SimulateOffline(3600);
            int bag0 = s.State.Inventory.Items.Count;
            var c = s.Collect();
            Assert.AreEqual(r.HeldItems, c.Kept);
            Assert.AreEqual(bag0 + r.HeldItems, s.State.Inventory.Items.Count);
            Assert.IsEmpty(s.State.PendingOfflineLoot);
        }

        [Test]
        public void Collect_BagFull_SalvagesRest()
        {
            var s = NewSession();
            s.SimulateOffline(3600);
            int held = s.State.PendingOfflineLoot.Count;
            s.State.Inventory.Capacity = 3; // only 3 free spaces (the worn sword does not count)
            double materials0 = s.State.Wallet.Materials;
            var c = s.Collect();
            Assert.AreEqual(3, c.Kept);
            Assert.AreEqual(held - 3, c.SalvagedBagFull);
            Assert.That(s.State.Wallet.Materials, Is.GreaterThan(materials0));
            Assert.IsEmpty(s.State.PendingOfflineLoot);
        }

        [Test]
        public void SimulateOffline_PublishesNoEvents()
        {
            var bus = new EventBus();
            int events = 0;
            bus.Subscribe<EnemyKilled>(_ => events++);
            bus.Subscribe<ItemDropped>(_ => events++);
            bus.Subscribe<LevelUp>(_ => events++);
            var s = new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(9), bus);
            s.SimulateOffline(1800);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void TwelveHoursAway_SimulatesQuickly()
        {
            var s = NewSession();
            var sw = Stopwatch.StartNew();
            s.SimulateOffline(6 * 3600); // 12 h away at 50 %
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "desktop budget; the phone target (< 200 ms) is checked on device (M4-3)");
        }
    }
}
