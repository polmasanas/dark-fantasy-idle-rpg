using System;
using System.Collections.Generic;
using DarkFantasy.Core.Items;
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
        public string Preset = ProgressionSystem.DefaultPreset;

        /// <summary>Spend new manual points by the preset as soon as they are earned, online and offline (M4-2).</summary>
        public bool AutoAllocateOnLevelUp;

        /// <summary>Weights 0–10 for the "custom" preset (M4-2); empty means "all into the primary attribute".</summary>
        public Dictionary<AttributeId, int> CustomWeights = new Dictionary<AttributeId, int>();

        /// <summary>Equipped item ids by slot (items live in the shared inventory, D-004).</summary>
        public Dictionary<EquipSlot, long> Equipped = new Dictionary<EquipSlot, long>();

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
