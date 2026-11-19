using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;

namespace DarkFantasy.Core.Items
{
    /// <summary>Run currencies (reset on prestige, D-023).</summary>
    public sealed class Wallet
    {
        public double Gold;
        public double Materials;
    }

    public enum AutoSalvageMode
    {
        Off,
        Common,
        CommonAndUncommon,
        BelowRareNotUpgrade,
    }

    /// <summary>All items the player owns, worn or not (saved). Worn items do not use inventory space.</summary>
    public sealed class InventoryState
    {
        public List<ItemInstance> Items = new List<ItemInstance>();
        public long NextId = 1;
        public int Capacity;
        public int Expansions;
        public AutoSalvageMode AutoSalvage = AutoSalvageMode.Off; // M3-5: off by default

        /// <summary>Ids worn by any character; kept in sync by <see cref="InventorySystem"/>.</summary>
        public HashSet<long> WornIds = new HashSet<long>();

        public static InventoryState Create(BalanceConfig config) =>
            new InventoryState { Capacity = config.Combat.InventoryBaseSlots };

        public ItemInstance Find(long id)
        {
            foreach (var i in Items) if (i.Id == id) return i;
            return null;
        }
    }

    public enum AddOutcome
    {
        Kept,
        AutoSalvaged,
        SalvagedInventoryFull,
    }

    public readonly struct AddReport
    {
        public readonly AddOutcome Outcome;
        public readonly ItemInstance Item;
        public readonly double Materials;

        public AddReport(AddOutcome outcome, ItemInstance item, double materials)
        {
            Outcome = outcome;
            Item = item;
            Materials = materials;
        }
    }

    public enum EquipResult
    {
        Ok,
        NotFound,
        WrongSlot,
        LevelTooLow,
    }

    /// <summary>
    /// Inventory rules (M3-5, D-004, D-025): adding with auto-salvage, equipping (one wearer, level
    /// requirement), locking, salvaging and expanding. Never equips anything on its own (R2-18).
    /// </summary>
    public sealed class InventorySystem
    {
        private readonly BalanceConfig _c;

        public InventorySystem(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int UsedSpace(InventoryState inv, IEnumerable<CharacterState> characters)
        {
            int worn = 0;
            foreach (var i in inv.Items) if (IsWorn(characters, i.Id)) worn++;
            return inv.Items.Count - worn;
        }

        /// <param name="isUpgrade">Used by the "below Rare and not an upgrade" auto-salvage mode.</param>
        public AddReport Add(InventoryState inv, Wallet wallet, ItemInstance item, Func<ItemInstance, bool> isUpgrade)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            item.Id = inv.NextId++;
            if (ShouldAutoSalvage(inv.AutoSalvage, item, isUpgrade))
                return new AddReport(AddOutcome.AutoSalvaged, item, Pay(wallet, item));
            if (inv.Items.Count - inv.WornIds.Count >= inv.Capacity)
                return new AddReport(AddOutcome.SalvagedInventoryFull, item, Pay(wallet, item));
            inv.Items.Add(item);
            return new AddReport(AddOutcome.Kept, item, 0);
        }

        private static bool ShouldAutoSalvage(AutoSalvageMode mode, ItemInstance item, Func<ItemInstance, bool> isUpgrade)
        {
            switch (mode)
            {
                case AutoSalvageMode.Common: return item.Rarity == Rarity.Common;
                case AutoSalvageMode.CommonAndUncommon: return item.Rarity <= Rarity.Uncommon;
                case AutoSalvageMode.BelowRareNotUpgrade: return item.Rarity < Rarity.Rare && !(isUpgrade?.Invoke(item) ?? false);
                default: return false;
            }
        }

        public EquipResult Equip(InventoryState inv, IList<CharacterState> characters, CharacterState wearer, long itemId, EquipSlot slot)
        {
            var item = inv.Find(itemId);
            if (item == null) return EquipResult.NotFound;
            if (ItemKeys.KindForSlot(slot) != item.Kind) return EquipResult.WrongSlot;
            if (wearer.Level < item.ItemLevel - _c.Combat.ItemLevelRequirementMargin) return EquipResult.LevelTooLow;

            // One wearer per item (D-004): take it off whoever wears it, in whichever slot.
            foreach (var c in characters)
            {
                var slots = new List<EquipSlot>();
                foreach (var kv in c.Equipped) if (kv.Value == itemId) slots.Add(kv.Key);
                foreach (var s in slots) c.Equipped.Remove(s);
            }
            if (wearer.Equipped.TryGetValue(slot, out var previous)) inv.WornIds.Remove(previous);
            wearer.Equipped[slot] = itemId;
            RebuildWorn(inv, characters);
            return EquipResult.Ok;
        }

        public void Unequip(InventoryState inv, IList<CharacterState> characters, CharacterState wearer, EquipSlot slot)
        {
            wearer.Equipped.Remove(slot);
            RebuildWorn(inv, characters);
        }

        public void SetLocked(InventoryState inv, long itemId, bool locked)
        {
            var item = inv.Find(itemId);
            if (item != null) item.Locked = locked;
        }

        /// <summary>Salvages one item; refused for worn or locked items.</summary>
        public bool Salvage(InventoryState inv, IEnumerable<CharacterState> characters, Wallet wallet, long itemId)
        {
            var item = inv.Find(itemId);
            if (item == null || item.Locked || IsWorn(characters, itemId)) return false;
            inv.Items.Remove(item);
            Pay(wallet, item);
            return true;
        }

        /// <summary>Salvages every unworn, unlocked item matching the filter. Returns how many.</summary>
        public int SalvageWhere(InventoryState inv, IEnumerable<CharacterState> characters, Wallet wallet, Func<ItemInstance, bool> filter)
        {
            var targets = new List<ItemInstance>();
            foreach (var i in inv.Items)
                if (!i.Locked && !IsWorn(characters, i.Id) && filter(i)) targets.Add(i);
            foreach (var i in targets)
            {
                inv.Items.Remove(i);
                Pay(wallet, i);
            }
            return targets.Count;
        }

        public double ExpansionCost(InventoryState inv) =>
            _c.Items.ExpansionCostBase * Math.Pow(_c.Items.ExpansionCostGrowth, inv.Expansions);

        public bool Expand(InventoryState inv, Wallet wallet)
        {
            double cost = ExpansionCost(inv);
            if (wallet.Gold < cost) return false;
            wallet.Gold -= cost;
            inv.Expansions++;
            inv.Capacity += _c.Combat.InventorySlotsPerExpansion;
            return true;
        }

        public double SalvageValue(ItemInstance item) => _c.Economy.MaterialPerSalvage[(int)item.Rarity];

        private double Pay(Wallet wallet, ItemInstance item)
        {
            double materials = SalvageValue(item);
            wallet.Materials += materials;
            wallet.Gold += item.GoldSpent * _c.Economy.SalvageUpgradeRefund;
            return materials;
        }

        private static bool IsWorn(IEnumerable<CharacterState> characters, long itemId)
        {
            foreach (var c in characters)
                foreach (var v in c.Equipped.Values)
                    if (v == itemId) return true;
            return false;
        }

        private static void RebuildWorn(InventoryState inv, IEnumerable<CharacterState> characters)
        {
            inv.WornIds.Clear();
            foreach (var c in characters)
                foreach (var v in c.Equipped.Values) inv.WornIds.Add(v);
        }
    }

    public enum UpgradeResult
    {
        Ok,
        MaxLevel,
        NotEnoughGold,
    }

    /// <summary>Gold upgrades: +5% base stats per level, max level by rarity (GAME_BALANCE 6.4).</summary>
    public sealed class UpgradeSystem
    {
        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public UpgradeSystem(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        public int MaxLevel(ItemInstance item) => _c.Gear.UpgradeMaxByRarity[(int)item.Rarity];

        public double Cost(ItemInstance item) => _f.UpgradeCost(item.ItemLevel, item.Upgrade);

        public UpgradeResult Upgrade(ItemInstance item, Wallet wallet)
        {
            if (item.Upgrade >= MaxLevel(item)) return UpgradeResult.MaxLevel;
            double cost = Cost(item);
            if (wallet.Gold < cost) return UpgradeResult.NotEnoughGold;
            wallet.Gold -= cost;
            item.GoldSpent += cost;
            item.Upgrade++;
            return UpgradeResult.Ok;
        }
    }
}
