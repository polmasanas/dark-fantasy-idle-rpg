using System;
using System.Collections.Generic;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Items
{
    /// <summary>
    /// Rolls new items: kind (main hand ×3), type, rarity (Luck shifts it up), random attributes
    /// (no duplicates, higher rarity rolls higher) and the optional class bonus line (M3-3, M3-4, R2-6).
    /// </summary>
    public sealed class LootSystem
    {
        private readonly BalanceConfig _c;
        private readonly ItemKind[] _kinds;
        private readonly double[] _kindWeights;

        public LootSystem(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _kinds = (ItemKind[])Enum.GetValues(typeof(ItemKind));
            _kindWeights = new double[_kinds.Length];
            var w = _c.Gear.SlotDropWeights;
            for (int i = 0; i < _kinds.Length; i++)
            {
                var k = _kinds[i];
                _kindWeights[i] = k == ItemKind.MainHand ? w["main_hand"]
                    : k == ItemKind.OffHand ? w["off_hand"]
                    : k == ItemKind.Ring ? 2 * w["jewellery"] // two ring slots
                    : k == ItemKind.Amulet ? w["jewellery"]
                    : w["armour"];
            }
        }

        public ItemInstance Roll(int itemLevel, IRng rng, Rarity minRarity = Rarity.Common, double luck = 0)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var kind = RollKind(rng);
            var types = _c.Items.TypesFor(kind);
            var type = types[(int)(rng.NextDouble() * types.Count)];
            var rarity = RollRarity(rng, minRarity, luck);
            var item = new ItemInstance
            {
                Kind = kind,
                TypeId = type.Id,
                ItemLevel = Math.Max(1, itemLevel),
                Rarity = rarity,
            };
            RollAttributes(item, rng);
            if (rng.Chance(_c.Items.ClassItemShare))
            {
                var classes = (ClassId[])Enum.GetValues(typeof(ClassId));
                item.ClassBonus = classes[(int)(rng.NextDouble() * classes.Length)];
            }
            return item;
        }

        public ItemKind RollKind(IRng rng) => _kinds[Weighted(rng, _kindWeights)];

        /// <summary>Rarity from the configured weights, never below <paramref name="min"/>; Luck multiplies the weights above Common by 1 + L/(L+k).</summary>
        public Rarity RollRarity(IRng rng, Rarity min, double luck)
        {
            var baseWeights = _c.Gear.RarityWeights;
            var weights = new double[baseWeights.Length];
            double luckMult = luck > 0 ? 1 + luck / (luck + _c.Combat.LuckK) : 1;
            for (int r = 0; r < weights.Length; r++)
                weights[r] = r < (int)min ? 0 : baseWeights[r] * (r > 0 ? luckMult : 1);
            return (Rarity)Weighted(rng, weights);
        }

        /// <summary>Roll quality in [0, 1): uniform, biased toward 1 for higher rarities.</summary>
        public double RollQuality(IRng rng, Rarity rarity) =>
            Math.Pow(rng.NextDouble(), 1.0 / (1.0 + _c.Items.RarityRollBias * (int)rarity));

        private void RollAttributes(ItemInstance item, IRng rng)
        {
            int count = _c.Gear.RandomAffixes[(int)item.Rarity];
            var candidates = new List<AttributeConfig>();
            foreach (var a in _c.Items.Attributes)
                if (a.Kinds.Contains(item.Kind)) candidates.Add(a);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = (int)(rng.NextDouble() * candidates.Count);
                var a = candidates[pick];
                candidates.RemoveAt(pick);
                double value = a.Min + (a.Max - a.Min) * RollQuality(rng, item.Rarity) + a.PerItemLevel * item.ItemLevel;
                item.Attributes.Add(new RolledAttribute(a.Id, a.Stat, value));
            }
        }

        private static int Weighted(IRng rng, double[] weights)
        {
            double total = 0;
            foreach (var w in weights) total += w;
            double x = rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                x -= weights[i];
                if (x < 0) return i;
            }
            for (int i = weights.Length - 1; i >= 0; i--) if (weights[i] > 0) return i;
            throw new InvalidOperationException("All weights are zero.");
        }
    }
}
