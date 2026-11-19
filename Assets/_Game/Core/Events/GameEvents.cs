using DarkFantasy.Core.Session;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Events
{
    /// <summary>A fight was resolved and applied to the game state.</summary>
    public sealed class FightResolved
    {
        public readonly PreparedFight Fight;
        public FightResolved(PreparedFight fight) { Fight = fight; }
    }

    public sealed class EnemyKilled
    {
        public readonly Encounter Encounter;
        public readonly double Xp;
        public readonly double Gold;
        public EnemyKilled(Encounter encounter, double xp, double gold) { Encounter = encounter; Xp = xp; Gold = gold; }
    }

    public sealed class LevelUp
    {
        public readonly int Level;
        public LevelUp(int level) { Level = level; }
    }

    public sealed class WorldChanged
    {
        public readonly WorldChange Change;
        public readonly WorldState World;
        public WorldChanged(WorldChange change, WorldState world) { Change = change; World = world; }
    }
}
