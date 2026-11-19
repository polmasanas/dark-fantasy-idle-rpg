using DarkFantasy.Core.Random;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Random
{
    public class Pcg32Tests
    {
        [Test]
        public void KnownVector_MatchesPublishedPcg32Demo()
        {
            // Reference output of the official pcg32-demo: pcg32_srandom_r(&rng, 42, 54)
            var rng = new Pcg32(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (var e in expected)
                Assert.AreEqual(e, rng.NextUInt());
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new Pcg32(12345);
            var b = new Pcg32(12345);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void FromState_ResumesSequence()
        {
            var a = new Pcg32(7);
            for (int i = 0; i < 50; i++) a.NextUInt();
            var b = Pcg32.FromState(a.State, a.Increment);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NextDouble_InUnitInterval()
        {
            var rng = new Pcg32(99);
            for (int i = 0; i < 100000; i++)
            {
                double d = rng.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void Chance_Frequency_WithinTolerance()
        {
            var rng = new Pcg32(2024);
            int hits = 0;
            const int n = 200000;
            for (int i = 0; i < n; i++) if (rng.Chance(0.25)) hits++;
            Assert.AreEqual(0.25, hits / (double)n, 0.005);
        }

        [Test]
        public void Range_StaysWithinBounds()
        {
            var rng = new Pcg32(5);
            for (int i = 0; i < 10000; i++)
            {
                double v = rng.Range(0.9, 1.1);
                Assert.That(v, Is.GreaterThanOrEqualTo(0.9).And.LessThan(1.1));
            }
        }
    }
}
