using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Config
{
    public enum SkillKind
    {
        Active,
        Passive,
    }

    /// <summary>The kinds of effect a skill node can carry (spec §3.5). New kinds need code once (D-032).</summary>
    public enum EffectKind
    {
        Hit,
        Dot,
        Buff,
        EnemySlow,
        Stat,
        ConditionalDamage,
        Trigger,
    }

    /// <summary>One effect component of a skill node. Values are for rank 1; <see cref="PerRank"/> adds per extra rank.</summary>
    public sealed class SkillEffect
    {
        public EffectKind Kind;
        public string Stat;
        public string Condition;
        public string Trigger;
        public Element Element = Element.Physical;
        public int Hits = 1;
        public bool AlwaysCrit;
        public bool CannotMiss;
        public double Percent;
        public double Value;
        public double Duration;
        public double Threshold;
        public double MaxStacks;
        public Dictionary<string, double> PerRank = new Dictionary<string, double>();

        /// <summary>The field's value at <paramref name="rank"/> (1-based): base + per_rank × (rank − 1).</summary>
        public double At(string field, int rank)
        {
            double baseValue;
            switch (field)
            {
                case "percent": baseValue = Percent; break;
                case "value": baseValue = Value; break;
                case "duration": baseValue = Duration; break;
                case "threshold": baseValue = Threshold; break;
                case "max_stacks": baseValue = MaxStacks; break;
                default: throw new KeyNotFoundException("unknown skill effect field '" + field + "'");
            }
            PerRank.TryGetValue(field, out double step);
            return baseValue + step * (rank < 1 ? 0 : rank - 1);
        }
    }

    /// <summary>One node of a class's Common skill tree (GDD §7, M6-1).</summary>
    public sealed class SkillNode
    {
        public string Id;
        public ClassId Class;
        public int Tier;
        public SkillKind Kind;
        public int MaxRank;
        public double CooldownS;
        public double RankCdr;
        public List<SkillEffect> Effects = new List<SkillEffect>();
    }

    /// <summary>All skill nodes, read from skills_config.json (M6-3).</summary>
    public sealed class SkillsConfig
    {
        public List<SkillNode> Nodes = new List<SkillNode>();
        public Dictionary<ClassId, List<string>> SuggestedOrder = new Dictionary<ClassId, List<string>>();

        private Dictionary<string, SkillNode> _byId;

        public SkillNode Find(string id)
        {
            if (id == null) return null;
            if (_byId == null) _byId = Nodes.ToDictionary(n => n.Id);
            return _byId.TryGetValue(id, out var n) ? n : null;
        }

        public IEnumerable<SkillNode> For(ClassId c) => Nodes.Where(n => n.Class == c);
    }

    /// <summary>General skill rules from balance_config.json "skill_rules".</summary>
    public sealed class SkillRulesConfig
    {
        public int TierPoints { get; set; }
        public double CooldownReductionCap { get; set; }
        public double RespecBase { get; set; }
        public double RespecGrowth { get; set; }
        public double DotTickS { get; set; }
        public double HeroDodgeCap { get; set; }
    }
}
