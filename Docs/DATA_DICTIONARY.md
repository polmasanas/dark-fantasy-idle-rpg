# DATA_DICTIONARY.md

> **Status:** v0.1 DRAFT. Definitions = static content (ScriptableObjects → Core records). State = saved runtime data.
> Conventions: IDs are lowercase `kind.name` strings (`enemy.ghoul`, `item.rusty_sword`). Numbers are `double` unless noted. Percentages are stored as fractions (0.15 = 15%).

## 1. Enumerations (serialised as strings)

| Enum | Values |
|---|---|
| `ClassId` | Fighter, Archer, Mage (data-driven list; enum used only for the triangle) |
| `Archetype` | Brute, Marksman, Caster |
| `Element` | Physical, Fire, Water, Nature, Light, Dark |
| `Family` | Humanoid, Beast, Undead, Demon, Elemental, Construct, Dragon (extensible: stored as ID) |
| `Rarity` | Common, Uncommon, Rare, Epic, Legendary, Mythical |
| `SlotId` | MainHand, OffHand, Helmet, Chest, Legs, Boots, Armguards, Gloves, Ring1, Ring2, Amulet, Pet, Accessory1–6 |
| `StatId` | Str, Dex, Int, Vit, Def, CritChance, CritDamage, Luck, PctDamage, PctHp, AttackSpeed, Accuracy, Evasion, ElemDamage[e], Resist[e], VsFamily[f], GoldFind, DropRate, … (data-driven `AttributeDefinition`) |
| `Currency` | Gold, Materials, Gems, ArtifactDust, DungeonToken(dungeonId), PetEssence, PetShard(petId) |

## 2. Definitions

| Definition | Key fields |
|---|---|
| `BalanceConfig` | All values in `Tools/BalanceSim/balance_config.json` (1:1 field names) |
| `AttributeDefinition` | id, displayName, iconId, perPoint effects `[{stat, value}]`, isPrimaryFor[] |
| `ClassDefinition` | id, primaryAttribute, autoGrowthTemplate `{attr: points per 4 levels}`, baseAttackInterval, starterWeaponId, skillTreeIds[4], archetype |
| `RegionDefinition` | id, index, name, biomeId, subzones[5], bossId, unlockRequirement, dungeonId? |
| `SubzoneDefinition` | id, index, enemyPool `[{enemyId, weight}]`, lootTableId, killsToAdvance (override optional) |
| `EnemyDefinition` | id, familyId, element, archetype, hpMult, atkMult, evasion, resistances `{element: fraction}`, isBoss, spriteSetId |
| `FamilyDefinition` | id, name, default traits, damageBonusStatId (for "vs family" gear) |
| `ItemDefinition` | id, slot, classRestriction? (bonus only), baseLines `[{stat, baseValue, scaling: Exponential/Linear/None}]`, affixPoolId, enchantPoolId, tags (e.g. `prestigeExclusive`, `tradeable`) |
| `AffixDefinition` | id, stat, minRoll, maxRoll (fraction of tier value), weight, allowedSlots[], minRarity |
| `LootTable` | id, entries `[{type: Item/Currency/Pet/Scroll/Table, ref, weight or chance, min, max}]`, mode (Weighted / Independent chances) |
| `PetDefinition` | id, rarity, passive `[{stat, perLevel}]`, activeEffectId?, maxLevel, shardBreakpoints[] |
| `SkillDefinition` | id, branchId, tier, maxRank, type (Passive/Active), cooldown, effects `[EffectSpec]`, requirements |
| `EffectSpec` | kind (Damage, StatMod, DoT, Heal, ExtraAttack, Cleave, …), element, scaling, chance, duration |
| `ArtifactDefinition` | id, effect `{stat, perLevel, compounding}`, costBase, costGrowth, maxLevel? |
| `DungeonDefinition` | id, unlockRegion, waves[], bossId, tokenId, chestTable, choosePriceFactor (default 1.0) |
| `AchievementDefinition` | id, condition (event + threshold), gemReward |

## 3. Runtime state (saved)

| Model | Fields | Survives prestige |
|---|---|---|
| `GameState` | schemaVersion, account, run, rng, lastSavedUtc, playtime | — |
| `AccountState` | gems, dust, artifactLevels, permanentScrolls `{attr: count}`, pets, prestigeItems, dungeonTokens, unlockedBranches, subzoneMastery, achievements, cosmetics, settings, statistics, prestigeCount, bestRegion | **Yes** |
| `RunState` | activeCharacterId, characters, inventory, world, gold, materials | **No** (replaced) |
| `CharacterState` | classId, level, xp, autoPoints, manualPoints, unspentPoints, skillRanks, skillLoadout[3], equipped `{slot: itemId}` | No |
| `ItemInstance` | id (GUID), definitionId, itemLevel (int), rarity, upgradeLevel (int), affixes `[{affixId, value}]`, enchant?, locked, origin (Drop/Craft/Dungeon/Trade), createdUtc | No (unless prestige-exclusive → account) |
| `PetInstance` | definitionId, level, stars, equippedBy? | Yes |
| `WorldProgress` | unlockedRegion, current `{region, subzone}`, killsBySubzone, bossesDefeated, fightBossWhenAvailable (bool), farmTarget | No |
| `Settings` | animationsEnabled, battery mode, fps, autoSalvageFilter, language, audio | Yes |
| `RngState` | PCG32 state + increment (ulong ×2) | Yes |

## 4. Events (Core)
`EnemyKilled{enemyId, level, region, subzone}`, `BossDefeated{region, firstTime}`, `ItemDropped{itemId, rarity}`, `ItemEquipped{characterId, slot, itemId}`, `LevelUp{characterId, level}`, `CurrencyChanged{currency, delta, reason}`, `PrestigeCompleted{dust, count}`, `OfflineReportReady{report}`, `StatsDirty{characterId}`.
