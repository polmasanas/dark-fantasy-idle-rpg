using System;
using System.Collections.Generic;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Characters
{
    /// <summary>
    /// Collects a character's sources into <see cref="StatInputs"/>: attributes and equipped items
    /// (M3); artifacts and skills join in later milestones.
    /// </summary>
    public sealed class CharacterStatsBuilder
    {
        private readonly BalanceConfig _c;
        private readonly GearStats _gear;

        public CharacterStatsBuilder(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _gear = new GearStats(config);
        }

        public StatInputs Build(CharacterState c, IEnumerable<ItemInstance> equipped)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            int Total(AttributeId a) => c.AutoPoints[a] + c.ManualPoints[a];
            var primary = _c.Classes[c.Class].Primary;
            var inputs = new StatInputs
            {
                Class = c.Class,
                Level = c.Level,
                Primary = Total(primary),
                Vit = Total(AttributeId.Vit),
                Def = Total(AttributeId.Def),
                CritPoints = Total(AttributeId.CritChance),
                CritDamagePoints = Total(AttributeId.CritDamage),
                CounterPoints = Total(AttributeId.Counter),
                Luck = Total(AttributeId.Luck),
            };
            _gear.Apply(inputs, equipped, c.Class);
            return inputs;
        }
    }
}
