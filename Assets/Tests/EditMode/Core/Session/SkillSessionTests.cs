using System.Diagnostics;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class SkillSessionTests
    {
        private const string Swing = "fighter_heavy_swing";

        private static GameSession NewSession(ulong seed = 4, bool unlocked = false)
        {
            var s = new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(seed), new EventBus());
            if (unlocked)
            {
                s.State.Account.HeroesUnlocked = true;
                s.EnsureHeroKits();
            }
            return s;
        }

        private static GameSession WithSwing(ulong seed = 4, bool unlocked = false)
        {
            var s = NewSession(seed, unlocked);
            s.State.Character.Level = 20;
            Assert.AreEqual(SkillResult.Ok, s.AddSkillRank(Swing));
            Assert.AreEqual(SkillResult.Ok, s.SlotSkill(Swing, 0));
            return s;
        }

        [Test]
        public void Timers_CarryAcrossFights_IncludingRespawnGap()
        {
            var s = WithSwing();
            var f = s.PrepareNextFight();
            Assert.AreEqual(1, f.Kit.Actives.Count);
            Assert.Greater(f.CooldownsAfter[0], 0, "the swing was cast in a short fight");
            Assert.AreEqual(0, s.CooldownLeft(ClassId.Fighter, Swing), "not committed before Apply");
            s.Apply(f);
            double expected = System.Math.Max(0, f.CooldownsAfter[0] - TestConfig.Load().Combat.EnemyRespawnS);
            Assert.AreEqual(expected, s.CooldownLeft(ClassId.Fighter, Swing), 1e-9);
            var g = s.PrepareNextFight();
            Assert.AreEqual(expected, g.CooldownsBefore[0], 1e-9);
        }

        [Test]
        public void Switch_DroppedFight_DoesNotAdvanceTimers()
        {
            var s = WithSwing(unlocked: true);
            s.PrepareNextFight();
            s.DropPreparedFight();
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Archer));
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Fighter));
            Assert.AreEqual(0, s.CooldownLeft(ClassId.Fighter, Swing));
        }

        [Test]
        public void Timers_PerHero()
        {
            var s = WithSwing(unlocked: true);
            s.Apply(s.PrepareNextFight());
            Assert.Greater(s.CooldownLeft(ClassId.Fighter, Swing), 0);
            s.SwitchTo(ClassId.Archer);
            s.State.Character.Level = 20;
            s.AddSkillRank("archer_aimed_shot");
            s.SlotSkill("archer_aimed_shot", 0);
            Assert.AreEqual(0, s.CooldownLeft(ClassId.Archer, "archer_aimed_shot"));
            Assert.Greater(s.CooldownLeft(ClassId.Fighter, Swing), 0, "the Fighter's timer is kept while away");
        }

        [Test]
        public void NewlySlotted_StartsReady_SwapKeepsTimer()
        {
            var s = WithSwing();
            s.Apply(s.PrepareNextFight());
            double left = s.CooldownLeft(ClassId.Fighter, Swing);
            Assert.Greater(left, 0);
            Assert.AreEqual(SkillResult.Ok, s.SwapSlots(0, 2));
            Assert.AreEqual(left, s.CooldownLeft(ClassId.Fighter, Swing), 1e-12);
            s.UnslotSkill(2);
            Assert.AreEqual(SkillResult.Ok, s.SlotSkill(Swing, 1));
            Assert.AreEqual(0, s.CooldownLeft(ClassId.Fighter, Swing));
        }

        [Test]
        public void AutoSpend_On_SpendsOnLevelUp_Offline()
        {
            var s = NewSession();
            s.State.Character.AutoSpendSkills = true;
            s.SimulateOffline(3 * 3600);
            Assert.GreaterOrEqual(s.State.Character.Level, 10);
            Assert.AreEqual(0, s.Skills.Unspent(s.State.Character));
            Assert.Greater(s.Skills.Spent(s.State.Character), 0);
            Assert.AreEqual(Swing, s.State.Character.Loadout[0]);
        }

        [Test]
        public void AutoSpend_Off_LeavesPointsUnspent()
        {
            var s = NewSession();
            s.SimulateOffline(3 * 3600);
            Assert.GreaterOrEqual(s.State.Character.Level, 10);
            Assert.AreEqual(0, s.Skills.Spent(s.State.Character));
            Assert.Greater(s.Skills.Unspent(s.State.Character), 0);
        }

        [Test]
        public void RespecSkills_ChargesActiveHeroOnly()
        {
            var s = WithSwing(unlocked: true);
            s.State.Heroes[1].Level = 20;
            s.State.Heroes[1].SkillRanks["archer_aimed_shot"] = 1;
            double cost = s.Skills.RespecCost(s.State.Character);
            s.State.Wallet.Gold = cost;
            int changes = 0;
            s.Events.Subscribe<SkillsChanged>(_ => changes++);
            Assert.AreEqual(SkillResult.Ok, s.RespecSkills());
            Assert.IsEmpty(s.State.Character.SkillRanks);
            Assert.AreEqual(1, s.State.Heroes[1].SkillRanks["archer_aimed_shot"]);
            Assert.AreEqual(0, s.State.Wallet.Gold, 1e-9);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SessionStart_SanitizesEveryHero()
        {
            var state = GameState.NewRun(ClassId.Fighter);
            state.Heroes[0].Level = 10;
            state.Heroes[0].SkillRanks["gone"] = 1;
            state.Heroes[2].SkillRanks["mage_firebolt"] = 3; // level 1: nothing earned
            new GameSession(TestConfig.Load(), state, new Pcg32(1), new EventBus());
            Assert.IsEmpty(state.Heroes[0].SkillRanks);
            Assert.IsEmpty(state.Heroes[2].SkillRanks);
        }

        [Test]
        public void NoRanks_SessionResultsIdenticalToM5()
        {
            var a = new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(11), new EventBus());
            var b = new GameSession(TestConfig.LoadWithoutSkills(), GameState.NewRun(ClassId.Fighter), new Pcg32(11), new EventBus());
            a.Advance(1800);
            b.Advance(1800);
            Assert.AreEqual(b.State.GameTime, a.State.GameTime);
            Assert.AreEqual(b.State.Wallet.Gold, a.State.Wallet.Gold);
            Assert.AreEqual(b.State.Character.Level, a.State.Character.Level);
            Assert.AreEqual(b.State.Character.Xp, a.State.Character.Xp);
            Assert.AreEqual(b.State.Inventory.Items.Count, a.State.Inventory.Items.Count);
        }

        [Test]
        public void TwelveHoursAway_WithFullTree_SimulatesQuickly()
        {
            var s = NewSession();
            s.State.Character.Level = 260;
            s.Skills.AutoSpend(s.State.Character);
            Assert.AreEqual(52, s.Skills.Spent(s.State.Character));
            var sw = Stopwatch.StartNew();
            s.SimulateOffline(6 * 3600);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "desktop budget, as OfflineTests");
        }
    }
}
