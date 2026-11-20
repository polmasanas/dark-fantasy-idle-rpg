using System.Collections.Generic;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Items
{
    /// <summary>One random attribute rolled onto an item.</summary>
    public readonly struct RolledAttribute
    {
        public readonly string AttributeId;
        public readonly GearStat Stat;
        public readonly double Value;

        public RolledAttribute(string attributeId, GearStat stat, double value)
        {
            AttributeId = attributeId;
            Stat = stat;
            Value = value;
        }
    }

    /// <summary>A concrete item (saved). Base stats come from its type, level, rarity and upgrade.</summary>
    public sealed class ItemInstance
    {
        /// <summary>Unique within the save; assigned when the item enters the inventory.</summary>
        public long Id;
        public ItemKind Kind;
        public string TypeId;
        public int ItemLevel;
        public Rarity Rarity;
        public int Upgrade;
        public List<RolledAttribute> Attributes = new List<RolledAttribute>();

        /// <summary>Class whose bonus line is active (R2-6), or null for a generic item.</summary>
        public ClassId? ClassBonus;

        public bool Locked;

        /// <summary>Gold spent on upgrades; half is refunded on salvage.</summary>
        public double GoldSpent;
    }
}
