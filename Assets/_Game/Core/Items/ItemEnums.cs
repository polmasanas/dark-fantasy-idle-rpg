using System;

namespace DarkFantasy.Core.Items
{
    /// <summary>What kind of item it is (which slot it fits). Rings fit both ring slots.</summary>
    public enum ItemKind
    {
        MainHand,
        OffHand,
        Helmet,
        Chest,
        Legs,
        Boots,
        Armguards,
        Gloves,
        Ring,
        Amulet,
    }

    /// <summary>Equipment slots (GDD section 6). Accessories and Pet are locked until later milestones (M3-1).</summary>
    public enum EquipSlot
    {
        MainHand,
        OffHand,
        Helmet,
        Chest,
        Legs,
        Boots,
        Armguards,
        Gloves,
        Ring1,
        Ring2,
        Amulet,
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythical,
    }

    /// <summary>Stats that item lines and random attributes can add (M3-3, M3-4).</summary>
    public enum GearStat
    {
        WeaponDamage,
        Armour,
        Hp,
        PctDamage,
        Primary,
        CritPoints,
        CritDamagePoints,
        AttackSpeed,
        Counter,
        PctHp,
        PctArmour,
        Vit,
        GoldFind,
        Luck,
        DropChance,
    }

    public enum StatLineScaling
    {
        /// <summary>base × item_growth^(ilvl−1) × rarity × upgrade</summary>
        Exponential,
        /// <summary>(base + per_item_level × ilvl) × rarity × upgrade</summary>
        Linear,
        /// <summary>base × rarity × upgrade</summary>
        Rarity,
        /// <summary>base, unaffected by level, rarity or upgrades</summary>
        Fixed,
    }

    public static class ItemKeys
    {
        public static string Kind(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.MainHand: return "main_hand";
                case ItemKind.OffHand: return "off_hand";
                case ItemKind.Helmet: return "helmet";
                case ItemKind.Chest: return "chest";
                case ItemKind.Legs: return "legs";
                case ItemKind.Boots: return "boots";
                case ItemKind.Armguards: return "armguards";
                case ItemKind.Gloves: return "gloves";
                case ItemKind.Ring: return "ring";
                case ItemKind.Amulet: return "amulet";
                default: throw new ArgumentOutOfRangeException(nameof(k), k, null);
            }
        }

        public static bool TryKind(string key, out ItemKind kind) => TryParse(key, Kind, out kind);

        public static string Stat(GearStat s)
        {
            switch (s)
            {
                case GearStat.WeaponDamage: return "weapon_damage";
                case GearStat.Armour: return "armour";
                case GearStat.Hp: return "hp";
                case GearStat.PctDamage: return "pct_damage";
                case GearStat.Primary: return "primary";
                case GearStat.CritPoints: return "crit_points";
                case GearStat.CritDamagePoints: return "crit_damage_points";
                case GearStat.AttackSpeed: return "attack_speed";
                case GearStat.Counter: return "counter";
                case GearStat.PctHp: return "pct_hp";
                case GearStat.PctArmour: return "pct_armour";
                case GearStat.Vit: return "vit";
                case GearStat.GoldFind: return "gold_find";
                case GearStat.Luck: return "luck";
                case GearStat.DropChance: return "drop_chance";
                default: throw new ArgumentOutOfRangeException(nameof(s), s, null);
            }
        }

        public static bool TryStat(string key, out GearStat stat) => TryParse(key, Stat, out stat);

        public static bool TryScaling(string key, out StatLineScaling scaling) =>
            TryParse(key, s => s.ToString().ToLowerInvariant(), out scaling);

        /// <summary>The kinds an equipment slot accepts.</summary>
        public static ItemKind KindForSlot(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Ring1:
                case EquipSlot.Ring2: return ItemKind.Ring;
                case EquipSlot.Amulet: return ItemKind.Amulet;
                default: return (ItemKind)(int)slot; // MainHand..Gloves share their order with ItemKind
            }
        }

        private static bool TryParse<T>(string key, Func<T, string> toKey, out T value) where T : struct, Enum
        {
            foreach (T v in Enum.GetValues(typeof(T)))
            {
                if (toKey(v) == key)
                {
                    value = v;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
