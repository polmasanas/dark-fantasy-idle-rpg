namespace DarkFantasy.Core.Stats
{
    public enum ClassId
    {
        Fighter,
        Archer,
        Mage,
    }

    /// <summary>
    /// Everything that feeds a character's combat stats, already summed by source (attributes,
    /// gear, scrolls, artifacts). Gear and skill aggregation fill this in later milestones.
    /// Percentages are fractions: 0.5 = +50 %.
    /// </summary>
    public sealed class StatInputs
    {
        public ClassId Class;
        public int Level = 1;

        // Attributes (points)
        public double Primary;          // STR / DEX / INT depending on class
        public double Vit;
        public double Def;
        public double CritPoints;       // Critical Chance attribute + jewellery crit points
        public double CritDamagePoints; // Critical Damage attribute
        public double CounterPoints;    // Counter attribute (D-033)
        public double Accuracy = 100;   // base accuracy until accuracy gear exists (M3/M5)

        // Gear and other sources
        public double WeaponDamage;
        public double GearHp;
        public double GearArmour;
        public double PctDamage;
        public double PctHp;
        public double AttackSpeedPct;
        public double PctArmour;

        // Utility (used by the session for rewards, not by combat)
        public double GoldFind;
        public double Luck;
        public double DropChance;

        // Artifact levels
        public int MightLevel;
        public int BulwarkLevel;
        public int HasteLevel;
    }

    /// <summary>Final combat numbers used by the combat engine.</summary>
    public sealed class CombatStats
    {
        /// <summary>The hero's class, for the triangle (D-017).</summary>
        public ClassId Class;

        /// <summary>Share of each hit added as elemental damage, indexed by (int)Element (index 0 unused) (M5-1).</summary>
        public double[] ElementShare = new double[DarkFantasy.Core.Combat.Elements.Count];

        /// <summary>Bonus to every elemental component (Mage line, M5-1).</summary>
        public double PctElemental;

        public double HitDamage;
        public double MaxHp;
        public double Armour;
        public double AttackInterval;
        public double Accuracy;
        public double BaseHitChance;
        public double CritChance;
        public double CritDamage;
        public double CounterChance;
        public double CounterDamage;
    }
}
