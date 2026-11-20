using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Items;
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

            var config = new BalanceConfig
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
                    SlotDropWeights = Section(g, "slot_drop_weights", "gear.slot_drop_weights").ToObject<Dictionary<string, double>>(),
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
                    MaterialPerSalvage = Arr<double>(ec, "economy", "material_per_salvage"),
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
                    BossRepeatKillFraction = D(cb, "combat", "boss_repeat_kill_fraction"),
                    BossRetryKillFraction = D(cb, "combat", "boss_retry_kill_fraction"),
                    InventoryBaseSlots = I(cb, "combat", "inventory_base_slots"),
                    InventorySlotsPerExpansion = I(cb, "combat", "inventory_slots_per_expansion"),
                    ItemLevelRequirementMargin = I(cb, "combat", "item_level_requirement_margin"),
                },
                Classes = Classes(Section(root, "classes")),
                AllocationPresets = Presets(Section(root, "allocation_presets")),
                Items = Items(Section(root, "items")),
                Onboarding = Onboarding(Section(root, "onboarding")),
                OfflineRules = OfflineRules(Section(root, "offline_rules")),
                Elements = ElementRules(Section(root, "elements")),
                Triangle = TriangleRules(Section(root, "triangle")),
                Families = Families(Section(root, "families")),
                Save = new SaveConfig
                {
                    AutosaveIntervalS = D(Section(root, "save"), "save", "autosave_interval_s"),
                },
                AllocationRules = new AllocationRulesConfig
                {
                    CustomWeightMax = I(Section(root, "allocation_rules"), "allocation_rules", "custom_weight_max"),
                },
            };
            config.Regions = Regions(Section(root, "regions"), config.Families);
            return config;
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

        private static ItemsConfig Items(JObject items)
        {
            var typesSection = Section(items, "types", "items.types");
            var types = new Dictionary<ItemKind, List<ItemTypeConfig>>();
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                bool armour = kind >= ItemKind.Helmet && kind <= ItemKind.Gloves;
                string key = armour ? "armour" : ItemKeys.Kind(kind);
                string path = "items.types." + key;
                if (!(Req(typesSection, "items.types", key) is JArray list))
                    throw new BalanceConfigException(path + " must be an array");
                var result = new List<ItemTypeConfig>();
                foreach (JObject t in list)
                {
                    string id = Req(t, path, "id").Value<string>();
                    var lines = new List<StatLineConfig>();
                    foreach (JObject l in (JArray)Req(t, path + "." + id, "lines"))
                    {
                        string lp = path + "." + id + ".lines";
                        lines.Add(new StatLineConfig
                        {
                            Stat = Stat(Req(l, lp, "stat").Value<string>(), lp),
                            Base = D(l, lp, "base"),
                            Scaling = Scaling(Req(l, lp, "scaling").Value<string>(), lp),
                            PerItemLevel = D(l, lp, "per_item_level"),
                        });
                    }
                    result.Add(new ItemTypeConfig { Id = id, Name = Req(t, path, "name").Value<string>(), Lines = lines });
                }
                types[kind] = result;
            }

            var attributes = new List<AttributeConfig>();
            if (!(Req(items, "items", "attributes") is JArray pool))
                throw new BalanceConfigException("items.attributes must be an array");
            foreach (JObject a in pool)
            {
                string id = Req(a, "items.attributes", "id").Value<string>();
                string path = "items.attributes." + id;
                var kinds = new List<ItemKind>();
                foreach (var k in (JArray)Req(a, path, "kinds"))
                {
                    if (!ItemKeys.TryKind((string)k, out var kind))
                        throw new BalanceConfigException(path + ": unknown item kind '" + k + "'");
                    kinds.Add(kind);
                }
                attributes.Add(new AttributeConfig
                {
                    Id = id,
                    Stat = Stat(Req(a, path, "stat").Value<string>(), path),
                    Min = D(a, path, "min"),
                    Max = D(a, path, "max"),
                    PerItemLevel = D(a, path, "per_item_level"),
                    Kinds = kinds,
                });
            }

            return new ItemsConfig
            {
                Types = types,
                Attributes = attributes,
                ClassItemShare = D(items, "items", "class_item_share"),
                ClassBonusPctDamage = D(items, "items", "class_bonus_pct_damage"),
                RarityRollBias = D(items, "items", "rarity_roll_bias"),
                ExpansionCostBase = D(items, "items", "expansion_cost_base"),
                ExpansionCostGrowth = D(items, "items", "expansion_cost_growth"),
            };
        }

        private static ElementsConfig ElementRules(JObject o)
        {
            var beats = new Dictionary<Combat.Element, Combat.Element>();
            foreach (var prop in Section(o, "beats", "elements.beats").Properties())
                beats[ElementKey(prop.Name, "elements.beats")] = ElementKey((string)prop.Value, "elements.beats." + prop.Name);
            return new ElementsConfig
            {
                StrongMult = D(o, "elements", "strong_mult"),
                WeakMult = D(o, "elements", "weak_mult"),
                Beats = beats,
                ResistsSelf = ElementList(o, "resists_self"),
                PhysicalWeakVs = ElementList(o, "physical_weak_vs"),
                ResistMin = D(o, "elements", "resist_min"),
                ResistMax = D(o, "elements", "resist_max"),
            };
        }

        private static List<Combat.Element> ElementList(JObject o, string key)
        {
            if (!(Req(o, "elements", key) is JArray list)) throw new BalanceConfigException("elements." + key + " must be a list");
            var result = new List<Combat.Element>();
            for (int i = 0; i < list.Count; i++) result.Add(ElementKey((string)list[i], "elements." + key + "[" + i + "]"));
            return result;
        }

        internal static Combat.Element ElementKey(string key, string path)
        {
            if (key == null || !Combat.Elements.TryParse(key, out var e))
                throw new BalanceConfigException(path + ": unknown element '" + key + "'");
            return e;
        }

        internal static Combat.Archetype ArchetypeKey(string key, string path)
        {
            if (key != null)
                foreach (Combat.Archetype a in Enum.GetValues(typeof(Combat.Archetype)))
                    if (string.Equals(a.ToString(), key, StringComparison.OrdinalIgnoreCase)) return a;
            throw new BalanceConfigException(path + ": unknown archetype '" + key + "'");
        }

        private static TriangleConfig TriangleRules(JObject o)
        {
            Dictionary<ClassId, Combat.Archetype> PerClass(string key)
            {
                var section = Section(o, key, "triangle." + key);
                var map = new Dictionary<ClassId, Combat.Archetype>();
                foreach (ClassId cls in Enum.GetValues(typeof(ClassId)))
                {
                    string k = cls.ToString().ToLowerInvariant();
                    map[cls] = ArchetypeKey((string)Req(section, "triangle." + key, k), "triangle." + key + "." + k);
                }
                return map;
            }
            return new TriangleConfig
            {
                AdvantageDealt = D(o, "triangle", "advantage_dealt"),
                AdvantageTaken = D(o, "triangle", "advantage_taken"),
                DisadvantageDealt = D(o, "triangle", "disadvantage_dealt"),
                DisadvantageTaken = D(o, "triangle", "disadvantage_taken"),
                ClassBeats = PerClass("class_beats"),
                ClassAs = PerClass("class_as"),
            };
        }

        private static Dictionary<string, FamilyConfig> Families(JObject o)
        {
            var result = new Dictionary<string, FamilyConfig>();
            foreach (var prop in o.Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                string path = "families." + prop.Name;
                var f = prop.Value as JObject ?? throw new BalanceConfigException(path + " must be an object");
                var pool = new Dictionary<Combat.Element, double>();
                foreach (var e in Section(f, "element_pool", path + ".element_pool").Properties())
                    pool[ElementKey(e.Name, path + ".element_pool")] = D((JObject)f["element_pool"], path + ".element_pool", e.Name);
                var resist = new Dictionary<Combat.Element, double>();
                foreach (var e in Section(f, "resist", path + ".resist").Properties())
                    resist[ElementKey(e.Name, path + ".resist")] = D((JObject)f["resist"], path + ".resist", e.Name);
                if (!(Req(f, path, "archetypes") is JArray arch) || arch.Count == 0)
                    throw new BalanceConfigException(path + ".archetypes must be a non-empty list");
                var archetypes = new Combat.Archetype[arch.Count];
                for (int i = 0; i < arch.Count; i++) archetypes[i] = ArchetypeKey((string)arch[i], path + ".archetypes[" + i + "]");
                var elementProp = f.Property("element") ?? throw new BalanceConfigException("balance config is missing key '" + path + ".element' (use null for none)");
                var element = elementProp.Value;
                var groupProp = f.Property("group") ?? throw new BalanceConfigException("balance config is missing key '" + path + ".group' (use null for none)");
                result[prop.Name] = new FamilyConfig
                {
                    Id = prop.Name,
                    Group = groupProp.Value.Type == JTokenType.Null ? null : (string)groupProp.Value,
                    Element = element.Type == JTokenType.Null ? (Combat.Element?)null : ElementKey((string)element, path + ".element"),
                    ElementPool = pool,
                    ResistOwn = D(f, path, "resist_own"),
                    Resist = resist,
                    Evasion = D(f, path, "evasion"),
                    ElementalLineMult = D(f, path, "elemental_line_mult"),
                    Archetypes = archetypes,
                    BossArchetype = ArchetypeKey((string)Req(f, path, "boss_archetype"), path + ".boss_archetype"),
                };
            }
            return result;
        }

        private static RegionsConfig Regions(JObject o, Dictionary<string, FamilyConfig> families)
        {
            string Known(string id, string path)
            {
                if (id == null || !families.ContainsKey(id)) throw new BalanceConfigException(path + ": unknown family '" + id + "'");
                return id;
            }
            if (!(Req(o, "regions", "subzone_families") is JArray subs)) throw new BalanceConfigException("regions.subzone_families must be a list");
            var subzoneFamilies = new List<string[]>();
            for (int r = 0; r < subs.Count; r++)
            {
                if (!(subs[r] is JArray row) || row.Count == 0) throw new BalanceConfigException("regions.subzone_families[" + r + "] must be a non-empty list");
                var ids = new string[row.Count];
                for (int s = 0; s < row.Count; s++) ids[s] = Known((string)row[s], "regions.subzone_families[" + r + "][" + s + "]");
                subzoneFamilies.Add(ids);
            }
            if (!(Req(o, "regions", "boss_families") is JArray bosses)) throw new BalanceConfigException("regions.boss_families must be a list");
            var bossFamilies = new List<string>();
            for (int r = 0; r < bosses.Count; r++) bossFamilies.Add(Known((string)bosses[r], "regions.boss_families[" + r + "]"));
            return new RegionsConfig
            {
                SubzoneFamilies = subzoneFamilies,
                BossFamilies = bossFamilies,
                FallbackFamily = Known((string)Req(o, "regions", "fallback_family"), "regions.fallback_family"),
            };
        }

        private static OfflineRulesConfig OfflineRules(JObject o) => new OfflineRulesConfig
        {
            ReportMinSeconds = D(o, "offline_rules", "report_min_seconds"),
            ClockToleranceS = D(o, "offline_rules", "clock_tolerance_s"),
        };

        private static OnboardingConfig Onboarding(JObject o)
        {
            var overrides = new Dictionary<int, double>();
            foreach (var prop in Section(o, "boss_hp_mult_overrides", "onboarding.boss_hp_mult_overrides").Properties())
            {
                if (!int.TryParse(prop.Name, out int region))
                    throw new BalanceConfigException("onboarding.boss_hp_mult_overrides: '" + prop.Name + "' is not a region number");
                overrides[region] = D((JObject)o["boss_hp_mult_overrides"], "onboarding.boss_hp_mult_overrides", prop.Name);
            }
            var enabled = Req(o, "onboarding", "training_boss_enabled");
            if (enabled.Type != JTokenType.Boolean)
                throw new BalanceConfigException("onboarding.training_boss_enabled must be true or false");
            return new OnboardingConfig
            {
                TrainingBossEnabled = enabled.Value<bool>(),
                TrainingBossAfterSubzone = I(o, "onboarding", "training_boss_after_subzone"),
                TrainingBossHpMult = D(o, "onboarding", "training_boss_hp_mult"),
                BossHpMultOverrides = overrides,
            };
        }

        private static GearStat Stat(string key, string path)
        {
            if (key == null || !ItemKeys.TryStat(key, out var s))
                throw new BalanceConfigException(path + ": unknown stat '" + key + "'");
            return s;
        }

        private static StatLineScaling Scaling(string key, string path)
        {
            if (key == null || !ItemKeys.TryScaling(key, out var s))
                throw new BalanceConfigException(path + ": unknown scaling '" + key + "'");
            return s;
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
