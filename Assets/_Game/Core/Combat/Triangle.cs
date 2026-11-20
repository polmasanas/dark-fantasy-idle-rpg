using System;
using System.Collections.Generic;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Combat
{
    /// <summary>How an enemy fights, mapped onto the class triangle (D-017). None = neutral.</summary>
    public enum Archetype
    {
        None,
        Brute,
        Marksman,
        Caster,
    }

    /// <summary>Class triangle multipliers (D-017) from balance_config.json "triangle".</summary>
    public sealed class TriangleTable
    {
        private readonly TriangleConfig _c;

        public TriangleTable(BalanceConfig config)
        {
            _c = (config ?? throw new ArgumentNullException(nameof(config))).Triangle;
        }

        /// <summary>Multiplier on damage the hero deals to an enemy of this archetype.</summary>
        public double DealtBy(ClassId hero, Archetype enemy)
        {
            switch (Relation(hero, enemy))
            {
                case 1: return _c.AdvantageDealt;
                case -1: return _c.DisadvantageDealt;
                default: return 1.0;
            }
        }

        /// <summary>Multiplier on damage the hero takes from an enemy of this archetype.</summary>
        public double TakenBy(ClassId hero, Archetype enemy)
        {
            switch (Relation(hero, enemy))
            {
                case 1: return _c.AdvantageTaken;
                case -1: return _c.DisadvantageTaken;
                default: return 1.0;
            }
        }

        /// <summary>+1 advantage, −1 disadvantage (the enemy fights like the class that beats this hero), 0 neutral.</summary>
        private int Relation(ClassId hero, Archetype enemy)
        {
            if (enemy == Archetype.None) return 0;
            if (_c.ClassBeats.TryGetValue(hero, out var beaten) && beaten == enemy) return 1;
            foreach (var kv in _c.ClassBeats)
                if (kv.Value == _c.ClassAs[hero] && _c.ClassAs.TryGetValue(kv.Key, out var asArch) && asArch == enemy)
                    return -1;
            return 0;
        }
    }
}
