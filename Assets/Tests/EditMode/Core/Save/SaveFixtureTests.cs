using System.IO;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Save
{
    /// <summary>
    /// Saves written by released schema versions must load forever (SAVE_SYSTEM §4). Fixtures in
    /// Assets/Tests/SaveFixtures are never edited or regenerated; a new schema adds vN+1 next to them.
    /// </summary>
    public class SaveFixtureTests
    {
        private static SaveData LoadFixture(string name) =>
            new SaveSerializer().Deserialize(File.ReadAllBytes(TestPaths.RepoFile("Assets/Tests/SaveFixtures/" + name)));

        [Test]
        public void V1_Loads_WithExpectedContent()
        {
            var d = LoadFixture("v1.dfrs");
            Assert.AreEqual(638_000_000_000_000_000, d.SavedUtcTicks);
            Assert.AreEqual(31, d.State.Character.Level);
            Assert.AreEqual(3, d.State.World.Region);
            Assert.AreEqual(40, d.State.Inventory.Items.Count);
            Assert.AreEqual(5, d.State.PendingOfflineLoot.Count);
            Assert.AreEqual(3971.6189841642213, d.State.Gold, 1e-9);
            Assert.AreEqual(ProgressionSystem.CustomPreset, d.State.Character.Preset);
            Assert.AreEqual(4, d.State.Character.CustomWeights[AttributeId.Vit]);
        }

        [Test]
        public void V1_ContinuesPlaying()
        {
            var d = LoadFixture("v1.dfrs");
            var s = new GameSession(TestConfig.Load(), d.State, Pcg32.FromState(d.RngState, d.RngIncrement), new EventBus());
            s.Collect();
            Assert.That(s.Advance(600), Is.GreaterThan(10));
        }
    }
}
