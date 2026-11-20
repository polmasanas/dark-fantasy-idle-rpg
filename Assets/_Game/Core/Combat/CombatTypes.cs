using System.Collections.Generic;

namespace DarkFantasy.Core.Combat
{
    /// <summary>The enemy side of a fight, derived from enemy definitions and formulas.</summary>
    public sealed class EnemySnapshot
    {
        public double Hp;
        public double Attack;
        public double AttackInterval;
        public int Level = 1;
        public double Evasion;
        public bool IsBoss;

        /// <summary>False for the training boss (ON-1): boss timer, but the player cannot die.</summary>
        public bool CanKillPlayer = true;
    }

    public enum FightOutcome
    {
        Won,
        /// <summary>Normal enemy not killed before the escape timer (R2-1c). No rewards.</summary>
        Escaped,
        /// <summary>Player HP reached 0. Only possible in boss fights (and dungeons, later).</summary>
        Died,
        /// <summary>Boss not killed within its time limit (R2-2).</summary>
        TimedOut,
    }

    public enum CombatEventType
    {
        PlayerHit,
        PlayerCrit,
        PlayerMiss,
        Counter,
        EnemyHit,
        EnemyDied,
        PlayerDied,
        Escaped,
        TimedOut,
    }

    /// <summary>One thing that happened in a fight. The view layer animates these; logic never needs them.</summary>
    public readonly struct CombatEvent
    {
        public readonly double Time;
        public readonly CombatEventType Type;
        public readonly double Amount;

        public CombatEvent(double time, CombatEventType type, double amount)
        {
            Time = time;
            Type = type;
            Amount = amount;
        }

        public override string ToString() => $"{Time:0.###}s {Type} {Amount:0.###}";
    }

    public sealed class FightResult
    {
        public FightOutcome Outcome;
        public double Duration;
        public double PlayerHpLeft;
        public IReadOnlyList<CombatEvent> Events;
    }
}
