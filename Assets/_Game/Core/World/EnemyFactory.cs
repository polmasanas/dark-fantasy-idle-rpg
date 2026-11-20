using System;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Random;

namespace DarkFantasy.Core.World
{
    /// <summary>
    /// Builds enemies from the formulas (GAME_BALANCE section 3). Families, elements and
    /// archetypes are added with the world content in M5.
    /// </summary>
    public sealed class EnemyFactory
    {
        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public EnemyFactory(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        public EnemySnapshot Create(Encounter e, IRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            double variance = rng.Range(_c.Enemy.VarianceMultMin, _c.Enemy.VarianceMultMax);
            if (e.IsTraining)
            {
                return new EnemySnapshot
                {
                    Hp = _f.EnemyHp(e.Level, false) * _c.Onboarding.TrainingBossHpMult * variance,
                    Attack = _f.EnemyAtk(e.Level, false),
                    AttackInterval = _c.Enemy.AttackIntervalS,
                    Level = e.Level,
                    IsBoss = true,
                    CanKillPlayer = false,
                };
            }
            double hp = _f.EnemyHp(e.Level, e.IsBoss);
            if (e.IsBoss && _c.Onboarding.BossHpMultOverrides.TryGetValue(e.Region, out var mult))
                hp = hp / _c.Enemy.BossHpMult * mult; // per-region boss strength (ON-1)
            return new EnemySnapshot
            {
                Hp = hp * variance,
                Attack = _f.EnemyAtk(e.Level, e.IsBoss),
                AttackInterval = _c.Enemy.AttackIntervalS,
                Level = e.Level,
                Evasion = 0,
                IsBoss = e.IsBoss,
            };
        }
    }
}
