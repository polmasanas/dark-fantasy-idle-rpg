using DarkFantasy.Core.Config;

namespace DarkFantasy.Core.Tests
{
    /// <summary>The repository's balance_config.json, loaded once for tests.</summary>
    public static class TestConfig
    {
        private static BalanceConfig _cached;

        public static BalanceConfig Load() =>
            _cached ?? (_cached = BalanceConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.ConfigPath)));
    }
}
