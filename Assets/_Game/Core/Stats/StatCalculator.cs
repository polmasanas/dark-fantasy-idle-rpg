using System;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;

namespace DarkFantasy.Core.Stats
{
    /// <summary>
    /// Turns summed <see cref="StatInputs"/> into <see cref="CombatStats"/>
    /// (GAME_BALANCE section 4.2, decisions R2-1b and R2-21). Pure function of its inputs.
    /// </summary>
    public sealed class StatCalculator
    {
        /// <summary>Damage per hit with no weapon equipped (mirrors the Python simulation).</summary>
        public const double UnarmedDamage = 3.0;

        /// <summary>Attack speed below -90 % is treated as -90 % so the interval stays finite.</summary>
        private const double MinAttackSpeed = -0.9;

        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public StatCalculator(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        public double BaseInterval(ClassId cls)
        {
            switch (cls)
            {
                case ClassId.Fighter: return _c.Combat.BaseIntervalFighter;
                case ClassId.Archer: return _c.Combat.BaseIntervalArcher;
                case ClassId.Mage: return _c.Combat.BaseIntervalMage;
                default: throw new ArgumentOutOfRangeException(nameof(cls), cls, null);
            }
        }

        public CombatStats Compute(StatInputs i)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            var p = _c.Player;
            double baseInterval = BaseInterval(i.Class);

            double damageMult = (1 + i.Primary * p.DmgPerPrimaryPct)
                                * (1 + i.PctDamage)
                                * _f.ArtifactMult("might", i.MightLevel);
            double classScale = baseInterval / _c.Combat.ReferenceIntervalS;
            double hitDamage = Math.Max(UnarmedDamage, i.WeaponDamage) * damageMult * classScale;

            double attackSpeed = i.AttackSpeedPct + (_f.ArtifactMult(GameFormulas.HasteArtifactId, i.HasteLevel) - 1);
            attackSpeed = Math.Max(MinAttackSpeed, attackSpeed);
            double interval = Math.Max(_c.Combat.AttackIntervalFloorS, baseInterval / (1 + attackSpeed));

            return new CombatStats
            {
                HitDamage = hitDamage,
                MaxHp = (p.BaseHp + i.GearHp) * (1 + i.Vit * p.HpPerVitPct) * (1 + i.PctHp)
                        * _f.ArtifactMult("bulwark", i.BulwarkLevel),
                Armour = i.GearArmour * (1 + i.Def * p.ArmourPctPerDef) * (1 + i.PctArmour),
                AttackInterval = interval,
                Accuracy = i.Accuracy,
                BaseHitChance = p.BaseHitChance,
                CritChance = Math.Min(p.CritChanceCap, p.BaseCritChance + i.CritPoints * p.CritChancePerPoint),
                CritDamage = p.BaseCritDamage + i.CritPoints * p.CritDamagePerPoint * 0.5
                             + i.CritDamagePoints * p.CritDamagePerPoint,
                CounterChance = Math.Min(p.CounterCap, i.CounterPoints * p.CounterChancePerPoint),
                CounterDamage = p.CounterDamage,
            };
        }
    }
}
