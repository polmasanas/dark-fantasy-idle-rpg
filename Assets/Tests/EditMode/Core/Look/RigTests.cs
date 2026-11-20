using DarkFantasy.Core.Look;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Look
{
    public class RigTests
    {
        private static RigLibrary Load() =>
            RigLibrary.FromJson(TestPaths.ReadRepoFile("Assets/_Game/Resources/Art/rigs.json"));

        [Test]
        public void Fighter_PartsInDrawOrder_WithPivots()
        {
            var rig = Load().Get("fighter");
            Assert.IsNotNull(rig);
            CollectionAssert.AreEqual(new[] { "cloak", "legs", "torso", "head", "weapon" }, System.Array.ConvertAll(rig.Parts, p => p.Id));
            var weapon = rig.Parts[4];
            Assert.IsTrue(weapon.Glow);
            Assert.That(weapon.PivotX, Is.InRange(0.0, 1.0));
            Assert.That(weapon.PivotY, Is.InRange(0.0, 1.0));
            Assert.That(rig.AuraPad, Is.GreaterThan(0));
            Assert.That(rig.Aspect, Is.GreaterThan(0));
        }

        [Test]
        public void UnknownBody_ReturnsNull()
        {
            Assert.IsNull(Load().Get("no_such_body"));
        }

        [Test]
        public void BadJson_ThrowsLookConfigException()
        {
            Assert.Throws<LookConfigException>(() => RigLibrary.FromJson("{ not json"));
            Assert.Throws<LookConfigException>(() => RigLibrary.FromJson("{\"x\": {\"aspect\": 1, \"aura_pad\": 0.1}}"));
        }
    }
}
