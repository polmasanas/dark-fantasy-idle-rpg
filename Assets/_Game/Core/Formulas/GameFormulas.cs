using System;
using DarkFantasy.Core.Config;

namespace DarkFantasy.Core.Formulas
{
    /// <summary>
    /// The game's progression formulas (Docs/GAME_BALANCE.md). Mirrors class Formulas in
    /// Tools/BalanceSim/balance_sim.py exactly; FormulaGoldenTests keeps the two in sync.
    /// </summary>
    public sealed class GameFormulas
    {
        public const string HasteArtifactId = "haste";

        private readonly BalanceConfig _c;

        public GameFormulas(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ---- World (GAME_BALANCE section 2) ----
        public int KillsToAdvance(int region)
        {
            var w = _c.World;
            return Math.Min(w.KillsToAdvanceCap, w.KillsToAdvanceBase + w.KillsToAdvancePerRegion * (region - 1));
        }

        public int SubzoneLevel(int region, int sub)
        {
            var w = _c.World;
            int globalSubzone = (region - 1) * w.SubzonesPerRegion + sub;
            return (globalSubzone - 1) * w.LevelsPerSubzone + 1;
        }

        public int BossLevel(int region) => region * _c.World.SubzonesPerRegion * _c.World.LevelsPerSubzone;

        // ---- Enemies (section 3) ----
        public double EnemyHp(int level, bool boss)
        {
            var e = _c.Enemy;
            return e.HpBase * Math.Pow(e.HpGrowth, level - 1) * (boss ? e.BossHpMult : 1.0);
        }

        public double EnemyAtk(int level, bool boss)
        {
            var e = _c.Enemy;
            return e.AtkBase * Math.Pow(e.AtkGrowth, level - 1) * (boss ? e.BossAtkMult : 1.0);
        }

        // ---- Experience (section 4.3) ----
        public double XpToNext(int level)
        {
            var x = _c.Xp;
            double kills = x.KillsPerLevelBase + x.KillsPerLevelPerLevel * level;
            return x.EnemyXpBase * Math.Pow(x.LevelReqGrowth, level - 1) * kills;
        }

        public double EnemyXp(int enemyLevel, int playerLevel, bool boss)
        {
            var x = _c.Xp;
            double xp = x.EnemyXpBase * Math.Pow(x.EnemyXpGrowth, enemyLevel - 1);
            int over = playerLevel - enemyLevel;
            if (over > 5)
                xp *= Math.Max(x.OverlevelPenaltyFloor, 1 - x.OverlevelPenaltyPerLevel * (over - 5));
            return xp * (boss ? x.BossXpMult : 1.0);
        }

        // ---- Defence (section 4.2) ----
        public double ArmourK(int enemyLevel) =>
            _c.Player.ArmourKBase * Math.Pow(_c.Player.ArmourKGrowth, enemyLevel - 1);

        // ---- Economy (section 6.4) ----
        public double GoldDrop(int level, bool boss)
        {
            var ec = _c.Economy;
            return ec.GoldBase * Math.Pow(ec.GoldGrowth, level - 1) * (boss ? ec.BossGoldMult : 1.0);
        }

        public double UpgradeCost(int itemLevel, int currentUpgrade) =>
            _c.Economy.UpgradeCostBase * Math.Pow(_c.Gear.ItemGrowth, itemLevel - 1)
            * Math.Pow(_c.Economy.UpgradeCostGrowth, currentUpgrade);

        // ---- Prestige (section 8) ----
        public long DustForRun(int highestBossRegion)
        {
            var p = _c.Prestige;
            if (highestBossRegion < p.MinRegionBoss) return 0;
            return (long)Math.Floor(p.DustBase * Math.Pow(p.DustGrowthPerRegion, highestBossRegion - p.MinRegionBoss));
        }

        /// <summary>Dust actually paid, including the first-prestiges bonus (D-037).</summary>
        public long DustForPrestige(int highestBossRegion, int prestigesDone)
        {
            long dust = DustForRun(highestBossRegion);
            if (prestigesDone < _c.Prestige.FirstPrestigesCount)
                dust = (long)Math.Floor(dust * _c.Prestige.FirstPrestigesDustMult);
            return dust;
        }

        /// <summary>Cost of buying the next level; +Infinity when the artifact is maxed.</summary>
        public double ArtifactCost(string artifactId, int currentLevel)
        {
            if (artifactId == HasteArtifactId)
            {
                var cb = _c.Combat;
                if (currentLevel >= cb.HasteMaxLevel) return double.PositiveInfinity;
                return cb.HasteCostBase * Math.Pow(cb.HasteCostGrowth, currentLevel);
            }
            var a = Artifact(artifactId);
            return a.CostBase * Math.Pow(a.CostGrowth, currentLevel);
        }

        /// <summary>
        /// Effect multiplier at a level: multiplicative (1+p)^level for stat artifacts,
        /// additive 1 + p·level for Haste (attack speed, R2-1b).
        /// </summary>
        public double ArtifactMult(string artifactId, int level)
        {
            if (artifactId == HasteArtifactId)
                return 1 + _c.Combat.HastePerLevel * level;
            return Math.Pow(1 + Artifact(artifactId).PerLevel, level);
        }

        private ArtifactConfig Artifact(string id)
        {
            if (!_c.Prestige.Artifacts.TryGetValue(id, out var a))
                throw new ArgumentException("Unknown artifact '" + id + "'", nameof(id));
            return a;
        }
    }
}
