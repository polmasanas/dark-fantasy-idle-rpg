using DarkFantasy.Core.Config;

namespace DarkFantasy.Core.Tests
{
    /// <summary>The repository's balance_config.json with skills_config.json attached, loaded once for tests.</summary>
    public static class TestConfig
    {
        private static BalanceConfig _cached;

        public static BalanceConfig Load()
        {
            if (_cached != null) return _cached;
            var c = BalanceConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
            c.Skills = SkillsConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.SkillsConfigPath));
            return _cached = c;
        }

        /// <summary>A fresh copy without skills (M5 behaviour), for "no skills = identical" checks.</summary>
        public static BalanceConfig LoadWithoutSkills() =>
            BalanceConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.ConfigPath));
    }
}
