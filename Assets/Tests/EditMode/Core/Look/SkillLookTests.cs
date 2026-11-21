using DarkFantasy.Core.Look;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Look
{
    public class SkillLookTests
    {
        private static LookConfig Look() => LookConfigLoader.FromJson(TestPaths.ReadRepoFile(TestPaths.LookConfigPath));

        [Test]
        public void EverySkill_HasNameAndDescription()
        {
            var look = Look();
            foreach (var node in TestConfig.Load().Skills.Nodes)
            {
                Assert.IsTrue(look.Skills.ContainsKey(node.Id), node.Id + " has no entry in look_config.json skills");
                Assert.IsNotEmpty(look.Skills[node.Id].Name, node.Id);
                Assert.IsNotEmpty(look.Skills[node.Id].Description, node.Id);
            }
        }

        [Test]
        public void NoUnknownSkillIds()
        {
            var skills = TestConfig.Load().Skills;
            foreach (var id in Look().Skills.Keys)
                Assert.IsNotNull(skills.Find(id), "look_config.json skills." + id + " is not in skills_config.json");
        }
    }
}
