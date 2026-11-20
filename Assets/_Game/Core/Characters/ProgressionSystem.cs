using System;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;

namespace DarkFantasy.Core.Characters
{
    /// <summary>
    /// XP, levels and attribute points (GDD section 5, D-014, D-034): each level pays the class's
    /// 3 automatic points for that step of its 4-level pattern, plus 1 manual point.
    /// </summary>
    public sealed class ProgressionSystem
    {
        /// <summary>Preset key for the per-character weights in <see cref="CharacterState.CustomWeights"/> (M4-2).</summary>
        public const string CustomPreset = "custom";

        /// <summary>The default preset (allocation_presets.primary): every manual point into the class primary attribute.</summary>
        public const string DefaultPreset = "primary";

        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public ProgressionSystem(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        /// <summary>Adds XP and performs every level-up it pays for. Returns levels gained.</summary>
        public int AddXp(CharacterState c, double xp)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (!(xp > 0) || double.IsInfinity(xp)) return 0; // ignores NaN, negatives and infinity
            c.Xp += xp;
            int gained = 0;
            double need;
            while (c.Xp >= (need = _f.XpToNext(c.Level)))
            {
                c.Xp -= need;
                LevelUp(c);
                gained++;
            }
            return gained;
        }

        private void LevelUp(CharacterState c)
        {
            c.Level++;
            var pattern = _c.Classes[c.Class].GrowthPattern;
            // The level-up into level L uses pattern row (L - 2) mod 4.
            foreach (var a in pattern[(c.Level - 2) % pattern.Length])
                c.AutoPoints[a] += 1;
            c.UnspentPoints += _c.Player.PointsPerLevelManual;
        }

        public bool SpendPoint(CharacterState c, AttributeId a)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (c.UnspentPoints <= 0) return false;
            c.UnspentPoints--;
            c.ManualPoints[a] += 1;
            return true;
        }

        /// <summary>
        /// Spends all unspent points following the character's preset. Each point goes to the
        /// attribute furthest below its target share (ties: enum order). Unknown or empty presets
        /// spend nothing. Returns points spent.
        /// </summary>
        public int AutoAllocate(CharacterState c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (c.UnspentPoints <= 0 || c.Preset == null) return 0;

            var shares = new double[Enum.GetValues(typeof(AttributeId)).Length];
            double totalShare = 0;
            if (c.Preset == CustomPreset)
            {
                foreach (var kv in c.CustomWeights)
                {
                    int w = Math.Min(kv.Value, _c.AllocationRules.CustomWeightMax);
                    if (w <= 0) continue;
                    shares[(int)kv.Key] += w;
                    totalShare += w;
                }
                if (totalShare <= 0)
                {
                    shares[(int)_c.Classes[c.Class].Primary] = 1; // default: all into the primary attribute
                    totalShare = 1;
                }
            }
            else
            {
                if (!_c.AllocationPresets.TryGetValue(c.Preset, out var preset)) return 0;
                foreach (var kv in preset)
                {
                    if (!(kv.Value > 0)) continue;
                    AttributeId a = kv.Key == "primary" ? _c.Classes[c.Class].Primary : Parse(kv.Key);
                    shares[(int)a] += kv.Value;
                    totalShare += kv.Value;
                }
            }
            if (totalShare <= 0) return 0;

            int spent = 0;
            while (c.UnspentPoints > 0)
            {
                int manualTotal = 0;
                foreach (var v in c.ManualPoints.Values) manualTotal += v;
                int best = -1;
                double bestGap = double.NegativeInfinity;
                for (int i = 0; i < shares.Length; i++)
                {
                    if (shares[i] <= 0) continue;
                    double gap = shares[i] / totalShare * (manualTotal + 1) - c.ManualPoints[(AttributeId)i];
                    if (gap > bestGap + 1e-12)
                    {
                        bestGap = gap;
                        best = i;
                    }
                }
                SpendPoint(c, (AttributeId)best);
                spent++;
            }
            return spent;
        }

        /// <summary>Sets a Custom preset weight, clamped to 0…allocation_rules.custom_weight_max; 0 removes it.</summary>
        public void SetCustomWeight(CharacterState c, AttributeId a, int weight)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            int w = Math.Max(0, Math.Min(weight, _c.AllocationRules.CustomWeightMax));
            if (w == 0) c.CustomWeights.Remove(a);
            else c.CustomWeights[a] = w;
        }

        public int Total(CharacterState c, AttributeId a) => c.AutoPoints[a] + c.ManualPoints[a];

        private static AttributeId Parse(string key)
        {
            if (!AttributeIds.TryParse(key, out var a))
                throw new ArgumentException("Unknown attribute '" + key + "' in allocation preset");
            return a;
        }
    }
}
