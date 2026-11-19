using System.IO;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests
{
    /// <summary>Finds repository files from tests, both under dotnet test and inside Unity.</summary>
    public static class TestPaths
    {
        public static string RepoFile(string relativePath)
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("Repository file not found: " + relativePath);
        }

        public static string ReadRepoFile(string relativePath) => File.ReadAllText(RepoFile(relativePath));

        public const string ConfigPath = "Tools/BalanceSim/balance_config.json";
    }
}
