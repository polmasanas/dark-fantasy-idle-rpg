using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class AutoAllocateSessionTests
    {
        private static GameSession NewSession() =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(5), new EventBus());

        [Test]
        public void ToggleOn_SpendsPointsOnLevelUp_Online()
        {
            var s = NewSession();
            s.State.Character.AutoAllocateOnLevelUp = true;
            s.Advance(1800);
            Assert.That(s.State.Character.Level, Is.GreaterThan(1));
            Assert.AreEqual(0, s.State.Character.UnspentPoints);
            Assert.AreEqual(s.State.Character.Level - 1, s.State.Character.ManualPoints[AttributeId.Str]);
        }

        [Test]
        public void ToggleOn_SpendsPointsOnLevelUp_Offline()
        {
            var s = NewSession();
            s.State.Character.AutoAllocateOnLevelUp = true;
            s.State.Character.Preset = ProgressionSystem.CustomPreset;
            s.Progression.SetCustomWeight(s.State.Character, AttributeId.Vit, 1);
            s.SimulateOffline(1800);
            Assert.AreEqual(0, s.State.Character.UnspentPoints);
            Assert.AreEqual(s.State.Character.Level - 1, s.State.Character.ManualPoints[AttributeId.Vit]);
        }

        [Test]
        public void ToggleOff_KeepsPoints()
        {
            var s = NewSession();
            s.Advance(1800);
            Assert.AreEqual(s.State.Character.Level - 1, s.State.Character.UnspentPoints);
            Assert.AreEqual(0, s.State.Character.ManualPoints[AttributeId.Str]);
        }
    }
}
