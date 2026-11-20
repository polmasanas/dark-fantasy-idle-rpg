using DarkFantasy.Core.Look;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Look
{
    public class EnemyLookTests
    {
        private static EnemyLooks Looks() =>
            new EnemyLooks(LookConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.LookConfigPath)));

        [Test]
        public void Region1_Subzones_UseTheFiveTrainingTargets()
        {
            var looks = Looks();
            var expected = new[] { "dummy", "target_post", "sparring_frame", "shield_board", "weighted_sack" };
            for (int sub = 1; sub <= 5; sub++)
            {
                var l = looks.For(new Encounter(1, sub, sub * 2, false));
                Assert.AreEqual(expected[sub - 1], l.Body);
                Assert.AreEqual(LookRank.Normal, l.Rank);
                Assert.IsFalse(l.UseBossCamera);
            }
            Assert.AreEqual("Target post", looks.For(new Encounter(1, 2, 4, false)).DisplayName);
        }

        [Test]
        public void Region1_Boss_IsTheRecruit_WithCamera()
        {
            var l = Looks().For(new Encounter(1, 5, 12, true));
            Assert.AreEqual("recruit", l.Body);
            Assert.AreEqual(LookRank.Boss, l.Rank);
            Assert.IsTrue(l.UseBossCamera);
            Assert.AreEqual("Bram Hollis · First of the Recruits", l.DisplayName);
        }

        [Test]
        public void TrainingBoss_IsTheInstructor_NoCamera()
        {
            var l = Looks().For(new Encounter(1, 1, 2, true, true));
            Assert.AreEqual("instructor", l.Body);
            Assert.AreEqual(LookRank.Boss, l.Rank);
            Assert.IsFalse(l.UseBossCamera);
            Assert.AreEqual("Maera Dunn · Sergeant, Drillmaster", l.DisplayName);
        }

        [Test]
        public void Region2_Normal_UsesHumanoidKit_WithAshAccent()
        {
            var l = Looks().For(new Encounter(2, 3, 16, false));
            Assert.AreEqual("cultist", l.Body);
            Assert.AreEqual("#E8A24A", l.GlowHex);
        }

        [Test]
        public void Region9_Boss_UsesFallbackBoss()
        {
            var l = Looks().For(new Encounter(9, 5, 90, true));
            Assert.AreEqual("humanoid_boss", l.Body);
            Assert.AreEqual("Nameless Warlord · of the Wilds", l.DisplayName);
        }

        [Test]
        public void ForClass_Fighter()
        {
            var c = Looks().ForClass(ClassId.Fighter);
            Assert.AreEqual("fighter", c.Body);
            Assert.AreEqual("#E8A24A", c.Glow);
        }
    }
}
