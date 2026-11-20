using System;
using System.Collections.Generic;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Random;

namespace DarkFantasy.Core.World
{
    /// <summary>
    /// Builds enemies from the formulas (GAME_BALANCE section 3) and their family (M5-2): element,
    /// archetype, evasion, resistances and the elemental loot multiplier.
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
            var family = FamilyFor(e);
            if (e.IsTraining)
            {
                return WithFamily(new EnemySnapshot
                {
                    Hp = _f.EnemyHp(e.Level, false) * _c.Onboarding.TrainingBossHpMult * variance,
                    Attack = _f.EnemyAtk(e.Level, false),
                    AttackInterval = _c.Enemy.AttackIntervalS,
                    Level = e.Level,
                    IsBoss = true,
                    CanKillPlayer = false,
                }, family, true, e.Subzone, rng);
            }
            double hp = _f.EnemyHp(e.Level, e.IsBoss);
            if (e.IsBoss && _c.Onboarding.BossHpMultOverrides.TryGetValue(e.Region, out var mult))
                hp = hp / _c.Enemy.BossHpMult * mult; // per-region boss strength (ON-1)
            return WithFamily(new EnemySnapshot
            {
                Hp = hp * variance,
                Attack = _f.EnemyAtk(e.Level, e.IsBoss),
                AttackInterval = _c.Enemy.AttackIntervalS,
                Level = e.Level,
                Evasion = 0,
                IsBoss = e.IsBoss,
            }, family, e.IsBoss, e.Subzone, rng);
        }

        /// <summary>The family living at this encounter (M5-5: per subzone; bosses use the region's boss family).</summary>
        public FamilyConfig FamilyFor(Encounter e)
        {
            var r = _c.Regions;
            int i = e.Region - 1;
            string id;
            if (e.IsBoss && !e.IsTraining)
                id = i >= 0 && i < r.BossFamilies.Count ? r.BossFamilies[i] : r.FallbackFamily;
            else if (i >= 0 && i < r.SubzoneFamilies.Count)
            {
                var row = r.SubzoneFamilies[i];
                id = row[Math.Min(Math.Max(1, e.Subzone), row.Length) - 1];
            }
            else
                id = r.FallbackFamily;
            return _c.Families[id];
        }

        /// <summary>
        /// Applies the family: archetype, evasion, element (fixed, or rolled from the pool: only then is
        /// extra randomness used, so other regions replay exactly), resistances and the loot multiplier.
        /// </summary>
        private EnemySnapshot WithFamily(EnemySnapshot s, FamilyConfig f, bool boss, int subzone, IRng rng)
        {
            s.Family = f.Id;
            s.Archetype = boss ? f.BossArchetype : f.Archetypes[(Math.Max(1, subzone) - 1) % f.Archetypes.Length];
            s.Evasion += f.Evasion;
            s.ElementalLineMult = f.ElementalLineMult;
            if (f.Element.HasValue) s.Element = f.Element.Value;
            else if (f.ElementPool.Count > 0) s.Element = Roll(f.ElementPool, rng);
            if (s.Element != Element.Physical) s.Resist[(int)s.Element] += f.ResistOwn;
            foreach (var kv in f.Resist) s.Resist[(int)kv.Key] += kv.Value;
            return s;
        }

        private static Element Roll(Dictionary<Element, double> pool, IRng rng)
        {
            double total = 0;
            foreach (var kv in pool) total += kv.Value;
            double x = rng.NextDouble() * total;
            Element last = Element.Physical;
            foreach (Element e in Enum.GetValues(typeof(Element)))
            {
                if (!pool.TryGetValue(e, out var w) || w <= 0) continue;
                last = e;
                if (x < w) return e;
                x -= w;
            }
            return last;
        }
    }
}
