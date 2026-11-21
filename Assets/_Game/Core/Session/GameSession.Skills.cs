using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Session
{
    public sealed partial class GameSession
    {
        private SkillBuild _skillBuild;

        /// <summary>Seconds left on each hero's skills, by node id (M6-2). Kept in memory only: after a load all are ready.</summary>
        private readonly Dictionary<ClassId, Dictionary<string, double>> _cooldowns = new Dictionary<ClassId, Dictionary<string, double>>();

        /// <summary>Skill points, ranks and loadout rules; null when no skills_config.json is loaded.</summary>
        public SkillTree Skills { get; private set; }

        private void InitSkills()
        {
            if (_c.Skills == null) return;
            Skills = new SkillTree(_c);
            _skillBuild = new SkillBuild(_c);
            foreach (var h in State.Heroes) Skills.Sanitize(h);
        }

        /// <summary>The active hero's skills for the next fight.</summary>
        public SkillKit SkillKit() => _skillBuild == null ? DarkFantasy.Core.Skills.SkillKit.None : _skillBuild.Kit(State.Character);

        public double CooldownLeft(ClassId hero, string skillId) =>
            _cooldowns.TryGetValue(hero, out var map) && map.TryGetValue(skillId, out double left) ? left : 0;

        private double[] CooldownsFor(CharacterState hero, SkillKit kit)
        {
            var result = new double[kit.Actives.Count];
            for (int i = 0; i < result.Length; i++) result[i] = CooldownLeft(hero.Class, kit.Actives[i].Node.Id);
            return result;
        }

        /// <summary>
        /// After a fight is applied, the slotted skills keep what the fight left minus the respawn gap; the active
        /// hero's other timers lose the whole fight and gap. A dropped fight commits nothing.
        /// </summary>
        private void CommitCooldowns(PreparedFight fight)
        {
            if (fight.Kit == null || fight.CooldownsAfter == null) return;
            var hero = State.Character.Class;
            if (!_cooldowns.TryGetValue(hero, out var map)) _cooldowns[hero] = map = new Dictionary<string, double>();
            double elapsed = fight.Result.Duration + _c.Combat.EnemyRespawnS;
            foreach (var id in new List<string>(map.Keys)) map[id] = Math.Max(0, map[id] - elapsed);
            for (int i = 0; i < fight.Kit.Actives.Count; i++)
                map[fight.Kit.Actives[i].Node.Id] = Math.Max(0, fight.CooldownsAfter[i] - _c.Combat.EnemyRespawnS);
        }

        private void ResetTimer(ClassId hero, string id)
        {
            if (_cooldowns.TryGetValue(hero, out var map)) map.Remove(id);
        }

        public SkillResult AddSkillRank(string id) => Changed(Skills.AddRank(State.Character, id));

        /// <summary>Puts a learned active in a slot; a skill that wasn't slotted before starts ready (spec §3.2).</summary>
        public SkillResult SlotSkill(string id, int slot)
        {
            bool wasSlotted = Array.IndexOf(State.Character.Loadout, id) >= 0;
            var r = Skills.Slot(State.Character, id, slot);
            if (r == SkillResult.Ok && !wasSlotted) ResetTimer(State.Character.Class, id);
            return Changed(r);
        }

        public void UnslotSkill(int slot)
        {
            Skills.Unslot(State.Character, slot);
            Changed(SkillResult.Ok);
        }

        public SkillResult SwapSlots(int a, int b)
        {
            Skills.Swap(State.Character, a, b);
            return Changed(SkillResult.Ok);
        }

        public SkillResult RespecSkills() => Changed(Skills.Respec(State.Character, State.Wallet));

        public void SetAutoSpendSkills(bool on)
        {
            State.Character.AutoSpendSkills = on;
            if (on) Skills.AutoSpend(State.Character);
            Changed(SkillResult.Ok);
        }

        private SkillResult Changed(SkillResult r)
        {
            if (r == SkillResult.Ok) Publish(new SkillsChanged(State.Character.Class));
            return r;
        }
    }
}
