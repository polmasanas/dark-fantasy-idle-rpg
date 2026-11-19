using System;

namespace DarkFantasy.Core.Characters
{
    /// <summary>Character attributes (GDD section 5). New attributes are appended, never reordered.</summary>
    public enum AttributeId
    {
        Str,
        Dex,
        Int,
        Vit,
        Def,
        CritChance,
        CritDamage,
        Luck,
        Counter,
    }

    public static class AttributeIds
    {
        /// <summary>Config/save key for an attribute, e.g. CritChance → "crit_chance".</summary>
        public static string ToKey(AttributeId a)
        {
            switch (a)
            {
                case AttributeId.Str: return "str";
                case AttributeId.Dex: return "dex";
                case AttributeId.Int: return "int";
                case AttributeId.Vit: return "vit";
                case AttributeId.Def: return "def";
                case AttributeId.CritChance: return "crit_chance";
                case AttributeId.CritDamage: return "crit_damage";
                case AttributeId.Luck: return "luck";
                case AttributeId.Counter: return "counter";
                default: throw new ArgumentOutOfRangeException(nameof(a), a, null);
            }
        }

        public static bool TryParse(string key, out AttributeId attribute)
        {
            foreach (AttributeId a in Enum.GetValues(typeof(AttributeId)))
            {
                if (ToKey(a) == key)
                {
                    attribute = a;
                    return true;
                }
            }
            attribute = default;
            return false;
        }
    }
}
