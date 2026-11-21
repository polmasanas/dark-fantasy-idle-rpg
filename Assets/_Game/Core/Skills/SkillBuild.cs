using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Skills
{
    /// <summary>A slotted active skill with its rank and final cooldown (rank and cooldown reduction applied).</summary>
    public sealed class ActiveSkill
    {
        public SkillNode Node;
        public int Rank;
        public int Slot;
        public double Cooldown;
    }

    /// <summary>A "+x% damage when …" passive. Bonuses of the same node don't stack: the largest match counts.</summary>
    public sealed class ConditionalBonus
    {
        public string NodeId;
        public string Condition;
        public double Value;
        public double Threshold;
    }

    /// <summary>What the combat engine needs from a hero's skills in one fight (spec §3.2–3.5).</summary>
    public sealed class SkillKit
    {
        /// <summary>Slotted actives in priority order (slot 1 first).</summary>
        public List<ActiveSkill> Actives = new List<ActiveSkill>();

        public double Dodge;
        public double DamageTakenCut;
        public double BossDamageTakenCut;
        public double SkillCritDamage;
        public double Lifesteal;
        public double HealBelowAt;
        public double HealBelowPct;
        public double CheatDeathHealPct;
        public double MomentumPerHit;
        public double MomentumMaxStacks;
        public double EchoChance;
        public List<ConditionalBonus> ConditionalDamage = new List<ConditionalBonus>();

        /// <summary>No skills at all: the engine then runs the exact M5 code path.</summary>
        public static SkillKit None => new SkillKit();

        public bool HasPassives =>
            Dodge > 0 || DamageTakenCut > 0 || BossDamageTakenCut > 0 || SkillCritDamage > 0 || Lifesteal > 0
            || HealBelowPct > 0 || CheatDeathHealPct > 0 || MomentumPerHit > 0 || EchoChance > 0 || ConditionalDamage.Count > 0;

        public bool IsEmpty => Actives.Count == 0 && !HasPassives;
    }

    /// <summary>Turns a hero's ranks into stat bonuses (<see cref="AddStats"/>) and a <see cref="SkillKit"/>.</summary>
    public sealed class SkillBuild
    {
        private readonly BalanceConfig _c;
        private readonly SkillsConfig _s;

        public SkillBuild(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _s = config.Skills ?? throw new ArgumentException("BalanceConfig.Skills is not loaded.", nameof(config));
        }

        /// <summary>Adds the permanent "stat" effects that the stat calculator understands.</summary>
        public void AddStats(CharacterState h, StatInputs into)
        {
            foreach (var (node, rank) in Learned(h))
                foreach (var e in node.Effects)
                {
                    if (e.Kind != EffectKind.Stat) continue;
                    double v = e.At("value", rank);
                    switch (e.Stat)
                    {
                        case "pct_hp": into.PctHp += v; break;
                        case "pct_armour": into.PctArmour += v; break;
                        case "attack_speed": into.AttackSpeedPct += v; break;
                        case "pct_elemental": into.PctElemental += v; break;
                        case "pct_damage": into.PctDamage += v; break;
                        case "crit_chance": into.SkillCritChance += v; break;
                        case "crit_damage": into.SkillCritDamage += v; break;
                        case "counter_chance": into.SkillCounterChance += v; break;
                    }
                }
        }

        /// <summary>
        /// The hero's actives (slot order) and combat passives. <paramref name="extraCooldownReduction"/> and
        /// <paramref name="extraDodge"/> are for future non-skill sources; both are capped with the skill values.
        /// </summary>
        public SkillKit Kit(CharacterState h, double extraCooldownReduction = 0, double extraDodge = 0)
        {
            var kit = new SkillKit();
            double cdr = extraCooldownReduction, dodge = extraDodge;
            foreach (var (node, rank) in Learned(h))
                foreach (var e in node.Effects)
                {
                    if (e.Kind == EffectKind.Stat)
                    {
                        double v = e.At("value", rank);
                        switch (e.Stat)
                        {
                            case "cooldown_reduction": cdr += v; break;
                            case "dodge": dodge += v; break;
                            case "damage_taken": kit.DamageTakenCut += v; break;
                            case "boss_damage_taken": kit.BossDamageTakenCut += v; break;
                            case "skill_crit_damage": kit.SkillCritDamage += v; break;
                        }
                    }
                    else if (e.Kind == EffectKind.ConditionalDamage)
                    {
                        kit.ConditionalDamage.Add(new ConditionalBonus
                        {
                            NodeId = node.Id, Condition = e.Condition, Value = e.At("value", rank), Threshold = e.At("threshold", rank),
                        });
                    }
                    else if (e.Kind == EffectKind.Trigger)
                    {
                        double v = e.At("value", rank);
                        switch (e.Trigger)
                        {
                            case "heal_below": kit.HealBelowPct += v; kit.HealBelowAt = e.At("threshold", rank); break;
                            case "cheat_death": kit.CheatDeathHealPct += v; break;
                            case "lifesteal": kit.Lifesteal += v; break;
                            case "momentum": kit.MomentumPerHit += v; kit.MomentumMaxStacks = e.At("max_stacks", rank); break;
                            case "echo": kit.EchoChance += v; break;
                        }
                    }
                }
            kit.Dodge = Math.Min(_c.SkillRules.HeroDodgeCap, Math.Max(0, dodge));
            double cut = Math.Min(_c.SkillRules.CooldownReductionCap, Math.Max(0, cdr));

            if (h.Loadout != null)
                for (int slot = 0; slot < h.Loadout.Length; slot++)
                {
                    var node = _s.Find(h.Loadout[slot]);
                    if (node == null || node.Class != h.Class || node.Kind != SkillKind.Active) continue;
                    if (!h.SkillRanks.TryGetValue(node.Id, out int rank) || rank < 1) continue;
                    kit.Actives.Add(new ActiveSkill
                    {
                        Node = node,
                        Rank = rank,
                        Slot = slot,
                        Cooldown = node.CooldownS * Math.Max(0, 1 - node.RankCdr * (rank - 1)) * (1 - cut),
                    });
                }
            return kit;
        }

        private IEnumerable<(SkillNode node, int rank)> Learned(CharacterState h)
        {
            if (h.SkillRanks == null) yield break;
            foreach (var kv in h.SkillRanks)
            {
                var node = _s.Find(kv.Key);
                if (node == null || node.Class != h.Class || kv.Value < 1) continue;
                yield return (node, Math.Min(kv.Value, node.MaxRank));
            }
        }
    }
}
