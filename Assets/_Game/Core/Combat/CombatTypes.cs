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

        /// <summary>The enemy's element (none = Physical) and how it fights (D-017, D-019).</summary>
        public Element Element = Element.Physical;
        public Archetype Archetype = Archetype.None;

        /// <summary>Resistance per damage element, indexed by (int)Element; clamped by elements.resist_min/max.</summary>
        public double[] Resist = new double[Elements.Count];

        /// <summary>Family id from balance_config.json "families" (M5-2).</summary>
        public string Family = "";

        /// <summary>Weight multiplier for elemental gear lines on this enemy's drops (M5-6).</summary>
        public double ElementalLineMult = 1.0;
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

        /// <summary>The element of the largest damage component of a hit (for the damage-number colour).</summary>
        public readonly Element Element;

        public CombatEvent(double time, CombatEventType type, double amount, Element element = Element.Physical)
        {
            Time = time;
            Type = type;
            Amount = amount;
            Element = element;
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
