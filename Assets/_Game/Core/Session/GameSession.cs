using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Offline;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Session
{
    /// <summary>
    /// Runs the idle loop: choose an encounter, resolve the fight, apply rewards and world rules.
    /// Two-phase so the view can replay a fight before its results land
    /// (<see cref="PrepareNextFight"/> then <see cref="Apply"/>); <see cref="Advance"/> does both
    /// for headless play (tests, and offline progress in M4).
    /// </summary>
    /// <summary>Result of asking to switch heroes (M5-3, M5-4).</summary>
    public enum SwitchResult
    {
        Switched,
        AlreadyActive,
        Locked,
        BossFight,
    }

    public sealed partial class GameSession
    {
        private readonly BalanceConfig _c;
        private readonly IRng _rng;
        private readonly GameFormulas _f;
        private readonly CombatEngine _engine;
        private readonly StatCalculator _stats;
        private readonly CharacterStatsBuilder _builder;
        private readonly EnemyFactory _enemies;
        private readonly LootSystem _loot;
        private readonly PowerScore _power;

        /// <summary>True from preparing a boss or training-boss fight until it is applied or dropped (M5-3).</summary>
        public bool InBossFight { get; private set; }

        /// <summary>Set while simulating time away: no events, no fight logs, drops held for Collect.</summary>
        private OfflineReport _offline;

        private static readonly EquipSlot[] ActiveSlots =
        {
            EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Helmet, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Boots,
            EquipSlot.Armguards, EquipSlot.Gloves, EquipSlot.Ring1, EquipSlot.Ring2, EquipSlot.Amulet,
        };

        public GameState State { get; }
        public EventBus Events { get; }
        public ProgressionSystem Progression { get; }
        public WorldSystem World { get; }
        public InventorySystem Inventory { get; }
        public UpgradeSystem Upgrades { get; }
        public ItemStats Items { get; }

        public GameSession(BalanceConfig config, GameState state, IRng rng, EventBus events)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            _f = new GameFormulas(config);
            _engine = new CombatEngine(config);
            _stats = new StatCalculator(config);
            _builder = new CharacterStatsBuilder(config);
            _enemies = new EnemyFactory(config);
            _loot = new LootSystem(config);
            _power = new PowerScore(config);
            Progression = new ProgressionSystem(config);
            World = new WorldSystem(config);
            Inventory = new InventorySystem(config);
            Upgrades = new UpgradeSystem(config);
            Items = new ItemStats(config);
            EnsureStarterKit();
            EnsureHeroKits();
            InitSkills();
            if (_c.Onboarding.TrainingBossEnabled && !State.Account.TrainingBossDone
                && State.World.Region == 1 && State.World.UnlockedSubzone <= _c.Onboarding.TrainingBossAfterSubzone)
                State.World.TrainingBossPending = true;
        }

        /// <summary>Each run starts with a Common level-1 class weapon, equipped (D-015).</summary>
        private void EnsureStarterKit()
        {
            if (State.Inventory == null) State.Inventory = InventoryState.Create(_c);
            // The worn-item index is derived data: rebuild it (e.g. after loading a save).
            State.Inventory.WornIds.Clear();
            foreach (var ch in State.Characters)
                foreach (var id in ch.Equipped.Values) State.Inventory.WornIds.Add(id);
            GiveStarterWeapon(State.Heroes[(int)ClassId.Fighter]);
        }

        /// <summary>Once the heroes are unlocked (M5-4), the Archer and Mage get their starter weapons if unarmed.</summary>
        public void EnsureHeroKits()
        {
            if (!State.Account.HeroesUnlocked) return;
            GiveStarterWeapon(State.Heroes[(int)ClassId.Archer]);
            GiveStarterWeapon(State.Heroes[(int)ClassId.Mage]);
        }

        private void GiveStarterWeapon(CharacterState hero)
        {
            if (hero.Equipped.ContainsKey(EquipSlot.MainHand)) return;
            // Added straight to the bag and worn at once: a worn item takes no bag space, so a full bag
            // must not salvage it (which would also pay materials again on every launch).
            var starter = new ItemInstance { Kind = ItemKind.MainHand, TypeId = _loot.StarterWeaponType(hero.Class), ItemLevel = 1, Rarity = Rarity.Common };
            starter.Id = State.Inventory.NextId++;
            State.Inventory.Items.Add(starter);
            Inventory.Equip(State.Inventory, State.Characters, hero, starter.Id, EquipSlot.MainHand, force: true);
        }

        /// <summary>
        /// Makes another hero active (M5-3): refused while a boss fight is prepared or running, and for the
        /// Archer and Mage before the unlock (M5-4). The caller drops the normal fight it was showing.
        /// </summary>
        public SwitchResult SwitchTo(ClassId hero)
        {
            if ((int)hero == State.ActiveHero) return SwitchResult.AlreadyActive;
            if (hero != ClassId.Fighter && !State.Account.HeroesUnlocked) return SwitchResult.Locked;
            if (InBossFight) return SwitchResult.BossFight;
            State.ActiveHero = (int)hero;
            Publish(new HeroSwitched(hero));
            return SwitchResult.Switched;
        }

        /// <summary>The prepared fight will never be applied (switch, offline catch-up): clears the boss lock.</summary>
        public void DropPreparedFight() => InBossFight = false;

        public IEnumerable<ItemInstance> EquippedItems(CharacterState c)
        {
            foreach (var id in c.Equipped.Values)
            {
                var item = State.Inventory.Find(id);
                if (item != null) yield return item;
            }
        }

        public StatInputs PlayerInputs() => _builder.Build(State.Character, EquippedItems(State.Character));

        /// <summary>Power score of the active character with an optional item swapped into a slot.</summary>
        public double Score(EquipSlot? slot = null, ItemInstance candidate = null)
        {
            var c = State.Character;
            var items = new List<ItemInstance>();
            foreach (var kv in c.Equipped)
                if (!(slot.HasValue && kv.Key == slot.Value)) items.Add(State.Inventory.Find(kv.Value));
            if (candidate != null) items.Add(candidate);
            return _power.Of(_stats.Compute(_builder.Build(c, items)), c.Level);
        }

        /// <summary>True when the item would raise the power score in some slot it fits (and its level allows it).</summary>
        public bool IsUpgrade(ItemInstance item)
        {
            var c = State.Character;
            if (c.Level < item.ItemLevel - _c.Combat.ItemLevelRequirementMargin) return false;
            double now = Score();
            foreach (var slot in ActiveSlots)
                if (ItemKeys.KindForSlot(slot) == item.Kind && Score(slot, item) > now * (1 + 1e-9)) return true;
            return false;
        }

        /// <summary>
        /// Player-triggered "Equip upgrades" (M3-5): repeatedly equips the unworn item that raises the
        /// power score most, until nothing improves. Never called automatically (R2-18). Returns items equipped.
        /// </summary>
        public int EquipUpgrades()
        {
            var c = State.Character;
            int equipped = 0;
            for (int guard = 0; guard < 64; guard++)
            {
                double now = Score();
                ItemInstance best = null;
                EquipSlot bestSlot = default;
                double bestScore = now * (1 + 1e-9);
                foreach (var item in State.Inventory.Items)
                {
                    if (State.Inventory.WornIds.Contains(item.Id)) continue;
                    if (c.Level < item.ItemLevel - _c.Combat.ItemLevelRequirementMargin) continue;
                    foreach (var slot in ActiveSlots)
                    {
                        if (ItemKeys.KindForSlot(slot) != item.Kind) continue;
                        double s = Score(slot, item);
                        if (s > bestScore)
                        {
                            bestScore = s;
                            best = item;
                            bestSlot = slot;
                        }
                    }
                }
                if (best == null) break;
                Inventory.Equip(State.Inventory, State.Characters, c, best.Id, bestSlot);
                equipped++;
            }
            return equipped;
        }

        /// <summary>Pause between a fight ending and the next enemy appearing (R2-1).</summary>
        public double RespawnSeconds => _c.Combat.EnemyRespawnS;

        /// <summary>Current combat stats of the active character.</summary>
        public CombatStats PlayerStats() => _stats.Compute(PlayerInputs());

        /// <summary>Resolves the next fight without changing the game state (only the RNG advances).</summary>
        public PreparedFight PrepareNextFight()
        {
            var encounter = World.NextEncounter(State.World, State.GameTime);
            InBossFight = encounter.IsBoss;
            var enemy = _enemies.Create(encounter, _rng);
            var player = PlayerStats();
            var kit = SkillKit();
            var before = CooldownsFor(State.Character, kit);
            var after = (double[])before.Clone();
            return new PreparedFight
            {
                Encounter = encounter,
                Enemy = enemy,
                Player = player,
                Kit = kit,
                CooldownsBefore = before,
                CooldownsAfter = after,
                Result = _engine.Resolve(player, enemy, _rng, _offline == null, kit, after),
            };
        }

        /// <summary>Applies a prepared fight: game time, rewards, levels and world progression.</summary>
        public void Apply(PreparedFight fight)
        {
            if (fight == null) throw new ArgumentNullException(nameof(fight));
            InBossFight = false;
            State.GameTime += fight.Result.Duration + _c.Combat.EnemyRespawnS;
            CommitCooldowns(fight);

            if (fight.Result.Outcome == FightOutcome.Won)
            {
                var e = fight.Encounter;
                double xp = _f.EnemyXp(e.Level, State.Character.Level, e.IsBoss);
                var inputs = PlayerInputs();
                double gold = _f.GoldDrop(e.Level, e.IsBoss) * (1 + inputs.GoldFind);
                State.Gold += gold;
                bool firstBossKill = e.IsTraining ? !State.Account.TrainingBossDone
                    : e.IsBoss && e.Region > State.World.HighestBossDefeated;
                int before = State.Character.Level;
                GrantXp(xp);
                Publish(new EnemyKilled(e, xp, gold));
                for (int lvl = before + 1; lvl <= State.Character.Level; lvl++)
                    Publish(new LevelUp(lvl));
                RollDrops(e, inputs, firstBossKill, fight.Enemy?.ElementalLineMult ?? 1.0);
            }

            if (_offline != null)
            {
                bool won = fight.Result.Outcome == FightOutcome.Won;
                if (fight.Encounter.IsBoss) { if (won) _offline.BossesDefeated++; else _offline.BossesFailed++; }
                else if (won) _offline.Kills++;
            }

            var change = World.OnFightFinished(State.World, fight.Encounter, fight.Result.Outcome, State.GameTime);
            if (change == WorldChange.TrainingBossDefeated) State.Account.TrainingBossDone = true;
            if (change == WorldChange.BossDefeated && fight.Encounter.Region == 1 && !State.Account.HeroesUnlocked)
            {
                State.Account.HeroesUnlocked = true; // M5-4: the Archer and Mage join after the recruit duel
                EnsureHeroKits();
                Publish(new HeroesUnlocked());
            }
            Publish(new FightResolved(fight));
            if (change != WorldChange.None)
                Publish(new WorldChanged(change, State.World));
        }

        /// <summary>
        /// Normal kills: 8% × (1 + drop chance bonus), excess over 100% becomes extra drops (R2-16).
        /// Bosses: always one item; Rare+ guaranteed on the first kill of that boss in this run (M3-2).
        /// </summary>
        private void RollDrops(Encounter e, StatInputs inputs, bool firstBossKill, double elementalLineMult)
        {
            int count;
            var minRarity = Rarity.Common;
            if (e.IsBoss)
            {
                count = 1;
                if (firstBossKill) minRarity = (Rarity)_c.Gear.BossMinRarity;
            }
            else
            {
                double chance = _c.Gear.DropChanceNormal * (1 + inputs.DropChance);
                count = (int)Math.Floor(chance);
                if (_rng.Chance(chance - count)) count++;
            }
            for (int i = 0; i < count; i++)
            {
                var item = _loot.Roll(e.Level, _rng, minRarity, inputs.Luck, elementalLineMult);
                var report = _offline == null
                    ? Inventory.Add(State.Inventory, State.Wallet, item, IsUpgrade)
                    : Inventory.Hold(State.Inventory, State.PendingOfflineLoot, State.Wallet, item, IsUpgrade);
                if (_offline != null)
                {
                    if (report.Outcome == AddOutcome.AutoSalvaged) _offline.AutoSalvaged++;
                    else _offline.HeldItems++;
                }
                Publish(new ItemDropped(report.Item, e.IsBoss, report.Outcome, report.Materials));
            }
        }

        /// <summary>
        /// Adds XP to the active character; when levels are gained and "Auto-allocate on level-up" is on,
        /// the new manual points are spent by the preset (M4-2). Returns levels gained.
        /// </summary>
        public int GrantXp(double xp)
        {
            int highest = 0;
            foreach (var h in State.Heroes) highest = Math.Max(highest, h.Level);
            if (State.Character.Level < highest) xp *= 1 + _c.Player.CatchupXpBonus; // D-024
            int gained = Progression.AddXp(State.Character, xp);
            if (gained > 0 && State.Character.AutoAllocateOnLevelUp) Progression.AutoAllocate(State.Character);
            if (gained > 0 && State.Character.AutoSpendSkills && Skills != null && Skills.AutoSpend(State.Character) > 0)
                Publish(new SkillsChanged(State.Character.Class));
            return gained;
        }

        /// <summary>Plays fights headlessly until <paramref name="seconds"/> of game time are used. Returns fights played.</summary>
        public int Advance(double seconds)
        {
            double end = State.GameTime + Math.Max(0, seconds);
            int fights = 0;
            while (State.GameTime < end)
            {
                Apply(PrepareNextFight());
                fights++;
            }
            return fights;
        }

        private void Publish<T>(T e)
        {
            if (_offline == null) Events.Publish(e);
        }

        /// <summary>
        /// Plays <paramref name="creditedSeconds"/> of game time fight by fight, exactly like online play,
        /// but silently: no events, drops held in <see cref="GameState.PendingOfflineLoot"/> (after auto-salvage)
        /// and nothing ever equipped (R2-18). The caller passes time already reduced by the offline efficiency.
        /// </summary>
        public OfflineReport SimulateOffline(double creditedSeconds)
        {
            var report = new OfflineReport { CreditedSeconds = Math.Max(0, creditedSeconds) };
            double gold0 = State.Wallet.Gold, materials0 = State.Wallet.Materials;
            int level0 = State.Character.Level;
            _offline = report;
            try
            {
                Advance(report.CreditedSeconds);
            }
            finally
            {
                _offline = null;
            }
            report.Gold = State.Wallet.Gold - gold0;
            report.Materials = State.Wallet.Materials - materials0;
            report.LevelsGained = State.Character.Level - level0;
            return report;
        }

        /// <summary>Moves held offline loot into the bag; what does not fit is salvaged (full-bag rule).</summary>
        public CollectReport Collect()
        {
            int kept = 0, salvaged = 0;
            double materials = 0;
            foreach (var item in State.PendingOfflineLoot)
            {
                var r = Inventory.Store(State.Inventory, State.Wallet, item);
                if (r.Outcome == AddOutcome.Kept) kept++;
                else salvaged++;
                materials += r.Materials;
            }
            State.PendingOfflineLoot.Clear();
            return new CollectReport(kept, salvaged, materials);
        }
    }
}
