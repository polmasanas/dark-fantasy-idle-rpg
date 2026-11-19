using System;
using System.Collections.Generic;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Characters
{
    /// <summary>One character's progression (saved). Reset on prestige (D-023).</summary>
    public sealed class CharacterState
    {
        public ClassId Class;
        public int Level = 1;
        public double Xp;
        public Dictionary<AttributeId, int> AutoPoints = new Dictionary<AttributeId, int>();
        public Dictionary<AttributeId, int> ManualPoints = new Dictionary<AttributeId, int>();
        public int UnspentPoints;

        /// <summary>Auto-allocate preset key from balance_config.json allocation_presets.</summary>
        public string Preset = "primary";

        /// <summary>
        /// Item level of the equipped Common starter weapon. Loot replaces this in M3;
        /// until then the development panel can raise it to test later content.
        /// </summary>
        public double WeaponItemLevel = 1;

        /// <summary>
        /// Development placeholder until loot exists (M3): when above 0, six Common armour pieces
        /// at this item level. 0 = no armour, the real starting state.
        /// </summary>
        public double PlaceholderArmourItemLevel;

        public static CharacterState Create(ClassId cls)
        {
            var c = new CharacterState { Class = cls };
            foreach (AttributeId a in Enum.GetValues(typeof(AttributeId)))
            {
                c.AutoPoints[a] = 0;
                c.ManualPoints[a] = 0;
            }
            return c;
        }
    }
}
