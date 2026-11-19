using DarkFantasy.Core.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Config
{
    public class BalanceConfigLoaderTests
    {
        [Test]
        public void Loader_ReadsRepoConfig()
        {
            var c = BalanceConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            Assert.AreEqual(1.10, c.Enemy.HpGrowth, 1e-12);
            Assert.AreEqual(3.0, c.Combat.BaseIntervalFighter, 1e-12);
            Assert.AreEqual(2.2, c.Combat.BaseIntervalArcher, 1e-12);
            Assert.AreEqual(1.0, c.Player.CritChanceCap, 1e-12);
            Assert.AreEqual(1.28, c.Prestige.Artifacts["might"].CostGrowth, 1e-12);
            Assert.AreEqual(5, c.World.SubzonesPerRegion);
            Assert.AreEqual(6, c.Gear.RarityBaseMult.Length);
            Assert.AreEqual(30.0, c.Combat.EscapeTimerS, 1e-12);
        }

        [Test]
        public void Loader_MissingKey_ThrowsNamedError()
        {
            var json = JObject.Parse(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            ((JObject)json["enemy"]).Remove("hp_growth");
            var ex = Assert.Throws<BalanceConfigException>(() => BalanceConfigLoader.FromJson(json.ToString()));
            StringAssert.Contains("enemy.hp_growth", ex.Message);
        }

        [Test]
        public void Loader_InvalidJson_ThrowsBalanceConfigException()
        {
            Assert.Throws<BalanceConfigException>(() => BalanceConfigLoader.FromJson("{ not json"));
        }
    }
}
