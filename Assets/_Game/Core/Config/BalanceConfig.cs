using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Config
{
    /// <summary>
    /// All balance numbers, mirroring Tools/BalanceSim/balance_config.json (decision D-031).
    /// Property names are the JSON keys in PascalCase. Formulas read only from here.
    /// </summary>
    public sealed class BalanceConfig
    {
        public WorldConfig World { get; set; }
        public EnemyConfig Enemy { get; set; }
        public PlayerConfig Player { get; set; }
        public XpConfig Xp { get; set; }
        public GearConfig Gear { get; set; }
        public EconomyConfig Economy { get; set; }
        public MasteryConfig Mastery { get; set; }
        public OfflineConfig Offline { get; set; }
        public PrestigeConfig Prestige { get; set; }
        public CombatConfig Combat { get; set; }
        public Dictionary<ClassId, ClassConfig> Classes { get; set; }
        public ItemsConfig Items { get; set; }
        public OnboardingConfig Onboarding { get; set; }
        public OfflineRulesConfig OfflineRules { get; set; }
        public AllocationRulesConfig AllocationRules { get; set; }
        public SaveConfig Save { get; set; }
        public ElementsConfig Elements { get; set; }
        public TriangleConfig Triangle { get; set; }

        /// <summary>Preset name → attribute key ("primary" = class primary) → share of manual points.</summary>
        public Dictionary<string, Dictionary<string, double>> AllocationPresets { get; set; }
    }

    /// <summary>Owner decision ON-1: training boss in the first run and per-region boss strength overrides.</summary>
    public sealed class OnboardingConfig
    {
        public bool TrainingBossEnabled { get; set; }
        public int TrainingBossAfterSubzone { get; set; }
        public double TrainingBossHpMult { get; set; }

        /// <summary>Region → boss HP multiplier replacing enemy.boss_hp_mult for that region's boss.</summary>
        public Dictionary<int, double> BossHpMultOverrides { get; set; }
    }

    public sealed class ClassConfig
    {
        public AttributeId Primary { get; set; }

        /// <summary>4 rows (one per level-up in the cycle) × 3 automatic points (D-014, D-034).</summary>
        public AttributeId[][] GrowthPattern { get; set; }
    }

    public sealed class WorldConfig
    {
        public int SubzonesPerRegion { get; set; }
        public int LevelsPerSubzone { get; set; }
        public int KillsToAdvanceBase { get; set; }
        public int KillsToAdvancePerRegion { get; set; }
        public int KillsToAdvanceCap { get; set; }
    }

    public sealed class EnemyConfig
    {
        public double HpBase { get; set; }
        public double HpGrowth { get; set; }
        public double AtkBase { get; set; }
        public double AtkGrowth { get; set; }
        public double AttackIntervalS { get; set; }
        public double BossHpMult { get; set; }
        public double BossAtkMult { get; set; }
        public double BossTimeLimitS { get; set; }
        public double VarianceMultMin { get; set; }
        public double VarianceMultMax { get; set; }
    }

    public sealed class PlayerConfig
    {
        public int PointsPerLevelAuto { get; set; }
        public int PointsPerLevelManual { get; set; }
        public double BaseHp { get; set; }
        public double HpPerVitPct { get; set; }
        public double DmgPerPrimaryPct { get; set; }
        public double ArmourPctPerDef { get; set; }
        public double BaseHitChance { get; set; }
        public double BaseCritChance { get; set; }
        public double CritChancePerPoint { get; set; }
        public double CritChanceCap { get; set; }
        public double BaseCritDamage { get; set; }
        public double CritDamagePerPoint { get; set; }
        public double ArmourKBase { get; set; }
        public double ArmourKGrowth { get; set; }
        public double DamageReductionCap { get; set; }
        public double BetweenFightS { get; set; }
        public double CounterChancePerPoint { get; set; }
        public double CounterCap { get; set; }
        public double CounterDamage { get; set; }
        public double CatchupXpBonus { get; set; }
    }

    public sealed class XpConfig
    {
        public double EnemyXpBase { get; set; }
        public double EnemyXpGrowth { get; set; }
        public double LevelReqGrowth { get; set; }
        public double KillsPerLevelBase { get; set; }
        public double KillsPerLevelPerLevel { get; set; }
        public double BossXpMult { get; set; }
        public double OverlevelPenaltyPerLevel { get; set; }
        public double OverlevelPenaltyFloor { get; set; }
    }

    public sealed class GearConfig
    {
        public double WeaponDmgBase { get; set; }
        public double OffhandDmgShare { get; set; }
        public double ArmourBase { get; set; }
        public double ArmourHpBase { get; set; }
        public double ItemGrowth { get; set; }
        public double UpgradeBasePct { get; set; }
        public int[] UpgradeMaxByRarity { get; set; }
        public string[] RarityNames { get; set; }
        public double[] RarityBaseMult { get; set; }
        public double[] RarityWeights { get; set; }
        public int[] RandomAffixes { get; set; }
        public double AffixValuePct { get; set; }
        public double DropChanceNormal { get; set; }
        public int BossMinRarity { get; set; }

        /// <summary>Drop weight per slot group: main_hand, off_hand, armour, jewellery, accessory.</summary>
        public Dictionary<string, double> SlotDropWeights { get; set; }
    }

    public sealed class EconomyConfig
    {
        public double GoldBase { get; set; }
        public double GoldGrowth { get; set; }
        public double BossGoldMult { get; set; }
        public double UpgradeCostBase { get; set; }
        public double UpgradeCostGrowth { get; set; }
        public double SalvageUpgradeRefund { get; set; }
        public double CraftCostBase { get; set; }
        public double CraftCostPerIlvl { get; set; }
        public double ScrollDropChance { get; set; }
        public double[] MaterialPerSalvage { get; set; }
    }

    public sealed class MasteryConfig
    {
        public int KillsPerStar { get; set; }
        public int MaxStars { get; set; }
        public double GoldPerStar { get; set; }
        public double DropPerStar { get; set; }
    }

    public sealed class OfflineConfig
    {
        public double Efficiency { get; set; }
        public double CapHours { get; set; }
    }

    /// <summary>M4-1 / R2-19: when the "while you were away" report shows and how much clock drift is tolerated.</summary>
    public sealed class OfflineRulesConfig
    {
        public double ReportMinSeconds { get; set; }
        public double ClockToleranceS { get; set; }
    }

    /// <summary>M4-2: limits of the per-character Custom auto-allocate preset.</summary>
    public sealed class AllocationRulesConfig
    {
        public int CustomWeightMax { get; set; }
    }

    /// <summary>D-019 / D-035: element matrix rules.</summary>
    public sealed class ElementsConfig
    {
        public double StrongMult { get; set; }
        public double WeakMult { get; set; }
        public Dictionary<DarkFantasy.Core.Combat.Element, DarkFantasy.Core.Combat.Element> Beats { get; set; }
        public List<DarkFantasy.Core.Combat.Element> ResistsSelf { get; set; }
        public List<DarkFantasy.Core.Combat.Element> PhysicalWeakVs { get; set; }
        public double ResistMin { get; set; }
        public double ResistMax { get; set; }
    }

    /// <summary>D-017: class triangle.</summary>
    public sealed class TriangleConfig
    {
        public double AdvantageDealt { get; set; }
        public double AdvantageTaken { get; set; }
        public double DisadvantageDealt { get; set; }
        public double DisadvantageTaken { get; set; }
        public Dictionary<ClassId, DarkFantasy.Core.Combat.Archetype> ClassBeats { get; set; }
        public Dictionary<ClassId, DarkFantasy.Core.Combat.Archetype> ClassAs { get; set; }
    }

    /// <summary>D-030: save cadence.</summary>
    public sealed class SaveConfig
    {
        public double AutosaveIntervalS { get; set; }
    }

    public sealed class ArtifactConfig
    {
        public string Stat { get; set; }
        public double PerLevel { get; set; }
        public double CostBase { get; set; }
        public double CostGrowth { get; set; }
    }

    public sealed class PrestigeConfig
    {
        public int MinRegionBoss { get; set; }
        public double DustBase { get; set; }
        public double DustGrowthPerRegion { get; set; }
        public int FirstPrestigesCount { get; set; }
        public double FirstPrestigesDustMult { get; set; }
        public Dictionary<string, ArtifactConfig> Artifacts { get; set; }
    }

    /// <summary>Round-2 combat and system values (GDD section 4, DECISIONS R2-*).</summary>
    public sealed class CombatConfig
    {
        public double ReferenceIntervalS { get; set; }
        public double BaseIntervalFighter { get; set; }
        public double BaseIntervalArcher { get; set; }
        public double BaseIntervalMage { get; set; }
        public double AttackIntervalFloorS { get; set; }
        public double EscapeTimerS { get; set; }
        public int EscapesBeforeRetreat { get; set; }
        public double RetreatRetryS { get; set; }
        public double HastePerLevel { get; set; }
        public int HasteMaxLevel { get; set; }
        public double HasteCostBase { get; set; }
        public double HasteCostGrowth { get; set; }
        public double CooldownReductionCap { get; set; }
        public double ResistMin { get; set; }
        public double ResistMax { get; set; }
        public double HitChanceMin { get; set; }
        public double HitChanceMax { get; set; }
        public double LuckK { get; set; }
        public double EnemyRespawnS { get; set; }
        public double BossRepeatKillFraction { get; set; }

        /// <summary>Share of K kills needed again after a failed boss attempt (R2-2 as changed by the owner: 1.0 = full wave).</summary>
        public double BossRetryKillFraction { get; set; }
        public int InventoryBaseSlots { get; set; }
        public int InventorySlotsPerExpansion { get; set; }
        public int ItemLevelRequirementMargin { get; set; }
    }
}
