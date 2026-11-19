using System;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Items
{
    /// <summary>
    /// A single number for "is this better?" (M3-5): expected damage per second × √(effective HP),
    /// computed from real combat stats. Advisory only: the UI always shows the stat differences too.
    /// </summary>
    public sealed class PowerScore
    {
        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public PowerScore(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        public double Of(CombatStats s, int characterLevel)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            double perHit = s.HitDamage * s.BaseHitChance * (1 + s.CritChance * (s.CritDamage - 1));
            double dps = perHit / s.AttackInterval;
            double counterShare = s.CounterChance * s.CounterDamage * s.AttackInterval / _c.Enemy.AttackIntervalS;
            dps *= 1 + counterShare;
            double k = _f.ArmourK(Math.Max(1, characterLevel));
            double dr = s.Armour > 0 ? Math.Min(_c.Player.DamageReductionCap, s.Armour / (s.Armour + k)) : 0;
            double effectiveHp = s.MaxHp / (1 - dr);
            return dps * Math.Sqrt(effectiveHp);
        }
    }
}
