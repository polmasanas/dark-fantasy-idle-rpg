using System;
using DarkFantasy.Core.Combat;
using System.Collections.Generic;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Items
{
    /// <summary>Values of an item's base stat lines (M3-4; mirrors Formulas.item_line in the Python reference).</summary>
    public sealed class ItemStats
    {
        private readonly BalanceConfig _c;

        public ItemStats(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ItemTypeConfig TypeOf(ItemInstance item)
        {
            foreach (var t in _c.Items.TypesFor(item.Kind))
                if (t.Id == item.TypeId) return t;
            throw new ArgumentException($"Unknown item type '{item.TypeId}' for {item.Kind}");
        }

        public double LineValue(StatLineConfig line, int itemLevel, Rarity rarity, int upgrade)
        {
            double rarityMult = _c.Gear.RarityBaseMult[(int)rarity];
            double upgradeMult = 1 + _c.Gear.UpgradeBasePct * upgrade;
            switch (line.Scaling)
            {
                case StatLineScaling.Exponential:
                    return line.Base * Math.Pow(_c.Gear.ItemGrowth, itemLevel - 1) * rarityMult * upgradeMult;
                case StatLineScaling.Linear:
                    return (line.Base + line.PerItemLevel * itemLevel) * rarityMult * upgradeMult;
                case StatLineScaling.Rarity:
                    return line.Base * rarityMult * upgradeMult;
                case StatLineScaling.Fixed:
                    return line.Base;
                default:
                    throw new ArgumentOutOfRangeException(nameof(line), line.Scaling, null);
            }
        }

        /// <summary>All stats an item contributes for a given wearer: base lines, random attributes and the class line.</summary>
        public IEnumerable<KeyValuePair<GearStat, double>> Contributions(ItemInstance item, ClassId wearer)
        {
            foreach (var line in TypeOf(item).Lines)
                yield return new KeyValuePair<GearStat, double>(line.Stat, LineValue(line, item.ItemLevel, item.Rarity, item.Upgrade));
            foreach (var a in item.Attributes)
                yield return new KeyValuePair<GearStat, double>(a.Stat, a.Value);
            if (item.ClassBonus.HasValue && item.ClassBonus.Value == wearer)
            {
                yield return new KeyValuePair<GearStat, double>(GearStat.PctDamage, _c.Items.ClassBonusPctDamage);
                if (wearer == ClassId.Mage) // M5-1: the Mage's class line also boosts elemental damage
                    yield return new KeyValuePair<GearStat, double>(GearStat.PctElemental, _c.Items.ClassBonusPctElemental);
            }
        }

        public string DisplayName(ItemInstance item)
        {
            string type = TypeOf(item).Name;
            bool armour = item.Kind >= ItemKind.Helmet && item.Kind <= ItemKind.Gloves;
            return armour ? type + " " + item.Kind : type;
        }
    }

    /// <summary>Adds equipped items to a character's <see cref="StatInputs"/>.</summary>
    public sealed class GearStats
    {
        private readonly ItemStats _items;

        public GearStats(BalanceConfig config)
        {
            _items = new ItemStats(config);
        }

        public void Apply(StatInputs inputs, IEnumerable<ItemInstance> equipped, ClassId wearer)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (equipped == null) return;
            foreach (var item in equipped)
            {
                if (item == null) continue;
                foreach (var kv in _items.Contributions(item, wearer))
                    Add(inputs, kv.Key, kv.Value);
            }
        }

        private static void Add(StatInputs i, GearStat stat, double v)
        {
            switch (stat)
            {
                case GearStat.WeaponDamage: i.WeaponDamage += v; break;
                case GearStat.Armour: i.GearArmour += v; break;
                case GearStat.Hp: i.GearHp += v; break;
                case GearStat.PctDamage: i.PctDamage += v; break;
                case GearStat.Primary: i.Primary += v; break;
                case GearStat.CritPoints: i.CritPoints += v; break;
                case GearStat.CritDamagePoints: i.CritDamagePoints += v; break;
                case GearStat.AttackSpeed: i.AttackSpeedPct += v; break;
                case GearStat.Counter: i.CounterPoints += v; break;
                case GearStat.PctHp: i.PctHp += v; break;
                case GearStat.PctArmour: i.PctArmour += v; break;
                case GearStat.Vit: i.Vit += v; break;
                case GearStat.GoldFind: i.GoldFind += v; break;
                case GearStat.Luck: i.Luck += v; break;
                case GearStat.DropChance: i.DropChance += v; break;
                case GearStat.AddsFire: i.ElementShare[(int)Element.Fire] += v; break;
                case GearStat.AddsNature: i.ElementShare[(int)Element.Nature] += v; break;
                case GearStat.AddsWater: i.ElementShare[(int)Element.Water] += v; break;
                case GearStat.AddsLight: i.ElementShare[(int)Element.Light] += v; break;
                case GearStat.AddsDark: i.ElementShare[(int)Element.Dark] += v; break;
                case GearStat.PctElemental: i.PctElemental += v; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }
}
