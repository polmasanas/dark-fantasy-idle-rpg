using DarkFantasy.Core.Util;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Util
{
    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(7.25, "7.3")]
        [TestCase(950, "950")]
        [TestCase(999.4, "999")]
        [TestCase(1500, "1.50K")]
        [TestCase(12345, "12.3K")]
        [TestCase(123456, "123K")]
        [TestCase(2.5e6, "2.50M")]
        [TestCase(7.1e9, "7.10B")]
        [TestCase(4.4e12, "4.40T")]
        [TestCase(1.23e15, "1.23e15")]
        [TestCase(-1500, "-1.50K")]
        [TestCase(999999, "1.00M")]
        public void Short_FormatsReadably(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }

        [Test]
        public void Short_HandlesNonFinite()
        {
            Assert.AreEqual("∞", NumberFormat.Short(double.PositiveInfinity));
            Assert.AreEqual("-", NumberFormat.Short(double.NaN));
        }
    }
}
