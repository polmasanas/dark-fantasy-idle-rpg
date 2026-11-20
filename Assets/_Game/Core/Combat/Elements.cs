using System;
using DarkFantasy.Core.Config;

namespace DarkFantasy.Core.Combat
{
    /// <summary>Damage elements (GDD 4.4). Physical = no element. The order is used as an array index.</summary>
    public enum Element
    {
        Physical,
        Fire,
        Nature,
        Water,
        Light,
        Dark,
    }

    public static class Elements
    {
        public static readonly int Count = Enum.GetValues(typeof(Element)).Length;

        public static bool TryParse(string key, out Element e)
        {
            foreach (Element v in Enum.GetValues(typeof(Element)))
            {
                if (string.Equals(v.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    e = v;
                    return true;
                }
            }
            e = Element.Physical;
            return false;
        }
    }

    /// <summary>The element matrix (D-019, D-035), built from balance_config.json "elements".</summary>
    public sealed class ElementTable
    {
        private readonly double[,] _m;

        public ElementTable(BalanceConfig config)
        {
            var c = (config ?? throw new ArgumentNullException(nameof(config))).Elements;
            int n = Elements.Count;
            _m = new double[n, n];
            for (int a = 0; a < n; a++)
                for (int t = 0; t < n; t++)
                    _m[a, t] = 1.0;
            foreach (var kv in c.Beats) _m[(int)kv.Key, (int)kv.Value] = c.StrongMult;
            foreach (var kv in c.Beats)
                if (!c.Beats.TryGetValue(kv.Value, out var back) || back != kv.Key)
                    _m[(int)kv.Value, (int)kv.Key] = c.WeakMult; // the beaten element hits back weakly (Fire > Nature ⇒ Nature → Fire weak)
            foreach (var e in c.ResistsSelf) _m[(int)e, (int)e] = c.WeakMult;
            foreach (var e in c.PhysicalWeakVs) _m[(int)Element.Physical, (int)e] = c.WeakMult;
        }

        public double Multiplier(Element attack, Element target) => _m[(int)attack, (int)target];
    }
}
