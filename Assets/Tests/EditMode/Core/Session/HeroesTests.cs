using System.Linq;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Session
{
    public class HeroesTests
    {
        private static GameSession NewSession(EventBus bus = null) =>
            new GameSession(TestConfig.Load(), GameState.NewRun(ClassId.Fighter), new Pcg32(4), bus ?? new EventBus());

        private static GameSession Unlocked()
        {
            var s = NewSession();
            s.State.Account.HeroesUnlocked = true;
            s.EnsureHeroKits();
            return s;
        }

        [Test]
        public void NewRun_ThreeHeroes_FighterActive_Locked()
        {
            var s = NewSession();
            Assert.AreEqual(3, s.State.Heroes.Count);
            CollectionAssert.AreEqual(new[] { ClassId.Fighter, ClassId.Archer, ClassId.Mage }, s.State.Heroes.Select(h => h.Class));
            Assert.AreEqual(ClassId.Fighter, s.State.Character.Class);
            Assert.IsFalse(s.State.Account.HeroesUnlocked);
            Assert.AreSame(s.State.Heroes[0], s.State.Character);
        }

        [Test]
        public void Switch_BeforeUnlock_Locked()
        {
            var s = NewSession();
            Assert.AreEqual(SwitchResult.Locked, s.SwitchTo(ClassId.Archer));
            Assert.AreEqual(ClassId.Fighter, s.State.Character.Class);
            Assert.AreEqual(SwitchResult.AlreadyActive, s.SwitchTo(ClassId.Fighter));
        }

        [Test]
        public void Region1BossKill_UnlocksHeroes_WithStarterWeapons()
        {
            var bus = new EventBus();
            int unlocked = 0;
            bus.Subscribe<HeroesUnlocked>(_ => unlocked++);
            var s = NewSession(bus);
            var boss = new Encounter(1, 5, 12, true);
            s.State.World.UnlockedSubzone = 5;
            s.Apply(new PreparedFight { Encounter = boss, Result = new DarkFantasy.Core.Combat.FightResult { Outcome = DarkFantasy.Core.Combat.FightOutcome.Won, Duration = 10 } });
            Assert.IsTrue(s.State.Account.HeroesUnlocked);
            Assert.AreEqual(1, unlocked);
            foreach (var cls in new[] { ClassId.Archer, ClassId.Mage })
            {
                var hero = s.State.Heroes[(int)cls];
                Assert.IsTrue(hero.Equipped.TryGetValue(EquipSlot.MainHand, out var id), cls.ToString());
                var weapon = s.State.Inventory.Find(id);
                Assert.AreEqual(cls == ClassId.Archer ? "bow" : "staff", weapon.TypeId);
                Assert.AreEqual(Rarity.Common, weapon.Rarity);
            }
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Mage));
        }

        [Test]
        public void Switch_DuringBossFight_Refused()
        {
            var s = Unlocked();
            s.State.World.TrainingBossPending = false;
            s.State.Account.TrainingBossDone = true;
            s.State.World.UnlockedSubzone = 5;
            s.State.World.FightBossWhenAvailable = true;
            s.State.World.Kills[4] = 1000;
            var fight = s.PrepareNextFight();
            Assert.IsTrue(fight.Encounter.IsBoss);
            Assert.IsTrue(s.InBossFight);
            Assert.AreEqual(SwitchResult.BossFight, s.SwitchTo(ClassId.Archer));
            Assert.AreEqual(ClassId.Fighter, s.State.Character.Class);
            s.Apply(fight);
            Assert.IsFalse(s.InBossFight);
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Archer));
        }

        [Test]
        public void Switch_DuringNormalFight_DropsIt()
        {
            var s = Unlocked();
            var fight = s.PrepareNextFight();
            Assert.IsFalse(fight.Encounter.IsBoss);
            int kills = s.State.World.KillsInSubzone(fight.Encounter.Subzone);
            Assert.AreEqual(SwitchResult.Switched, s.SwitchTo(ClassId.Archer));
            var next = s.PrepareNextFight();
            Assert.AreEqual(ClassId.Archer, next.Player.Class);
            Assert.AreEqual(kills, s.State.World.KillsInSubzone(fight.Encounter.Subzone), "the dropped fight gives nothing");
        }

        [Test]
        public void AbandonedBossFight_DoesNotLockSwitching()
        {
            var s = Unlocked();
            s.State.World.TrainingBossPending = true; // training boss is next
            s.State.World.Kills[0] = 1000;
            var fight = s.PrepareNextFight();
            Assert.IsTrue(fight.Encounter.IsBoss);
            s.DropPreparedFight();
            Assert.IsFalse(s.InBossFight);
        }

        [Test]
        public void OnlyActiveHeroEarnsXp_WithCatchUp()
        {
            var s = Unlocked();
            s.State.Heroes[0].Level = 10;
            s.SwitchTo(ClassId.Archer);
            var archer = s.State.Heroes[1];
            double xpBefore = archer.Xp;
            s.GrantXp(1.0);
            Assert.AreEqual(1.5, archer.Xp - xpBefore, 1e-9);
            Assert.AreEqual(0, s.State.Heroes[2].Xp);
            s.SwitchTo(ClassId.Fighter);
            double fx = s.State.Heroes[0].Xp;
            s.GrantXp(1.0);
            Assert.AreEqual(1.0, s.State.Heroes[0].Xp - fx, 1e-9, "the highest hero gets no bonus");
        }

        [Test]
        public void EquipUpgrades_NeverTakesOtherHeroesItems()
        {
            var s = Unlocked();
            var fighterSword = s.State.Heroes[0].Equipped[EquipSlot.MainHand];
            s.SwitchTo(ClassId.Archer);
            s.State.Heroes[1].Equipped.Remove(EquipSlot.MainHand); // the Archer is unarmed: the sword would be an upgrade
            s.Inventory.RebuildWornIndex(s.State.Inventory, s.State.Characters);
            s.EquipUpgrades();
            Assert.AreEqual(fighterSword, s.State.Heroes[0].Equipped[EquipSlot.MainHand]);
            Assert.IsFalse(s.State.Heroes[1].Equipped.ContainsValue(fighterSword));
        }

        [Test]
        public void Equip_ItemWornByOtherHero_RequiresConfirmation()
        {
            var s = Unlocked();
            var sword = s.State.Heroes[0].Equipped[EquipSlot.MainHand];
            var archer = s.State.Heroes[1];
            var r = s.Inventory.Equip(s.State.Inventory, s.State.Characters, archer, sword, EquipSlot.MainHand);
            Assert.AreEqual(EquipResult.NeedsConfirmation, r);
            Assert.AreEqual(sword, s.State.Heroes[0].Equipped[EquipSlot.MainHand]);
            r = s.Inventory.Equip(s.State.Inventory, s.State.Characters, archer, sword, EquipSlot.MainHand, force: true);
            Assert.AreEqual(EquipResult.Ok, r);
            Assert.IsFalse(s.State.Heroes[0].Equipped.ContainsKey(EquipSlot.MainHand));
            Assert.AreEqual(sword, archer.Equipped[EquipSlot.MainHand]);
        }

        [Test]
        public void SimulateOffline_UsesActiveHero()
        {
            var s = Unlocked();
            s.SwitchTo(ClassId.Mage);
            s.SimulateOffline(1800);
            Assert.That(s.State.Heroes[2].Level, Is.GreaterThan(1));
            Assert.AreEqual(1, s.State.Heroes[1].Level);
            Assert.AreEqual(1, s.State.Heroes[0].Level);
        }

        [Test]
        public void Unlock_IsAccountWide()
        {
            Assert.That(typeof(AccountState).GetField("HeroesUnlocked"), Is.Not.Null);
            Assert.That(typeof(WorldState).GetField("HeroesUnlocked"), Is.Null);
        }
    }
}
