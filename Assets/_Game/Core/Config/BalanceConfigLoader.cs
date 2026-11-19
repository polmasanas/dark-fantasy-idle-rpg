using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Config
{
    /// <summary>Thrown when balance_config.json is malformed or misses a required key.</summary>
    public sealed class BalanceConfigException : Exception
    {
        public BalanceConfigException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Reads balance_config.json into a <see cref="BalanceConfig"/>. Every key is required, so a
    /// missing value fails loudly at boot instead of silently becoming 0 in a formula.
    /// </summary>
    public static class BalanceConfigLoader
    {
        public static BalanceConfig FromJson(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new BalanceConfigException("balance config is not valid JSON: " + e.Message, e);
            }

            var w = Section(root, "world");
            var e1 = Section(root, "enemy");
            var p = Section(root, "player");
            var x = Section(root, "xp");
            var g = Section(root, "gear");
            var ec = Section(root, "economy");
            var m = Section(root, "mastery");
            var o = Section(root, "offline");
            var pr = Section(root, "prestige");
            var cb = Section(root, "combat");

            return new BalanceConfig
            {
                World = new WorldConfig
                {
                    SubzonesPerRegion = I(w, "world", "subzones_per_region"),
                    LevelsPerSubzone = I(w, "world", "levels_per_subzone"),
                    KillsToAdvanceBase = I(w, "world", "kills_to_advance_base"),
                    KillsToAdvancePerRegion = I(w, "world", "kills_to_advance_per_region"),
                    KillsToAdvanceCap = I(w, "world", "kills_to_advance_cap"),
                },
                Enemy = new EnemyConfig
                {
                    HpBase = D(e1, "enemy", "hp_base"),
                    HpGrowth = D(e1, "enemy", "hp_growth"),
                    AtkBase = D(e1, "enemy", "atk_base"),
                    AtkGrowth = D(e1, "enemy", "atk_growth"),
                    AttackIntervalS = D(e1, "enemy", "attack_interval_s"),
                    BossHpMult = D(e1, "enemy", "boss_hp_mult"),
                    BossAtkMult = D(e1, "enemy", "boss_atk_mult"),
                    BossTimeLimitS = D(e1, "enemy", "boss_time_limit_s"),
                    VarianceMultMin = D(e1, "enemy", "variance_mult_min"),
                    VarianceMultMax = D(e1, "enemy", "variance_mult_max"),
                },
                Player = new PlayerConfig
                {
                    PointsPerLevelAuto = I(p, "player", "points_per_level_auto"),
                    PointsPerLevelManual = I(p, "player", "points_per_level_manual"),
                    BaseHp = D(p, "player", "base_hp"),
                    HpPerVitPct = D(p, "player", "hp_per_vit_pct"),
                    DmgPerPrimaryPct = D(p, "player", "dmg_per_primary_pct"),
                    ArmourPctPerDef = D(p, "player", "armour_pct_per_def"),
                    BaseHitChance = D(p, "player", "base_hit_chance"),
                    BaseCritChance = D(p, "player", "base_crit_chance"),
                    CritChancePerPoint = D(p, "player", "crit_chance_per_point"),
                    CritChanceCap = D(p, "player", "crit_chance_cap"),
                    BaseCritDamage = D(p, "player", "base_crit_damage"),
                    CritDamagePerPoint = D(p, "player", "crit_damage_per_point"),
                    ArmourKBase = D(p, "player", "armour_k_base"),
                    ArmourKGrowth = D(p, "player", "armour_k_growth"),
                    DamageReductionCap = D(p, "player", "damage_reduction_cap"),
                    BetweenFightS = D(p, "player", "between_fight_s"),
                    CounterChancePerPoint = D(p, "player", "counter_chance_per_point"),
                    CounterCap = D(p, "player", "counter_cap"),
                    CounterDamage = D(p, "player", "counter_damage"),
                    CatchupXpBonus = D(p, "player", "catchup_xp_bonus"),
                },
                Xp = new XpConfig
                {
                    EnemyXpBase = D(x, "xp", "enemy_xp_base"),
                    EnemyXpGrowth = D(x, "xp", "enemy_xp_growth"),
                    LevelReqGrowth = D(x, "xp", "level_req_growth"),
                    KillsPerLevelBase = D(x, "xp", "kills_per_level_base"),
                    KillsPerLevelPerLevel = D(x, "xp", "kills_per_level_per_level"),
                    BossXpMult = D(x, "xp", "boss_xp_mult"),
                    OverlevelPenaltyPerLevel = D(x, "xp", "overlevel_penalty_per_level"),
                    OverlevelPenaltyFloor = D(x, "xp", "overlevel_penalty_floor"),
                },
                Gear = new GearConfig
                {
                    WeaponDmgBase = D(g, "gear", "weapon_dmg_base"),
                    OffhandDmgShare = D(g, "gear", "offhand_dmg_share"),
                    ArmourBase = D(g, "gear", "armour_base"),
                    ArmourHpBase = D(g, "gear", "armour_hp_base"),
                    ItemGrowth = D(g, "gear", "item_growth"),
                    UpgradeBasePct = D(g, "gear", "upgrade_base_pct"),
                    UpgradeMaxByRarity = Arr<int>(g, "gear", "upgrade_max_by_rarity"),
                    RarityNames = Arr<string>(g, "gear", "rarity_names"),
                    RarityBaseMult = Arr<double>(g, "gear", "rarity_base_mult"),
                    RarityWeights = Arr<double>(g, "gear", "rarity_weights"),
                    RandomAffixes = Arr<int>(g, "gear", "random_affixes"),
                    AffixValuePct = D(g, "gear", "affix_value_pct"),
                    DropChanceNormal = D(g, "gear", "drop_chance_normal"),
                    BossMinRarity = I(g, "gear", "boss_min_rarity"),
                },
                Economy = new EconomyConfig
                {
                    GoldBase = D(ec, "economy", "gold_base"),
                    GoldGrowth = D(ec, "economy", "gold_growth"),
                    BossGoldMult = D(ec, "economy", "boss_gold_mult"),
                    UpgradeCostBase = D(ec, "economy", "upgrade_cost_base"),
                    UpgradeCostGrowth = D(ec, "economy", "upgrade_cost_growth"),
                    SalvageUpgradeRefund = D(ec, "economy", "salvage_upgrade_refund"),
                    CraftCostBase = D(ec, "economy", "craft_cost_base"),
                    CraftCostPerIlvl = D(ec, "economy", "craft_cost_per_ilvl"),
                    ScrollDropChance = D(ec, "economy", "scroll_drop_chance"),
                },
                Mastery = new MasteryConfig
                {
                    KillsPerStar = I(m, "mastery", "kills_per_star"),
                    MaxStars = I(m, "mastery", "max_stars"),
                    GoldPerStar = D(m, "mastery", "gold_per_star"),
                    DropPerStar = D(m, "mastery", "drop_per_star"),
                },
                Offline = new OfflineConfig
                {
                    Efficiency = D(o, "offline", "efficiency"),
                    CapHours = D(o, "offline", "cap_hours"),
                },
                Prestige = new PrestigeConfig
                {
                    MinRegionBoss = I(pr, "prestige", "min_region_boss"),
                    DustBase = D(pr, "prestige", "dust_base"),
                    DustGrowthPerRegion = D(pr, "prestige", "dust_growth_per_region"),
                    FirstPrestigesCount = I(pr, "prestige", "first_prestiges_count"),
                    FirstPrestigesDustMult = D(pr, "prestige", "first_prestiges_dust_mult"),
                    Artifacts = Artifacts(pr),
                },
                Combat = new CombatConfig
                {
                    ReferenceIntervalS = D(cb, "combat", "reference_interval_s"),
                    BaseIntervalFighter = D(cb, "combat", "base_interval_fighter"),
                    BaseIntervalArcher = D(cb, "combat", "base_interval_archer"),
                    BaseIntervalMage = D(cb, "combat", "base_interval_mage"),
                    AttackIntervalFloorS = D(cb, "combat", "attack_interval_floor_s"),
                    EscapeTimerS = D(cb, "combat", "escape_timer_s"),
                    EscapesBeforeRetreat = I(cb, "combat", "escapes_before_retreat"),
                    RetreatRetryS = D(cb, "combat", "retreat_retry_s"),
                    HastePerLevel = D(cb, "combat", "haste_per_level"),
                    HasteMaxLevel = I(cb, "combat", "haste_max_level"),
                    HasteCostBase = D(cb, "combat", "haste_cost_base"),
                    HasteCostGrowth = D(cb, "combat", "haste_cost_growth"),
                    CooldownReductionCap = D(cb, "combat", "cooldown_reduction_cap"),
                    ResistMin = D(cb, "combat", "resist_min"),
                    ResistMax = D(cb, "combat", "resist_max"),
                    HitChanceMin = D(cb, "combat", "hit_chance_min"),
                    HitChanceMax = D(cb, "combat", "hit_chance_max"),
                    LuckK = D(cb, "combat", "luck_k"),
                    EnemyRespawnS = D(cb, "combat", "enemy_respawn_s"),
                    BossRetryS = D(cb, "combat", "boss_retry_s"),
                    BossRepeatKillFraction = D(cb, "combat", "boss_repeat_kill_fraction"),
                },
                Classes = Classes(Section(root, "classes")),
                AllocationPresets = Presets(Section(root, "allocation_presets")),
            };
        }

        private static Dictionary<string, ArtifactConfig> Artifacts(JObject prestige)
        {
            var arts = Section(prestige, "artifacts", "prestige.artifacts");
            var result = new Dictionary<string, ArtifactConfig>();
            foreach (var prop in arts.Properties())
            {
                if (!(prop.Value is JObject a))
                    throw new BalanceConfigException("prestige.artifacts." + prop.Name + " must be an object");
                string path = "prestige.artifacts." + prop.Name;
                result[prop.Name] = new ArtifactConfig
                {
                    Stat = Req(a, path, "stat").Value<string>(),
                    PerLevel = D(a, path, "per_level"),
                    CostBase = D(a, path, "cost_base"),
                    CostGrowth = D(a, path, "cost_growth"),
                };
            }
            return result;
        }

        private static Dictionary<ClassId, ClassConfig> Classes(JObject classes)
        {
            var result = new Dictionary<ClassId, ClassConfig>();
            foreach (ClassId cls in Enum.GetValues(typeof(ClassId)))
            {
                string key = cls.ToString().ToLowerInvariant();
                string path = "classes." + key;
                var obj = Section(classes, key, path);
                if (!(Req(obj, path, "growth_pattern") is JArray rows) || rows.Count != 4)
                    throw new BalanceConfigException(path + ".growth_pattern must be an array of 4 rows");
                var pattern = new AttributeId[4][];
                for (int i = 0; i < 4; i++)
                {
                    if (!(rows[i] is JArray row) || row.Count != 3)
                        throw new BalanceConfigException(path + ".growth_pattern row " + i + " must have 3 attributes");
                    pattern[i] = new AttributeId[3];
                    for (int j = 0; j < 3; j++)
                        pattern[i][j] = Attribute((string)row[j], path + ".growth_pattern");
                }
                result[cls] = new ClassConfig
                {
                    Primary = Attribute(Req(obj, path, "primary").Value<string>(), path + ".primary"),
                    GrowthPattern = pattern,
                };
            }
            return result;
        }

        private static Dictionary<string, Dictionary<string, double>> Presets(JObject presets)
        {
            var result = new Dictionary<string, Dictionary<string, double>>();
            foreach (var prop in presets.Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                if (!(prop.Value is JObject shares))
                    throw new BalanceConfigException("allocation_presets." + prop.Name + " must be an object");
                var map = new Dictionary<string, double>();
                foreach (var share in shares.Properties())
                {
                    if (share.Name != "primary") Attribute(share.Name, "allocation_presets." + prop.Name);
                    map[share.Name] = D(shares, "allocation_presets." + prop.Name, share.Name);
                }
                result[prop.Name] = map;
            }
            return result;
        }

        private static AttributeId Attribute(string key, string path)
        {
            if (key == null || !AttributeIds.TryParse(key, out var a))
                throw new BalanceConfigException(path + ": unknown attribute '" + key + "'");
            return a;
        }

        private static JObject Section(JObject parent, string key, string path = null)
        {
            if (!(parent[key] is JObject obj))
                throw new BalanceConfigException("balance config is missing section '" + (path ?? key) + "'");
            return obj;
        }

        private static JToken Req(JObject obj, string path, string key)
        {
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
                throw new BalanceConfigException("balance config is missing key '" + path + "." + key + "'");
            return token;
        }

        private static double D(JObject obj, string path, string key)
        {
            var t = Req(obj, path, key);
            if (t.Type != JTokenType.Float && t.Type != JTokenType.Integer)
                throw new BalanceConfigException("balance config key '" + path + "." + key + "' must be a number");
            return t.Value<double>();
        }

        private static int I(JObject obj, string path, string key)
        {
            var t = Req(obj, path, key);
            if (t.Type != JTokenType.Integer)
                throw new BalanceConfigException("balance config key '" + path + "." + key + "' must be an integer");
            return t.Value<int>();
        }

        private static T[] Arr<T>(JObject obj, string path, string key)
        {
            if (!(Req(obj, path, key) is JArray arr))
                throw new BalanceConfigException("balance config key '" + path + "." + key + "' must be an array");
            return arr.ToObject<T[]>();
        }
    }
}
