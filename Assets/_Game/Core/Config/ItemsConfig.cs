using System.Collections.Generic;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.Items;

namespace DarkFantasy.Core.Config
{
    /// <summary>Item types per kind, the random attribute pool and loot settings (M3-3, M3-4).</summary>
    public sealed class ItemsConfig
    {
        public Dictionary<ItemKind, List<ItemTypeConfig>> Types { get; set; }
        public List<AttributeConfig> Attributes { get; set; }
        public double ClassItemShare { get; set; }
        public double ClassBonusPctDamage { get; set; }
        public double ClassBonusPctElemental { get; set; }
        public Dictionary<ClassId, string> StarterTypes { get; set; }
        public double RarityRollBias { get; set; }
        public double ExpansionCostBase { get; set; }
        public double ExpansionCostGrowth { get; set; }

        public IReadOnlyList<ItemTypeConfig> TypesFor(ItemKind kind) => Types[kind];
    }

    public sealed class ItemTypeConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<StatLineConfig> Lines { get; set; }
    }

    public sealed class StatLineConfig
    {
        public GearStat Stat { get; set; }
        public double Base { get; set; }
        public StatLineScaling Scaling { get; set; }
        public double PerItemLevel { get; set; }
    }

    public sealed class AttributeConfig
    {
        public string Id { get; set; }
        public GearStat Stat { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double PerItemLevel { get; set; }
        public List<ItemKind> Kinds { get; set; }
    }
}
