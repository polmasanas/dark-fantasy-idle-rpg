using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Items
{
    public class InventoryTests
    {
        private static InventorySystem Inv() => new InventorySystem(TestConfig.Load());

        private static ItemInstance Item(ItemKind kind, string type, int ilvl = 1, Rarity rarity = Rarity.Common) =>
            new ItemInstance { Kind = kind, TypeId = type, ItemLevel = ilvl, Rarity = rarity };

        private static (InventoryState inv, Wallet wallet, CharacterState fighter, List<CharacterState> all) Setup()
        {
            var fighter = CharacterState.Create(ClassId.Fighter);
            fighter.Level = 20;
            return (InventoryState.Create(TestConfig.Load()), new Wallet(), fighter, new List<CharacterState> { fighter });
        }

        [Test]
        public void Add_AssignsUniqueIds()
        {
            var (inv, wallet, _, all) = Setup();
            var a = Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium"), _ => false);
            var b = Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium"), _ => false);
            Assert.AreEqual(AddOutcome.Kept, a.Outcome);
            Assert.AreNotEqual(a.Item.Id, b.Item.Id);
            Assert.AreEqual(2, inv.Items.Count);
        }

        [Test]
        public void Equip_PutsItemInSlot_AndRespectsLevelRequirement()
        {
            var (inv, wallet, fighter, all) = Setup();
            var ok = Inv().Add(inv, wallet, Item(ItemKind.Helmet, "heavy", 25), _ => false).Item;   // 25 - 5 = 20 <= 20
            var tooHigh = Inv().Add(inv, wallet, Item(ItemKind.Helmet, "heavy", 26), _ => false).Item;
            Assert.AreEqual(EquipResult.Ok, Inv().Equip(inv, all, fighter, ok.Id, EquipSlot.Helmet));
            Assert.AreEqual(ok.Id, fighter.Equipped[EquipSlot.Helmet]);
            Assert.AreEqual(EquipResult.LevelTooLow, Inv().Equip(inv, all, fighter, tooHigh.Id, EquipSlot.Helmet));
        }

        [Test]
        public void Equip_WrongSlot_Refused_RingsFitBothRingSlots()
        {
            var (inv, wallet, fighter, all) = Setup();
            var ring = Inv().Add(inv, wallet, Item(ItemKind.Ring, "band_of_power"), _ => false).Item;
            Assert.AreEqual(EquipResult.WrongSlot, Inv().Equip(inv, all, fighter, ring.Id, EquipSlot.Amulet));
            Assert.AreEqual(EquipResult.Ok, Inv().Equip(inv, all, fighter, ring.Id, EquipSlot.Ring2));
        }

        [Test]
        public void Equip_ItemWornByOther_MovesIt()
        {
            var (inv, wallet, fighter, all) = Setup();
            var archer = CharacterState.Create(ClassId.Archer);
            archer.Level = 20;
            all.Add(archer);
            var boots = Inv().Add(inv, wallet, Item(ItemKind.Boots, "light"), _ => false).Item;
            Inv().Equip(inv, all, fighter, boots.Id, EquipSlot.Boots);
            Assert.AreEqual(EquipResult.NeedsConfirmation, Inv().Equip(inv, all, archer, boots.Id, EquipSlot.Boots)); // D-004: ask first
            Assert.AreEqual(EquipResult.Ok, Inv().Equip(inv, all, archer, boots.Id, EquipSlot.Boots, force: true));
            Assert.IsFalse(fighter.Equipped.ContainsKey(EquipSlot.Boots));
            Assert.AreEqual(boots.Id, archer.Equipped[EquipSlot.Boots]);
        }

        [Test]
        public void Equip_SameItemIntoSecondRingSlot_LeavesFirst()
        {
            var (inv, wallet, fighter, all) = Setup();
            var ring = Inv().Add(inv, wallet, Item(ItemKind.Ring, "band_of_power"), _ => false).Item;
            Inv().Equip(inv, all, fighter, ring.Id, EquipSlot.Ring1);
            Inv().Equip(inv, all, fighter, ring.Id, EquipSlot.Ring2);
            Assert.IsFalse(fighter.Equipped.ContainsKey(EquipSlot.Ring1));
            Assert.AreEqual(ring.Id, fighter.Equipped[EquipSlot.Ring2]);
        }

        [Test]
        public void Salvage_GivesMaterialsAndHalfUpgradeGold()
        {
            var (inv, wallet, fighter, all) = Setup();
            var item = Inv().Add(inv, wallet, Item(ItemKind.Chest, "medium", 10, Rarity.Epic), _ => false).Item;
            item.GoldSpent = 300;
            Assert.IsTrue(Inv().Salvage(inv, all, wallet, item.Id));
            Assert.AreEqual(8, wallet.Materials, 1e-9); // Epic
            Assert.AreEqual(150, wallet.Gold, 1e-9);
            Assert.AreEqual(0, inv.Items.Count);
        }

        [Test]
        public void Salvage_EquippedOrLocked_Refused()
        {
            var (inv, wallet, fighter, all) = Setup();
            var worn = Inv().Add(inv, wallet, Item(ItemKind.Legs, "medium"), _ => false).Item;
            Inv().Equip(inv, all, fighter, worn.Id, EquipSlot.Legs);
            var locked = Inv().Add(inv, wallet, Item(ItemKind.Legs, "heavy"), _ => false).Item;
            Inv().SetLocked(inv, locked.Id, true);
            Assert.IsFalse(Inv().Salvage(inv, all, wallet, worn.Id));
            Assert.IsFalse(Inv().Salvage(inv, all, wallet, locked.Id));
            Assert.AreEqual(2, inv.Items.Count);
            Assert.AreEqual(0, Inv().SalvageWhere(inv, all, wallet, _ => true));
        }

        [Test]
        public void AutoSalvage_Modes()
        {
            var (inv, wallet, fighter, all) = Setup();
            inv.AutoSalvage = AutoSalvageMode.CommonAndUncommon;
            Assert.AreEqual(AddOutcome.AutoSalvaged, Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Uncommon), _ => false).Outcome);
            Assert.AreEqual(AddOutcome.Kept, Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Rare), _ => false).Outcome);
            inv.AutoSalvage = AutoSalvageMode.BelowRareNotUpgrade;
            Assert.AreEqual(AddOutcome.Kept, Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Uncommon), _ => true).Outcome, "upgrades are kept");
            Assert.AreEqual(AddOutcome.AutoSalvaged, Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Uncommon), _ => false).Outcome);
            inv.AutoSalvage = AutoSalvageMode.Off;
            Assert.AreEqual(AddOutcome.Kept, Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Common), _ => false).Outcome);
        }

        [Test]
        public void FullInventory_AutoSalvagesAndReports()
        {
            var (inv, wallet, fighter, all) = Setup();
            for (int i = 0; i < inv.Capacity; i++)
                Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium"), _ => false);
            var r = Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium", 1, Rarity.Legendary), _ => false);
            Assert.AreEqual(AddOutcome.SalvagedInventoryFull, r.Outcome);
            Assert.AreEqual(16, r.Materials, 1e-9);
            Assert.AreEqual(inv.Capacity, inv.Items.Count);
        }

        [Test]
        public void EquippedItems_DoNotUseInventorySpace()
        {
            var (inv, wallet, fighter, all) = Setup();
            var worn = Inv().Add(inv, wallet, Item(ItemKind.Boots, "medium"), _ => false).Item;
            Inv().Equip(inv, all, fighter, worn.Id, EquipSlot.Boots);
            Assert.AreEqual(0, Inv().UsedSpace(inv, all));
        }

        [Test]
        public void Expand_CostsGoldAndAddsFifty()
        {
            var (inv, wallet, fighter, all) = Setup();
            Assert.IsFalse(Inv().Expand(inv, wallet));
            wallet.Gold = 1000;
            Assert.IsTrue(Inv().Expand(inv, wallet));
            Assert.AreEqual(250, inv.Capacity);
            Assert.AreEqual(0, wallet.Gold, 1e-9);
            Assert.AreEqual(2000, Inv().ExpansionCost(inv), 1e-9);
        }

        [Test]
        public void Upgrade_CostsGold_TracksSpent()
        {
            var up = new UpgradeSystem(TestConfig.Load());
            var item = Item(ItemKind.MainHand, "sword", 10, Rarity.Common);
            var wallet = new Wallet { Gold = 1e9 };
            double cost = up.Cost(item);
            Assert.AreEqual(12 * System.Math.Pow(1.07, 9), cost, 1e-9);
            Assert.AreEqual(UpgradeResult.Ok, up.Upgrade(item, wallet));
            Assert.AreEqual(1, item.Upgrade);
            Assert.AreEqual(cost, item.GoldSpent, 1e-9);
        }

        [Test]
        public void Upgrade_AtMaxOrPoor_Refused()
        {
            var up = new UpgradeSystem(TestConfig.Load());
            var item = Item(ItemKind.MainHand, "sword", 10, Rarity.Common);
            Assert.AreEqual(UpgradeResult.NotEnoughGold, up.Upgrade(item, new Wallet { Gold = 1 }));
            item.Upgrade = 5; // Common max +5
            Assert.AreEqual(UpgradeResult.MaxLevel, up.Upgrade(item, new Wallet { Gold = 1e9 }));
            Assert.AreEqual(5, item.Upgrade);
        }
    }
}
