using System;
using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Items;

namespace DarkFantasy.Core.Skills
{
    public enum SkillResult
    {
        Ok,
        NoPoints,
        TierLocked,
        MaxRank,
        UnknownSkill,
        NotActive,
        NotLearned,
        NotEnoughGold,
    }

    /// <summary>
    /// Skill points, ranks, loadout and respec for one hero (GDD §7, R2-8, R2-9, M6-1).
    /// Points earned = floor(level / skill_point_every_levels); unspent = earned − sum of ranks.
    /// </summary>
    public sealed class SkillTree
    {
        private readonly BalanceConfig _c;
        private readonly SkillsConfig _s;

        public SkillTree(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _s = config.Skills ?? throw new ArgumentException("BalanceConfig.Skills is not loaded.", nameof(config));
        }

        public int Earned(CharacterState h) => h.Level / _c.Combat.SkillPointEveryLevels;

        public int Spent(CharacterState h) => h.SkillRanks.Values.Sum();

        public int Unspent(CharacterState h) => Math.Max(0, Earned(h) - Spent(h));

        public int Rank(CharacterState h, string id) => h.SkillRanks.TryGetValue(id, out int r) ? r : 0;

        /// <summary>Tier t opens once (t − 1) × tier_points points are spent in the tree.</summary>
        public bool TierOpen(CharacterState h, int tier) => Spent(h) >= (tier - 1) * _c.SkillRules.TierPoints;

        public SkillResult CanAddRank(CharacterState h, string id)
        {
            var node = _s.Find(id);
            if (node == null || node.Class != h.Class) return SkillResult.UnknownSkill;
            if (Rank(h, id) >= node.MaxRank) return SkillResult.MaxRank;
            if (!TierOpen(h, node.Tier)) return SkillResult.TierLocked;
            if (Unspent(h) <= 0) return SkillResult.NoPoints;
            return SkillResult.Ok;
        }

        public SkillResult AddRank(CharacterState h, string id)
        {
            var r = CanAddRank(h, id);
            if (r == SkillResult.Ok) h.SkillRanks[id] = Rank(h, id) + 1;
            return r;
        }

        /// <summary>Puts a learned active in a slot; if it already sits in another slot it moves.</summary>
        public SkillResult Slot(CharacterState h, string id, int slot)
        {
            var node = _s.Find(id);
            if (node == null || node.Class != h.Class) return SkillResult.UnknownSkill;
            if (node.Kind != SkillKind.Active) return SkillResult.NotActive;
            if (Rank(h, id) < 1) return SkillResult.NotLearned;
            EnsureSlots(h);
            if (slot < 0 || slot >= h.Loadout.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            for (int i = 0; i < h.Loadout.Length; i++)
                if (h.Loadout[i] == id) h.Loadout[i] = null;
            h.Loadout[slot] = id;
            return SkillResult.Ok;
        }

        public void Unslot(CharacterState h, int slot)
        {
            EnsureSlots(h);
            if (slot >= 0 && slot < h.Loadout.Length) h.Loadout[slot] = null;
        }

        public void Swap(CharacterState h, int a, int b)
        {
            EnsureSlots(h);
            if (a < 0 || b < 0 || a >= h.Loadout.Length || b >= h.Loadout.Length) return;
            var t = h.Loadout[a];
            h.Loadout[a] = h.Loadout[b];
            h.Loadout[b] = t;
        }

        /// <summary>Gold cost of a respec: respec_base × respec_growth^(level − 1).</summary>
        public double RespecCost(CharacterState h) =>
            _c.SkillRules.RespecBase * Math.Pow(_c.SkillRules.RespecGrowth, h.Level - 1);

        public SkillResult Respec(CharacterState h, Wallet w)
        {
            double cost = RespecCost(h);
            if (w.Gold < cost) return SkillResult.NotEnoughGold;
            w.Gold -= cost;
            Reset(h);
            return SkillResult.Ok;
        }

        /// <summary>
        /// Spends every unspent point by the class's suggested order, skipping maxed or locked nodes, and puts
        /// each newly learned active in the first empty slot. Returns the number of ranks added.
        /// </summary>
        public int AutoSpend(CharacterState h)
        {
            if (!_s.SuggestedOrder.TryGetValue(h.Class, out var order)) return 0;
            EnsureSlots(h);
            int added = 0;
            bool progress = true;
            while (Unspent(h) > 0 && progress)
            {
                progress = false;
                // Each entry of the order is one rank step: the k-th mention of a node means "rank k of it".
                var mentions = new Dictionary<string, int>();
                foreach (var id in order)
                {
                    mentions.TryGetValue(id, out int k);
                    mentions[id] = ++k;
                    if (Rank(h, id) >= k || CanAddRank(h, id) != SkillResult.Ok) continue;
                    bool wasNew = Rank(h, id) == 0;
                    h.SkillRanks[id] = Rank(h, id) + 1;
                    added++;
                    progress = true;
                    if (wasNew && _s.Find(id).Kind == SkillKind.Active && !h.Loadout.Contains(id))
                    {
                        int free = Array.IndexOf(h.Loadout, null);
                        if (free >= 0) h.Loadout[free] = id;
                    }
                    break;
                }
            }
            return added;
        }

        /// <summary>
        /// Repairs a loaded hero: drops unknown or other-class node ids (their points come back), clears slots that
        /// hold anything but a learned active, and resets the whole tree for free when ranks exceed a node's max or
        /// the points earned (Review Focus 1–2).
        /// </summary>
        public void Sanitize(CharacterState h)
        {
            if (h.SkillRanks == null) h.SkillRanks = new Dictionary<string, int>();
            foreach (var id in h.SkillRanks.Keys.ToList())
            {
                var node = _s.Find(id);
                if (node == null || node.Class != h.Class || h.SkillRanks[id] <= 0) h.SkillRanks.Remove(id);
            }
            bool broken = h.SkillRanks.Any(kv => kv.Value > _s.Find(kv.Key).MaxRank) || Spent(h) > Earned(h);
            if (broken) h.SkillRanks.Clear();
            EnsureSlots(h);
            for (int i = 0; i < h.Loadout.Length; i++)
            {
                var id = h.Loadout[i];
                if (id == null) continue;
                var node = _s.Find(id);
                bool ok = node != null && node.Kind == SkillKind.Active && Rank(h, id) > 0
                          && Array.IndexOf(h.Loadout, id) == i;
                if (!ok) h.Loadout[i] = null;
            }
        }

        private void Reset(CharacterState h)
        {
            h.SkillRanks.Clear();
            EnsureSlots(h);
            for (int i = 0; i < h.Loadout.Length; i++) h.Loadout[i] = null;
        }

        private void EnsureSlots(CharacterState h)
        {
            int n = _c.Combat.ActiveSkillSlots;
            if (h.Loadout != null && h.Loadout.Length == n) return;
            var slots = new string[n];
            if (h.Loadout != null) Array.Copy(h.Loadout, slots, Math.Min(n, h.Loadout.Length));
            h.Loadout = slots;
        }
    }
}
