using System.Collections.Generic;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class GameSessionTests
    {
        private static GameSession NewSession(ulong seed = 42, EventBus bus = null) =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(seed), bus ?? new EventBus());

        [Test]
        public void Apply_Won_GrantsXpAndGold()
        {
            var s = NewSession();
            var fight = s.PrepareNextFight();
            Assert.AreEqual(FightOutcome.Won, fight.Result.Outcome); // level-1 Fighter beats a level-1 enemy
            s.Apply(fight);
            Assert.AreEqual(3.0, s.State.Character.Xp, 1e-9);  // EnemyXp(1) = 3
            Assert.AreEqual(2.0, s.State.Gold, 1e-9);          // GoldDrop(1) = 2
            Assert.AreEqual(1, s.State.World.KillsInSubzone(1));
        }

        [Test]
        public void Apply_Escaped_GrantsNothing()
        {
            var s = NewSession();
            var fight = s.PrepareNextFight();
            fight.Result = new FightResult { Outcome = FightOutcome.Escaped, Duration = 30, Events = new CombatEvent[0] };
            s.Apply(fight);
            Assert.AreEqual(0.0, s.State.Character.Xp);
            Assert.AreEqual(0.0, s.State.Gold);
            Assert.AreEqual(1, s.State.World.ConsecutiveEscapes);
        }

        [Test]
        public void Apply_AdvancesGameTime()
        {
            var s = NewSession();
            var fight = s.PrepareNextFight();
            s.Apply(fight);
            Assert.AreEqual(fight.Result.Duration + 0.6, s.State.GameTime, 1e-9);
        }

        [Test]
        public void Prepare_DoesNotChangeState()
        {
            var s = NewSession();
            s.Advance(120);
            var st = s.State;
            double time = st.GameTime, xp = st.Character.Xp, gold = st.Gold;
            int kills = st.World.KillsInSubzone(st.World.UnlockedSubzone), level = st.Character.Level;
            s.PrepareNextFight();
            Assert.AreEqual(time, st.GameTime);
            Assert.AreEqual(xp, st.Character.Xp);
            Assert.AreEqual(gold, st.Gold);
            Assert.AreEqual(level, st.Character.Level);
            Assert.AreEqual(kills, st.World.KillsInSubzone(st.World.UnlockedSubzone));
        }

        [Test]
        public void Advance_SameSeed_SameState()
        {
            var a = NewSession(7);
            var b = NewSession(7);
            a.Advance(3600);
            b.Advance(3600);
            Assert.AreEqual(a.State.GameTime, b.State.GameTime);
            Assert.AreEqual(a.State.Character.Level, b.State.Character.Level);
            Assert.AreEqual(a.State.Character.Xp, b.State.Character.Xp);
            Assert.AreEqual(a.State.Gold, b.State.Gold);
            Assert.AreEqual(a.State.World.UnlockedSubzone, b.State.World.UnlockedSubzone);
        }

        [Test]
        public void Advance_FreshFighter_ReachesRegion1Boss()
        {
            var bus = new EventBus();
            int bossFights = 0;
            bus.Subscribe<WorldChanged>(e =>
            {
                if (e.Change == WorldChange.BossFailed || e.Change == WorldChange.BossDefeated) bossFights++;
            });
            var s = NewSession(3, bus);
            s.Advance(4 * 3600);
            Assert.That(bossFights, Is.GreaterThan(0));
            Assert.That(s.State.Character.Level, Is.GreaterThan(5));
        }

        [Test]
        public void Events_PublishedOnKillAndLevelUp()
        {
            var bus = new EventBus();
            var kills = new List<EnemyKilled>();
            var levels = new List<LevelUp>();
            int resolved = 0;
            bus.Subscribe<EnemyKilled>(kills.Add);
            bus.Subscribe<LevelUp>(levels.Add);
            bus.Subscribe<FightResolved>(_ => resolved++);
            var s = NewSession(5, bus);
            s.Advance(600);
            Assert.That(kills.Count, Is.GreaterThan(10));
            Assert.That(levels.Count, Is.GreaterThan(0));
            Assert.AreEqual(2, levels[0].Level);
            Assert.That(resolved, Is.GreaterThanOrEqualTo(kills.Count));
        }

        [Test]
        public void EventBus_UnsubscribeStopsDelivery()
        {
            var bus = new EventBus();
            int n = 0;
            System.Action<LevelUp> h = _ => n++;
            bus.Subscribe(h);
            bus.Publish(new LevelUp(2));
            bus.Unsubscribe(h);
            bus.Publish(new LevelUp(3));
            Assert.AreEqual(1, n);
        }
    }
}
