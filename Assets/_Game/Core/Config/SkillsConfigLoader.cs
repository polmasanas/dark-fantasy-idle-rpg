using System.Collections.Generic;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Config
{
    /// <summary>
    /// Reads skills_config.json (M6-3). Unknown kinds, stats, conditions or triggers fail loudly with
    /// the path of the bad field, like <see cref="BalanceConfigLoader"/>.
    /// </summary>
    public static class SkillsConfigLoader
    {
        public static readonly string[] Stats =
        {
            "pct_hp", "pct_armour", "attack_speed", "crit_chance", "crit_damage", "counter_chance", "pct_elemental",
            "pct_damage", "dodge", "cooldown_reduction", "damage_taken", "boss_damage_taken", "skill_crit_damage",
        };

        public static readonly string[] Triggers = { "heal_below", "cheat_death", "lifesteal", "momentum", "echo" };

        private static readonly string[] Conditions = { "enemy_hp_below", "own_hp_above", "vs_boss" };

        public static SkillsConfig FromJson(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new BalanceConfigException("skills config is not valid JSON: " + e.Message, e);
            }

            var result = new SkillsConfig();
            var ids = new HashSet<string>();
            if (!(root["nodes"] is JArray nodes)) throw new BalanceConfigException("skills config: 'nodes' must be a list");
            for (int i = 0; i < nodes.Count; i++)
            {
                string path = "nodes[" + i + "]";
                if (!(nodes[i] is JObject n)) throw new BalanceConfigException(path + " must be an object");
                var node = new SkillNode
                {
                    Id = Str(n, path, "id"),
                    Class = Class(Str(n, path, "class"), path + ".class"),
                    Tier = Int(n, path, "tier"),
                    MaxRank = Int(n, path, "max_rank"),
                };
                string kind = Str(n, path, "kind");
                if (kind == "active") node.Kind = SkillKind.Active;
                else if (kind == "passive") node.Kind = SkillKind.Passive;
                else throw new BalanceConfigException(path + ".kind: unknown kind '" + kind + "'");
                if (!ids.Add(node.Id)) throw new BalanceConfigException(path + ".id: duplicate id '" + node.Id + "'");
                if (node.Tier < 1 || node.Tier > 4) throw new BalanceConfigException(path + ".tier must be 1–4");
                if (node.MaxRank < 1) throw new BalanceConfigException(path + ".max_rank must be at least 1");
                if (node.Kind == SkillKind.Active)
                {
                    node.CooldownS = Num(n, path, "cooldown_s");
                    node.RankCdr = Num(n, path, "rank_cdr");
                    if (!(node.CooldownS > 0)) throw new BalanceConfigException(path + ".cooldown_s must be positive");
                }
                if (!(n["effects"] is JArray effects)) throw new BalanceConfigException(path + ".effects must be a list");
                for (int j = 0; j < effects.Count; j++)
                    node.Effects.Add(Effect(effects[j] as JObject, path + ".effects[" + j + "]"));
                Validate(node, path);
                result.Nodes.Add(node);
            }

            if (!(root["suggested_order"] is JObject order)) throw new BalanceConfigException("skills config: 'suggested_order' must be an object");
            foreach (var prop in order.Properties())
            {
                var cls = Class(prop.Name, "suggested_order." + prop.Name);
                if (!(prop.Value is JArray list)) throw new BalanceConfigException("suggested_order." + prop.Name + " must be a list");
                var ordered = new List<string>();
                foreach (var t in list)
                {
                    string id = (string)t;
                    var node = result.Find(id);
                    if (node == null || node.Class != cls)
                        throw new BalanceConfigException("suggested_order." + prop.Name + ": unknown node '" + id + "' for that class");
                    ordered.Add(id);
                }
                result.SuggestedOrder[cls] = ordered;
            }
            return result;
        }

        private static SkillEffect Effect(JObject o, string path)
        {
            if (o == null) throw new BalanceConfigException(path + " must be an object");
            var e = new SkillEffect();
            string kind = Str(o, path, "kind");
            switch (kind)
            {
                case "hit": e.Kind = EffectKind.Hit; break;
                case "dot": e.Kind = EffectKind.Dot; break;
                case "buff": e.Kind = EffectKind.Buff; break;
                case "enemy_slow": e.Kind = EffectKind.EnemySlow; break;
                case "stat": e.Kind = EffectKind.Stat; break;
                case "conditional_damage": e.Kind = EffectKind.ConditionalDamage; break;
                case "trigger": e.Kind = EffectKind.Trigger; break;
                default: throw new BalanceConfigException(path + ".kind: unknown kind '" + kind + "'");
            }
            e.Percent = Opt(o, path, "percent");
            e.Value = Opt(o, path, "value");
            e.Duration = Opt(o, path, "duration");
            e.Threshold = Opt(o, path, "threshold");
            e.MaxStacks = Opt(o, path, "max_stacks");
            if (o["hits"] != null) e.Hits = Int(o, path, "hits");
            e.AlwaysCrit = o["always_crit"] != null && o["always_crit"].Type == JTokenType.Boolean && (bool)o["always_crit"];
            e.CannotMiss = o["cannot_miss"] != null && o["cannot_miss"].Type == JTokenType.Boolean && (bool)o["cannot_miss"];
            if (o["element"] != null)
            {
                string el = (string)o["element"];
                if (!Elements.TryParse(el, out e.Element)) throw new BalanceConfigException(path + ".element: unknown element '" + el + "'");
            }
            if (o["stat"] != null)
            {
                e.Stat = (string)o["stat"];
                if (System.Array.IndexOf(Stats, e.Stat) < 0) throw new BalanceConfigException(path + ".stat: unknown stat '" + e.Stat + "'");
            }
            if (o["trigger"] != null)
            {
                e.Trigger = (string)o["trigger"];
                if (System.Array.IndexOf(Triggers, e.Trigger) < 0) throw new BalanceConfigException(path + ".trigger: unknown trigger '" + e.Trigger + "'");
            }
            if (o["condition"] != null)
            {
                e.Condition = (string)o["condition"];
                bool known = System.Array.IndexOf(Conditions, e.Condition) >= 0
                             || e.Condition.StartsWith("vs_family:") || e.Condition.StartsWith("vs_archetype:");
                if (!known) throw new BalanceConfigException(path + ".condition: unknown condition '" + e.Condition + "'");
            }
            if ((e.Kind == EffectKind.Stat || e.Kind == EffectKind.Buff) && e.Stat == null)
                throw new BalanceConfigException(path + ": a " + kind + " effect needs a 'stat'");
            if (e.Kind == EffectKind.Trigger && e.Trigger == null)
                throw new BalanceConfigException(path + ": a trigger effect needs a 'trigger'");
            if (e.Kind == EffectKind.ConditionalDamage && e.Condition == null)
                throw new BalanceConfigException(path + ": a conditional_damage effect needs a 'condition'");
            if (o["per_rank"] is JObject per)
                foreach (var p in per.Properties())
                {
                    if (p.Value.Type != JTokenType.Float && p.Value.Type != JTokenType.Integer)
                        throw new BalanceConfigException(path + ".per_rank." + p.Name + " must be a number");
                    e.PerRank[p.Name] = p.Value.Value<double>();
                    e.At(p.Name, 1); // rejects unknown fields
                }
            return e;
        }

        /// <summary>
        /// Values the engine relies on, checked at every rank so bad data fails at boot instead of hanging a fight:
        /// a slow of −100% would make the enemy's interval 0, a cooldown cut per rank reaching 100% a cooldown of 0.
        /// </summary>
        private static void Validate(SkillNode node, string path)
        {
            if (node.Kind == SkillKind.Active && (node.RankCdr < 0 || node.RankCdr * (node.MaxRank - 1) >= 1))
                throw new BalanceConfigException(path + ".rank_cdr must be at least 0 and leave a cooldown above 0 at max rank");
            for (int j = 0; j < node.Effects.Count; j++)
            {
                var e = node.Effects[j];
                string ep = path + ".effects[" + j + "]";
                if (e.Hits < 1) throw new BalanceConfigException(ep + ".hits must be at least 1");
                for (int rank = 1; rank <= node.MaxRank; rank++)
                {
                    if (e.At("duration", rank) < 0) throw new BalanceConfigException(ep + ": duration below 0 at rank " + rank);
                    if ((e.Kind == EffectKind.Hit || e.Kind == EffectKind.Dot) && e.At("percent", rank) < 0)
                        throw new BalanceConfigException(ep + ": percent below 0 at rank " + rank);
                    if (e.Kind == EffectKind.EnemySlow && e.At("value", rank) < 0)
                        throw new BalanceConfigException(ep + ": a slow below 0 at rank " + rank + " would speed the enemy up");
                }
            }
        }

        private static ClassId Class(string key, string path)
        {
            switch (key)
            {
                case "fighter": return ClassId.Fighter;
                case "archer": return ClassId.Archer;
                case "mage": return ClassId.Mage;
                default: throw new BalanceConfigException(path + ": unknown class '" + key + "'");
            }
        }

        private static string Str(JObject o, string path, string key)
        {
            var t = o[key];
            if (t == null || t.Type != JTokenType.String) throw new BalanceConfigException(path + "." + key + " must be a string");
            return (string)t;
        }

        private static int Int(JObject o, string path, string key)
        {
            var t = o[key];
            if (t == null || t.Type != JTokenType.Integer) throw new BalanceConfigException(path + "." + key + " must be an integer");
            return t.Value<int>();
        }

        private static double Num(JObject o, string path, string key)
        {
            var t = o[key];
            if (t == null || (t.Type != JTokenType.Float && t.Type != JTokenType.Integer))
                throw new BalanceConfigException(path + "." + key + " must be a number");
            return t.Value<double>();
        }

        private static double Opt(JObject o, string path, string key) => o[key] == null ? 0 : Num(o, path, key);
    }
}
