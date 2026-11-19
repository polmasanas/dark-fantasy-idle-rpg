using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class SessionLootTests
    {
        private static GameSession NewSession(ulong seed, EventBus bus = null) =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(seed), bus ?? new EventBus());

        [Test]
        public void NewRun_StartsWithEquippedCommonSword()
        {
            var s = NewSession(1);
            var id = s.State.Character.Equipped[EquipSlot.MainHand];
            var sword = s.State.Inventory.Items.Single(i => i.Id == id);
            Assert.AreEqual("sword", sword.TypeId);
            Assert.AreEqual(Rarity.Common, sword.Rarity);
            Assert.AreEqual(1, sword.ItemLevel);
            Assert.AreEqual(10.0 * 3.0 / 1.6, s.PlayerStats().HitDamage, 1e-9);
        }

        [Test]
        public void Kills_DropItemsAtAboutEightPercent()
        {
            var bus = new EventBus();
            int drops = 0, kills = 0;
            bus.Subscribe<ItemDropped>(_ => drops++);
            bus.Subscribe<EnemyKilled>(e => { if (!e.Encounter.IsBoss) kills++; });
            var s = NewSession(2, bus);
            s.Advance(3 * 3600);
            Assert.That(kills, Is.GreaterThan(500));
            Assert.AreEqual(0.08, drops / (double)kills, 0.03);
        }

        [Test]
        public void Boss_FirstKillGuaranteesRare_SecondKillSameRunDoesNot()
        {
            var s = NewSession(3);
            var bus = s.Events;
            var drops = new System.Collections.Generic.List<ItemInstance>();
            bus.Subscribe<ItemDropped>(e => { if (e.FromBoss) drops.Add(e.Item); });
            var boss = new Encounter(1, 5, 10, true);
            var won = new FightResult { Outcome = FightOutcome.Won, Duration = 10, Events = new CombatEvent[0] };
            for (int i = 0; i < 300; i++)
            {
                s.State.World.Region = 1; // pretend we re-fight region 1's boss
                s.Apply(new PreparedFight { Encounter = boss, Result = won });
            }
            Assert.That(drops[0].Rarity, Is.GreaterThanOrEqualTo(Rarity.Rare));
            Assert.AreEqual(300, drops.Count, "every boss kill drops one item");
            Assert.That(drops.Skip(1).Count(d => d.Rarity < Rarity.Rare), Is.GreaterThan(150), "later kills use normal odds");
        }

        [Test]
        public void FreshFighter_BeatsRegion1Boss_WithRealLoot()
        {
            int bossWins = 0;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var bus = new EventBus();
                var s = NewSession(seed, bus);
                bus.Subscribe<ItemDropped>(e => s.EquipUpgrades());
                bus.Subscribe<WorldChanged>(e => { if (e.Change == WorldChange.BossDefeated) bossWins++; });
                s.Advance(4 * 3600);
            }
            Assert.That(bossWins, Is.GreaterThan(0), "a player who equips upgrades beats boss 1 within 4 h in at least one of 3 runs");
        }

        [Test]
        public void EquipUpgrades_OnlyEquipsBetterItems()
        {
            var s = NewSession(4);
            var inv = new InventorySystem(TestConfig.Load());
            var better = inv.Add(s.State.Inventory, s.State.Wallet,
                new ItemInstance { Kind = ItemKind.MainHand, TypeId = "sword", ItemLevel = 1, Rarity = Rarity.Legendary }, _ => false).Item;
            var worse = inv.Add(s.State.Inventory, s.State.Wallet,
                new ItemInstance { Kind = ItemKind.Chest, TypeId = "heavy", ItemLevel = 1, Rarity = Rarity.Common }, _ => false).Item;
            int equipped = s.EquipUpgrades();
            Assert.AreEqual(better.Id, s.State.Character.Equipped[EquipSlot.MainHand]);
            Assert.AreEqual(worse.Id, s.State.Character.Equipped[EquipSlot.Chest], "an empty slot filled by any item is an upgrade");
            Assert.AreEqual(2, equipped);
            Assert.AreEqual(0, s.EquipUpgrades(), "nothing left to improve");
        }
    }
}
