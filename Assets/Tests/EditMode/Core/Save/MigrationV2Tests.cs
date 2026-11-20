using System.IO;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Save
{
    public class MigrationV2Tests
    {
        private static SaveData LoadV1() =>
            new SaveSerializer().Deserialize(File.ReadAllBytes(TestPaths.RepoFile("Assets/Tests/SaveFixtures/v1.dfrs")));

        [Test]
        public void SchemaIsTwo()
        {
            Assert.AreEqual(2, SaveSerializer.CurrentSchemaVersion);
        }

        [Test]
        public void V1Fixture_StillLoads_AsFighter()
        {
            var d = LoadV1();
            Assert.AreEqual(3, d.State.Heroes.Count);
            Assert.AreEqual(0, d.State.ActiveHero);
            Assert.AreEqual(ClassId.Fighter, d.State.Character.Class);
            Assert.AreEqual(31, d.State.Character.Level);
            Assert.AreEqual(1, d.State.Heroes[1].Level);
            Assert.AreEqual(1, d.State.Heroes[2].Level);
            Assert.IsTrue(d.State.Account.HeroesUnlocked, "region 3 means the region 1 boss was beaten");
        }

        [Test]
        public void Migration_V1PastRegion1_UnlocksHeroes()
        {
            var d = LoadV1();
            var s = new GameSession(TestConfig.Load(), d.State, Pcg32.FromState(d.RngState, d.RngIncrement), new EventBus());
            foreach (var cls in new[] { ClassId.Archer, ClassId.Mage })
            {
                Assert.IsTrue(s.State.Heroes[(int)cls].Equipped.TryGetValue(EquipSlot.MainHand, out var id));
                Assert.AreEqual(Rarity.Common, s.State.Inventory.Find(id).Rarity);
            }
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Archer));
        }

        [Test]
        public void Migration_V1BeforeRegion1Boss_Locked()
        {
            var v1 = new JObject
            {
                ["character"] = new JObject { ["class"] = "Fighter" },
                ["world"] = new JObject { ["highestBossDefeated"] = 0 },
                ["account"] = new JObject { ["trainingBossDone"] = true },
            };
            var v2 = new SaveSerializer().Migrate(v1, 1);
            Assert.IsNull(v2["character"]);
            Assert.AreEqual(3, ((JArray)v2["heroes"]).Count);
            Assert.AreEqual("Archer", (string)v2["heroes"][1]["class"]);
            Assert.AreEqual(0, (int)v2["activeHero"]);
            Assert.IsFalse((bool)v2["account"]["heroesUnlocked"]);
        }

        [Test]
        public void RoundTrip_V2_KeepsActiveHeroAndUnlock()
        {
            var state = GameState.NewRun(ClassId.Fighter);
            var s = new GameSession(TestConfig.Load(), state, new Pcg32(3), new EventBus());
            s.State.Account.HeroesUnlocked = true;
            s.EnsureHeroKits();
            s.SwitchTo(ClassId.Mage);
            s.State.Heroes[2].Level = 7;
            var rng = new Pcg32(3);
            var bytes = new SaveSerializer().Serialize(new SaveData { State = s.State, RngState = rng.State, RngIncrement = rng.Increment });
            var loaded = new SaveSerializer().Deserialize(bytes).State;
            Assert.AreEqual(2, loaded.ActiveHero);
            Assert.IsTrue(loaded.Account.HeroesUnlocked);
            Assert.AreEqual(7, loaded.Character.Level);
            Assert.AreEqual(ClassId.Mage, loaded.Character.Class);
            Assert.IsTrue(loaded.Heroes[1].Equipped.ContainsKey(EquipSlot.MainHand));
        }
    }
}
