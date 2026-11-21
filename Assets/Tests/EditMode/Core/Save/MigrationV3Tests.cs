using System.IO;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Save
{
    public class MigrationV3Tests
    {
        private static SaveData Fixture(string name) =>
            new SaveSerializer().Deserialize(File.ReadAllBytes(TestPaths.RepoFile("Assets/Tests/SaveFixtures/" + name)));

        /// <summary>A new run with its inventory and starter kit, as a real session creates it.</summary>
        private static GameState Fresh() =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(2), new EventBus()).State;

        private static SaveData RoundTrip(GameState state) =>
            new SaveSerializer().Deserialize(new SaveSerializer().Serialize(new SaveData { State = state, RngState = 1, RngIncrement = 3 }));

        [Test]
        public void SchemaIsThree()
        {
            Assert.AreEqual(3, SaveSerializer.CurrentSchemaVersion);
        }

        [Test]
        public void RoundTrip_SkillsLoadoutAutoSpend()
        {
            var state = Fresh();
            var f = state.Heroes[0];
            f.Level = 30;
            f.SkillRanks["fighter_heavy_swing"] = 2;
            f.SkillRanks["fighter_toughness"] = 3;
            f.Loadout[1] = "fighter_heavy_swing";
            f.AutoSpendSkills = true;
            var back = RoundTrip(state).State.Heroes[0];
            Assert.AreEqual(2, back.SkillRanks["fighter_heavy_swing"]);
            Assert.AreEqual(3, back.SkillRanks["fighter_toughness"]);
            CollectionAssert.AreEqual(new string[] { null, "fighter_heavy_swing", null }, back.Loadout);
            Assert.IsTrue(back.AutoSpendSkills);
            Assert.IsFalse(RoundTrip(state).State.Heroes[1].AutoSpendSkills);
        }

        [Test]
        public void MigrationV2ToV3_AddsEmptySkills()
        {
            var hero = new JObject { ["class"] = "Fighter", ["level"] = 3 };
            var payload = new JObject { ["heroes"] = new JArray { hero } };
            var result = new V2ToV3().Apply(payload);
            var h = (JObject)((JArray)result["heroes"])[0];
            Assert.AreEqual(2, new V2ToV3().From);
            Assert.AreEqual(0, ((JObject)h["skills"]).Count);
            Assert.AreEqual(3, ((JArray)h["loadout"]).Count);
            Assert.IsFalse((bool)h["autoSpendSkills"]);
        }

        [Test]
        public void V1Fixture_StillLoads()
        {
            var d = Fixture("v1.dfrs");
            Assert.AreEqual(31, d.State.Character.Level);
            Assert.IsEmpty(d.State.Character.SkillRanks);
            Assert.AreEqual(3, d.State.Character.Loadout.Length);
        }

        [Test]
        public void V2Fixture_StillLoads()
        {
            var d = Fixture("v2.dfrs");
            Assert.AreEqual(ClassId.Archer, d.State.Character.Class);
            foreach (var h in d.State.Heroes) Assert.IsEmpty(h.SkillRanks);
        }

        [Test]
        public void V3Fixture_Loads()
        {
            var d = Fixture("v3.dfrs");
            var f = d.State.Heroes[0];
            Assert.AreEqual(30, f.Level);
            Assert.AreEqual(3, f.SkillRanks["fighter_heavy_swing"]);
            Assert.AreEqual(2, f.SkillRanks["fighter_toughness"]);
            Assert.AreEqual(1, f.SkillRanks["fighter_rend"]);
            CollectionAssert.AreEqual(new[] { "fighter_rend", "fighter_heavy_swing", null }, f.Loadout);
            Assert.IsTrue(f.AutoSpendSkills);
            var s = new GameSession(TestConfig.Load(), d.State, Pcg32.FromState(d.RngState, d.RngIncrement), new EventBus());
            Assert.AreEqual(0, s.Skills.Unspent(f));
            Assert.That(s.Advance(300), Is.GreaterThan(5));
        }

        [Test]
        public void Load_UnknownSkillId_DroppedAndRefunded()
        {
            var state = Fresh();
            state.Heroes[0].Level = 30;
            state.Heroes[0].SkillRanks["fighter_heavy_swing"] = 1;
            state.Heroes[0].SkillRanks["removed_in_a_later_version"] = 4;
            state.Heroes[0].Loadout[0] = "removed_in_a_later_version";
            var loaded = RoundTrip(state).State;
            var s = new GameSession(TestConfig.Load(), loaded, new Pcg32(1), new EventBus());
            Assert.IsFalse(loaded.Heroes[0].SkillRanks.ContainsKey("removed_in_a_later_version"));
            Assert.IsNull(loaded.Heroes[0].Loadout[0]);
            Assert.AreEqual(5, s.Skills.Unspent(loaded.Heroes[0]));
        }

        [Test]
        public void Load_SpentAboveEarned_ResetsTree()
        {
            var state = Fresh();
            state.Heroes[0].Level = 10; // 2 points
            state.Heroes[0].SkillRanks["fighter_heavy_swing"] = 3;
            state.Heroes[0].Loadout[0] = "fighter_heavy_swing";
            var loaded = RoundTrip(state).State;
            var s = new GameSession(TestConfig.Load(), loaded, new Pcg32(1), new EventBus());
            Assert.IsEmpty(loaded.Heroes[0].SkillRanks);
            Assert.AreEqual(2, s.Skills.Unspent(loaded.Heroes[0]));
        }
    }
}
