using System.Collections.Generic;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class FightReplayerTests
    {
        private sealed class RecordingView : IFightView
        {
            public readonly List<PreparedFight> Begun = new List<PreparedFight>();
            public readonly List<CombatEvent> Shown = new List<CombatEvent>();
            public readonly List<PreparedFight> Ended = new List<PreparedFight>();
            public void BeginFight(PreparedFight f) => Begun.Add(f);
            public void ShowEvent(PreparedFight f, CombatEvent e) => Shown.Add(e);
            public void EndFight(PreparedFight f) => Ended.Add(f);
        }

        private static GameSession Session() =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(9), new EventBus());

        [Test]
        public void Tick_ShowsEventsInOrder_ThenAppliesOnce()
        {
            var s = Session();
            var view = new RecordingView();
            var r = new FightReplayer(s, view);
            r.Tick(0.0001);
            Assert.AreEqual(1, view.Begun.Count);
            var fight = view.Begun[0];
            Assert.AreEqual(0.0, s.State.GameTime, "not applied before the replay ends");

            double total = fight.Result.Duration + s.RespawnSeconds;
            r.Tick(total - 0.05 - 0.0001); // stop 0.05 s before the end of the replay
            Assert.AreEqual(0, view.Ended.Count);
            CollectionAssert.AreEqual(fight.Result.Events, view.Shown);

            r.Tick(0.1);
            Assert.AreEqual(1, view.Ended.Count);
            Assert.AreEqual(total, s.State.GameTime, 1e-9);
        }

        [Test]
        public void Tick_EventsNotShownEarly()
        {
            var s = Session();
            var view = new RecordingView();
            var r = new FightReplayer(s, view);
            r.Tick(0.0001);
            double firstEventTime = view.Begun[0].Result.Events[0].Time;
            r.Tick(firstEventTime - 0.01);
            Assert.AreEqual(0, view.Shown.Count);
            r.Tick(0.02);
            Assert.AreEqual(1, view.Shown.Count);
        }

        [Test]
        public void Tick_LargeStep_PlaysSeveralFights()
        {
            var s = Session();
            var view = new RecordingView();
            var r = new FightReplayer(s, view);
            r.Tick(300);
            Assert.That(view.Ended.Count, Is.GreaterThan(5));
            Assert.That(view.Begun.Count, Is.EqualTo(view.Ended.Count).Or.EqualTo(view.Ended.Count + 1));
            Assert.That(s.State.GameTime, Is.LessThanOrEqualTo(300.0 + 1e-9));
        }

        [Test]
        public void Tick_ZeroOrNegative_DoesNotAdvance()
        {
            var s = Session();
            var view = new RecordingView();
            var r = new FightReplayer(s, view);
            r.Tick(0);
            r.Tick(-5);
            r.Tick(double.NaN);
            Assert.AreEqual(0, view.Shown.Count);
            Assert.AreEqual(0, view.Ended.Count);
        }
    }
}
