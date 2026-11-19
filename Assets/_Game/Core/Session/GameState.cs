using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Session
{
    /// <summary>Everything that changes during play (M2 subset; saved from M4).</summary>
    public sealed class GameState
    {
        public CharacterState Character;
        public WorldState World;
        public double Gold;

        /// <summary>Simulated seconds since the run started; drives retry and retreat timers.</summary>
        public double GameTime;

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
