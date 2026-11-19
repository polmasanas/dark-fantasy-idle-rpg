using System;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Characters
{
    /// <summary>
    /// Collects a character's sources into <see cref="StatInputs"/>. M2: attributes and the
    /// Common starter weapon only; gear, artifacts and skills join in later milestones.
    /// </summary>
    public sealed class CharacterStatsBuilder
    {
        private readonly BalanceConfig _c;

        public CharacterStatsBuilder(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        public StatInputs Build(CharacterState c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            int Total(AttributeId a) => c.AutoPoints[a] + c.ManualPoints[a];
            var primary = _c.Classes[c.Class].Primary;
            return new StatInputs
            {
                Class = c.Class,
                Level = c.Level,
                Primary = Total(primary),
                Vit = Total(AttributeId.Vit),
                Def = Total(AttributeId.Def),
                CritPoints = Total(AttributeId.CritChance),
                CritDamagePoints = Total(AttributeId.CritDamage),
                CounterPoints = Total(AttributeId.Counter),
                WeaponDamage = _c.Gear.WeaponDmgBase * Math.Pow(_c.Gear.ItemGrowth, c.WeaponItemLevel - 1),
            };
        }
    }
}
