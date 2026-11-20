using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Save
{
    public class SaveSerializerTests
    {
        private static (GameSession session, Pcg32 rng) PlayedSession(double seconds)
        {
            var rng = new Pcg32(42);
            var bus = new EventBus();
            GameSession s = null;
            bus.Subscribe<ItemDropped>(_ => s.EquipUpgrades());
            s = new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), rng, bus);
            s.Advance(seconds);
            return (s, rng);
        }

        private static SaveData Snapshot(GameSession s, Pcg32 rng) => new SaveData
        {
            State = s.State, RngState = rng.State, RngIncrement = rng.Increment,
            SavedUtcTicks = 638_000_000_000_000_000, SavedUptimeSeconds = 1234.5, PlaytimeSeconds = 99,
        };

        [Test]
        public void RoundTrip_SameNextFight()
        {
            var (s, rng) = PlayedSession(1800);
            var bytes = new SaveSerializer().Serialize(Snapshot(s, rng));
            var loaded = new SaveSerializer().Deserialize(bytes);

            var rng2 = Pcg32.FromState(loaded.RngState, loaded.RngIncrement);
            var s2 = new GameSession(TestConfig.Load(), loaded.State, rng2, new EventBus());

            Assert.AreEqual(s.State.Character.Level, s2.State.Character.Level);
            Assert.AreEqual(s.State.Character.Xp, s2.State.Character.Xp);
            Assert.AreEqual(s.State.Gold, s2.State.Gold);
            Assert.AreEqual(s.State.GameTime, s2.State.GameTime);
            Assert.AreEqual(s.State.World.UnlockedSubzone, s2.State.World.UnlockedSubzone);
            Assert.AreEqual(s.State.Inventory.Items.Count, s2.State.Inventory.Items.Count);
            CollectionAssert.AreEquivalent(s.State.Character.Equipped, s2.State.Character.Equipped);
            var a = s.State.Inventory.Items.Last();
            var b = s2.State.Inventory.Items.Last();
            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual(a.Rarity, b.Rarity);
            CollectionAssert.AreEqual(a.Attributes.Select(x => x.Value), b.Attributes.Select(x => x.Value));
            Assert.AreEqual(1234.5, loaded.SavedUptimeSeconds);

            var f1 = s.PrepareNextFight();
            var f2 = s2.PrepareNextFight();
            Assert.AreEqual(f1.Enemy.Hp, f2.Enemy.Hp);
            Assert.AreEqual(f1.Result.Outcome, f2.Result.Outcome);
            Assert.AreEqual(f1.Result.Duration, f2.Result.Duration);
        }

        [Test]
        public void Load_RebuildsWornItemIndex()
        {
            var (s, rng) = PlayedSession(1800);
            var loaded = new SaveSerializer().Deserialize(new SaveSerializer().Serialize(Snapshot(s, rng)));
            var s2 = new GameSession(TestConfig.Load(), loaded.State, Pcg32.FromState(loaded.RngState, loaded.RngIncrement), new EventBus());
            CollectionAssert.AreEquivalent(s.State.Character.Equipped.Values, s2.State.Inventory.WornIds);
            Assert.AreEqual(0, s2.EquipUpgrades(), "nothing should look unequipped after loading");
        }

        [Test]
        public void Output_UsesStringEnums()
        {
            var (s, rng) = PlayedSession(600);
            string json = SaveTestUtil.EnvelopeJson(new SaveSerializer().Serialize(Snapshot(s, rng)));
            StringAssert.Contains("\"Fighter\"", json);
            StringAssert.Contains("\"MainHand\"", json);
            StringAssert.Contains("\"Common\"", json);
        }

        [Test]
        public void Garbage_IsNotASave()
        {
            var ex = Assert.Throws<SaveFormatException>(() => new SaveSerializer().Deserialize(Encoding.UTF8.GetBytes("hello")));
            Assert.AreEqual(SaveError.NotASave, ex.Error);
        }

        [Test]
        public void Truncated_IsCorrupt()
        {
            var (s, rng) = PlayedSession(60);
            var bytes = new SaveSerializer().Serialize(Snapshot(s, rng));
            var cut = bytes.Take(bytes.Length / 2).ToArray();
            var ex = Assert.Throws<SaveFormatException>(() => new SaveSerializer().Deserialize(cut));
            Assert.AreEqual(SaveError.Corrupt, ex.Error);
        }

        [Test]
        public void TamperedPayload_ChecksumMismatch()
        {
            var (s, rng) = PlayedSession(60);
            var bytes = new SaveSerializer().Serialize(Snapshot(s, rng));
            var env = JObject.Parse(SaveTestUtil.EnvelopeJson(bytes));
            env["payload"]["wallet"]["gold"] = 1e12;
            var ex = Assert.Throws<SaveFormatException>(() => new SaveSerializer().Deserialize(SaveTestUtil.Rewrap(bytes, env)));
            Assert.AreEqual(SaveError.ChecksumMismatch, ex.Error);
        }

        [Test]
        public void NewerSchema_Refused()
        {
            var (s, rng) = PlayedSession(60);
            var newer = new SaveSerializer(5, new IMigration[0]).Serialize(Snapshot(s, rng));
            var ex = Assert.Throws<SaveFormatException>(() => new SaveSerializer().Deserialize(newer));
            Assert.AreEqual(SaveError.NewerSchema, ex.Error);
        }

        private sealed class AppendMigration : IMigration
        {
            private readonly string _mark;
            public AppendMigration(int from, string mark) { From = from; _mark = mark; }
            public int From { get; }
            public JObject Apply(JObject payload)
            {
                payload["trail"] = (string)payload["trail"] + _mark;
                return payload;
            }
        }

        [Test]
        public void Migrations_AppliedInOrder()
        {
            var serializer = new SaveSerializer(3, new IMigration[] { new AppendMigration(2, "b"), new AppendMigration(1, "a") });
            var migrated = serializer.Migrate(new JObject { ["trail"] = "" }, 1);
            Assert.AreEqual("ab", (string)migrated["trail"]);
        }

        [Test]
        public void MissingMigration_IsCorrupt()
        {
            var serializer = new SaveSerializer(3, new IMigration[] { new AppendMigration(1, "a") });
            var ex = Assert.Throws<SaveFormatException>(() => serializer.Migrate(new JObject(), 1));
            Assert.AreEqual(SaveError.Corrupt, ex.Error);
        }
    }

    /// <summary>Opens the container format for tests: magic + codec byte + gzip(JSON envelope).</summary>
    internal static class SaveTestUtil
    {
        public static string EnvelopeJson(byte[] bytes)
        {
            using (var gz = new GZipStream(new MemoryStream(bytes, 5, bytes.Length - 5), CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
                return r.ReadToEnd();
        }

        public static byte[] Rewrap(byte[] original, JObject envelope)
        {
            var ms = new MemoryStream();
            ms.Write(original, 0, 5);
            using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
            {
                var data = Encoding.UTF8.GetBytes(envelope.ToString(Newtonsoft.Json.Formatting.None));
                gz.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}
