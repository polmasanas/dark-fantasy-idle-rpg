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

        [Test]
        public void BossSize_GrowsWithRegion_FromOneAndAHalfToThree()
        {
            var looks = Looks();
            Assert.AreEqual(1.5, looks.For(new Encounter(1, 5, 12, true)).SizeVsRegular, 1e-9);
            Assert.AreEqual(2.0, looks.For(new Encounter(6, 5, 60, true)).SizeVsRegular, 1e-9);
            Assert.AreEqual(3.0, looks.For(new Encounter(16, 5, 160, true)).SizeVsRegular, 1e-9);
            Assert.AreEqual(3.0, looks.For(new Encounter(90, 5, 900, true)).SizeVsRegular, 1e-9);
        }

        [Test]
        public void NormalEnemies_AndTrainingBoss_AreRegularSize()
        {
            var looks = Looks();
            Assert.AreEqual(1.0, looks.For(new Encounter(3, 2, 30, false)).SizeVsRegular, 1e-9);
            Assert.AreEqual(1.0, looks.For(new Encounter(1, 1, 2, true, true)).SizeVsRegular, 1e-9);
        }

        [Test]
        public void Motions_LoadPerPart()
        {
            var c = LookConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.LookConfigPath));
            Assert.AreEqual(55, c.Motions["weapon"].AttackDeg, 1e-9); // positive = clockwise = forward for a right-facing hero
            Assert.AreEqual(3.4, c.Motions["cloak"].PeriodS, 1e-9);
            Assert.IsFalse(c.Motions.ContainsKey("_comment"));
        }

        [Test]
        public void Region4_Subzones_ElementalsThenWizards()
        {
            var looks = Looks();
            Assert.AreEqual("shard_thrower", looks.For(new Encounter(4, 1, 40, false)).Body);
            Assert.AreEqual("hierophant", looks.For(new Encounter(4, 5, 48, false)).Body);
            Assert.AreEqual("frozen_oath", looks.For(new Encounter(4, 5, 50, true)).Body);
        }

        [Test]
        public void Glow_UsesTheEnemysElement()
        {
            var l = Looks().For(new Encounter(4, 2, 42, false), "fire");
            Assert.AreEqual("#FF7A3D", l.GlowHex);
        }

        [Test]
        public void KitOrder_MatchesFamilyArchetypes()
        {
            var balance = TestConfig.Load();
            var factory = new EnemyFactory(balance);
            var look = LookConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.LookConfigPath));
            var regions = new RegionLooks(look);
            for (int r = 1; r <= balance.Regions.SubzoneFamilies.Count; r++)
                for (int sub = 1; sub <= 5; sub++)
                {
                    var family = factory.FamilyFor(new Encounter(r, sub, 1, false));
                    string kit = regions.KitFor(r, sub);
                    Assert.AreEqual(family.Archetypes.Length, look.Kits[kit].Length, $"region {r} subzone {sub}: kit {kit} vs family {family.Id}");
                }
        }
    }
}
