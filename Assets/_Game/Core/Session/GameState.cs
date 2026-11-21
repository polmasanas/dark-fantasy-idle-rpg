using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Session
{
    /// <summary>Account-wide progress that survives prestige (grows in M7). </summary>
    public sealed class AccountState
    {
        /// <summary>The first-run training boss has been beaten (ON-1).</summary>
        public bool TrainingBossDone;

        /// <summary>The Archer and Mage are playable (M5-4: first region 1 boss kill). Survives prestige.</summary>
        public bool HeroesUnlocked;
    }

    /// <summary>Everything that changes during play (saved from M4).</summary>
    public sealed class GameState
    {
        /// <summary>The three heroes, indexed by (int)ClassId (M5).</summary>
        public List<CharacterState> Heroes = new List<CharacterState>();

        /// <summary>Index into <see cref="Heroes"/> of the hero who fights (only they earn XP, D-024).</summary>
        public int ActiveHero;

        /// <summary>The active hero. Setting it replaces the hero in its slot (used when loading).</summary>
        public CharacterState Character
        {
            get => Heroes[ActiveHero];
            set => Heroes[ActiveHero] = value;
        }
        public WorldState World;
        public Wallet Wallet = new Wallet();
        public AccountState Account = new AccountState();

        /// <summary>Created by <see cref="GameSession"/> with the configured capacity when null.</summary>
        public InventoryState Inventory;

        /// <summary>Offline loot waiting for "Collect" on the welcome-back report (M4-1).</summary>
        public List<ItemInstance> PendingOfflineLoot = new List<ItemInstance>();

        /// <summary>Simulated seconds since the run started; drives retreat timers.</summary>
        public double GameTime;

        public double Gold
        {
            get => Wallet.Gold;
            set => Wallet.Gold = value;
        }

        /// <summary>All heroes. Equipment rules work across this list (D-004).</summary>
        public IList<CharacterState> Characters => Heroes;

        /// <summary>A new run: all three heroes at level 1, <paramref name="active"/> fighting.</summary>
        public static GameState NewRun(ClassId active)
        {
            var s = new GameState { World = new WorldState() };
            foreach (ClassId cls in Enum.GetValues(typeof(ClassId))) s.Heroes.Add(CharacterState.Create(cls));
            s.ActiveHero = (int)active;
            return s;
        }
    }

    /// <summary>A fight that has been resolved but not yet applied (the view replays it first).</summary>
    public sealed class PreparedFight
    {
        public Encounter Encounter;
        public EnemySnapshot Enemy;
        public CombatStats Player;
        public FightResult Result;

        /// <summary>The hero's skills in this fight and their cooldowns before and after it (M6-2).</summary>
        public Skills.SkillKit Kit;
        public double[] CooldownsBefore;
        public double[] CooldownsAfter;
    }
}
