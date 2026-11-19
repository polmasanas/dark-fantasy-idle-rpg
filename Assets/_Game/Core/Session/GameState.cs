using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Session
{
    /// <summary>Everything that changes during play (saved from M4).</summary>
    public sealed class GameState
    {
        public CharacterState Character;
        public WorldState World;
        public Wallet Wallet = new Wallet();

        /// <summary>Created by <see cref="GameSession"/> with the configured capacity when null.</summary>
        public InventoryState Inventory;

        /// <summary>Simulated seconds since the run started; drives retreat timers.</summary>
        public double GameTime;

        public double Gold
        {
            get => Wallet.Gold;
            set => Wallet.Gold = value;
        }

        /// <summary>All characters (M3: one). Equipment rules work across this list (D-004).</summary>
        public IList<CharacterState> Characters => new[] { Character };

        public static GameState NewRun(ClassId cls) => new GameState
        {
            Character = CharacterState.Create(cls),
            World = new WorldState(),
        };
    }

    /// <summary>A fight that has been resolved but not yet applied (the view replays it first).</summary>
    public sealed class PreparedFight
    {
        public Encounter Encounter;
        public EnemySnapshot Enemy;
        public CombatStats Player;
        public FightResult Result;
    }
}
