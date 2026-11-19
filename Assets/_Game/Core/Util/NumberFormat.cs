using System;
using System.Globalization;

namespace DarkFantasy.Core.Util
{
    /// <summary>Compact, culture-independent number display: 950, 1.50K, 12.3K, 2.50M, 1.23e15.</summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "K", "M", "B", "T" };

        public static string Short(double value)
        {
            if (double.IsNaN(value)) return "-";
            if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
            var inv = CultureInfo.InvariantCulture;
            string sign = value < 0 ? "-" : "";
            double v = Math.Abs(value);
            if (v < 10) return sign + (v == Math.Floor(v) ? v.ToString("0", inv) : v.ToString("0.0", inv));
            if (v < 1000) return sign + Math.Floor(v + 0.5 > 999 ? 999 : v + 0.5).ToString("0", inv);
            if (v >= 1e15) return sign + v.ToString("0.00e0", inv);

            int tier = (int)Math.Floor(Math.Log10(v) / 3); // 1 = K ... 4 = T
            double scaled = v / Math.Pow(1000, tier);
            if (scaled >= 999.5 && tier < Suffixes.Length)
            {
                tier++; // 999 999 shows as 1.00M, not 1000K
                scaled = v / Math.Pow(1000, tier);
            }
            string digits = scaled < 10 ? scaled.ToString("0.00", inv)
                : scaled < 100 ? scaled.ToString("0.0", inv)
                : scaled.ToString("0", inv);
            return sign + digits + Suffixes[tier - 1];
        }
    }
}
