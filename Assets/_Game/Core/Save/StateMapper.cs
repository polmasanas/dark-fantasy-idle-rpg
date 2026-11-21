using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Save
{
    /// <summary>
    /// Explicit GameState ↔ JSON mapping. Enums are written by name and 64-bit values as strings, so
    /// reordering enums or adding fields never breaks old saves (SAVE_SYSTEM.md section 2).
    /// </summary>
    internal static class StateMapper
    {
        public static JObject ToJson(SaveData d)
        {
            var s = d.State;
            return new JObject
            {
                ["rng"] = new JObject { ["state"] = U(d.RngState), ["inc"] = U(d.RngIncrement) },
                ["gameTime"] = s.GameTime,
                ["account"] = new JObject { ["trainingBossDone"] = s.Account.TrainingBossDone, ["heroesUnlocked"] = s.Account.HeroesUnlocked },
                ["wallet"] = new JObject { ["gold"] = s.Wallet.Gold, ["materials"] = s.Wallet.Materials },
                ["heroes"] = Heroes(s),
                ["activeHero"] = s.ActiveHero,
                ["world"] = World(s.World),
                ["inventory"] = Inventory(s.Inventory),
                ["pendingOfflineLoot"] = Items(s.PendingOfflineLoot),
            };
        }

        private static JArray Heroes(GameState s)
        {
            var list = new JArray();
            foreach (var h in s.Heroes) list.Add(Character(h));
            return list;
        }

        public static SaveData FromJson(JObject p)
        {
            var state = GameState.NewRun(ClassId.Fighter);
            state.GameTime = (double)p["gameTime"];
            state.Account = new AccountState
            {
                TrainingBossDone = (bool)p["account"]["trainingBossDone"],
                HeroesUnlocked = (bool?)p["account"]["heroesUnlocked"] ?? false,
            };
            state.Wallet = new Wallet { Gold = (double)p["wallet"]["gold"], Materials = (double)p["wallet"]["materials"] };
            var heroes = (JArray)p["heroes"];
            foreach (var h in heroes)
            {
                var hero = Character((JObject)h);
                state.Heroes[(int)hero.Class] = hero; // slots are by class, whatever the order on disk
            }
            int active = (int)p["activeHero"];
            if (active < 0 || active >= state.Heroes.Count) throw new SaveFormatException(SaveError.Corrupt, "activeHero " + active + " is out of range");
            state.ActiveHero = active;
            state.World = World((JObject)p["world"]);
            state.Inventory = Inventory((JObject)p["inventory"]);
            state.PendingOfflineLoot = Items((JArray)p["pendingOfflineLoot"]);
            return new SaveData
            {
                State = state,
                RngState = ParseU((string)p["rng"]["state"]),
                RngIncrement = ParseU((string)p["rng"]["inc"]),
            };
        }

        internal static JObject Character(CharacterState c)
        {
            var equipped = new JObject();
            foreach (var kv in c.Equipped) equipped[kv.Key.ToString()] = L(kv.Value);
            var custom = new JObject();
            foreach (var kv in c.CustomWeights) custom[kv.Key.ToString()] = kv.Value;
            return new JObject
            {
                ["class"] = c.Class.ToString(),
                ["level"] = c.Level,
                ["xp"] = c.Xp,
                ["autoPoints"] = Points(c.AutoPoints),
                ["manualPoints"] = Points(c.ManualPoints),
                ["unspentPoints"] = c.UnspentPoints,
                ["preset"] = c.Preset,
                ["autoAllocateOnLevelUp"] = c.AutoAllocateOnLevelUp,
                ["customWeights"] = custom,
                ["equipped"] = equipped,
                ["skills"] = Ranks(c.SkillRanks),
                ["loadout"] = new JArray(c.Loadout),
                ["autoSpendSkills"] = c.AutoSpendSkills,
            };
        }

        private static CharacterState Character(JObject j)
        {
            var c = CharacterState.Create(E<ClassId>(j["class"]));
            c.Level = (int)j["level"];
            c.Xp = (double)j["xp"];
            ReadPoints((JObject)j["autoPoints"], c.AutoPoints);
            ReadPoints((JObject)j["manualPoints"], c.ManualPoints);
            c.UnspentPoints = (int)j["unspentPoints"];
            c.Preset = (string)j["preset"];
            c.AutoAllocateOnLevelUp = (bool?)j["autoAllocateOnLevelUp"] ?? false;
            if (j["customWeights"] is JObject custom)
                foreach (var p in custom.Properties()) c.CustomWeights[E<AttributeId>(p.Name)] = (int)p.Value;
            foreach (var p in ((JObject)j["equipped"]).Properties()) c.Equipped[E<EquipSlot>(p.Name)] = ParseL((string)p.Value);
            // Schema 3 (M6): ranks by node id; unknown ids are dropped later by SkillTree.Sanitize.
            if (j["skills"] is JObject skills)
                foreach (var p in skills.Properties())
                    if (p.Value.Type == JTokenType.Integer) c.SkillRanks[p.Name] = (int)p.Value;
            if (j["loadout"] is JArray loadout)
            {
                c.Loadout = new string[Math.Max(CharacterState.DefaultSkillSlots, loadout.Count)];
                for (int i = 0; i < loadout.Count; i++)
                    c.Loadout[i] = loadout[i].Type == JTokenType.String ? (string)loadout[i] : null;
            }
            c.AutoSpendSkills = (bool?)j["autoSpendSkills"] ?? false;
            return c;
        }

        private static JObject Ranks(Dictionary<string, int> ranks)
        {
            var o = new JObject();
            foreach (var kv in ranks) o[kv.Key] = kv.Value;
            return o;
        }

        private static JObject World(WorldState w) => new JObject
        {
            ["region"] = w.Region,
            ["unlockedSubzone"] = w.UnlockedSubzone,
            ["kills"] = new JArray(w.Kills),
            ["fightBossWhenAvailable"] = w.FightBossWhenAvailable,
            ["highestBossDefeated"] = w.HighestBossDefeated,
            ["consecutiveEscapes"] = w.ConsecutiveEscapes,
            ["retreatUntil"] = w.RetreatUntil,
            ["trainingBossPending"] = w.TrainingBossPending,
        };

        private static WorldState World(JObject j) => new WorldState
        {
            Region = (int)j["region"],
            UnlockedSubzone = (int)j["unlockedSubzone"],
            Kills = ((JArray)j["kills"]).ToObject<int[]>(),
            FightBossWhenAvailable = (bool)j["fightBossWhenAvailable"],
            HighestBossDefeated = (int)j["highestBossDefeated"],
            ConsecutiveEscapes = (int)j["consecutiveEscapes"],
            RetreatUntil = (double)j["retreatUntil"],
            TrainingBossPending = (bool)j["trainingBossPending"],
        };

        private static JObject Inventory(InventoryState inv) => new JObject
        {
            ["nextId"] = L(inv.NextId),
            ["capacity"] = inv.Capacity,
            ["expansions"] = inv.Expansions,
            ["autoSalvage"] = inv.AutoSalvage.ToString(),
            ["items"] = Items(inv.Items),
        };

        private static InventoryState Inventory(JObject j)
        {
            var inv = new InventoryState
            {
                NextId = ParseL((string)j["nextId"]),
                Capacity = (int)j["capacity"],
                Expansions = (int)j["expansions"],
                AutoSalvage = E<AutoSalvageMode>(j["autoSalvage"]),
                Items = Items((JArray)j["items"]),
            };
            return inv; // WornIds are rebuilt by the session from the characters' equipment
        }

        private static JArray Items(List<ItemInstance> items)
        {
            var arr = new JArray();
            foreach (var i in items)
            {
                var attrs = new JArray();
                foreach (var a in i.Attributes)
                    attrs.Add(new JObject { ["id"] = a.AttributeId, ["stat"] = a.Stat.ToString(), ["value"] = a.Value });
                arr.Add(new JObject
                {
                    ["id"] = L(i.Id),
                    ["kind"] = i.Kind.ToString(),
                    ["type"] = i.TypeId,
                    ["itemLevel"] = i.ItemLevel,
                    ["rarity"] = i.Rarity.ToString(),
                    ["upgrade"] = i.Upgrade,
                    ["attributes"] = attrs,
                    ["classBonus"] = i.ClassBonus.HasValue ? i.ClassBonus.Value.ToString() : null,
                    ["locked"] = i.Locked,
                    ["goldSpent"] = i.GoldSpent,
                });
            }
            return arr;
        }

        private static List<ItemInstance> Items(JArray arr)
        {
            var list = new List<ItemInstance>();
            if (arr == null) return list;
            foreach (JObject j in arr)
            {
                var item = new ItemInstance
                {
                    Id = ParseL((string)j["id"]),
                    Kind = E<ItemKind>(j["kind"]),
                    TypeId = (string)j["type"],
                    ItemLevel = (int)j["itemLevel"],
                    Rarity = E<Rarity>(j["rarity"]),
                    Upgrade = (int)j["upgrade"],
                    Locked = (bool)j["locked"],
                    GoldSpent = (double)j["goldSpent"],
                };
                string cls = (string)j["classBonus"];
                if (cls != null) item.ClassBonus = E<ClassId>(cls);
                foreach (JObject a in (JArray)j["attributes"])
                    item.Attributes.Add(new RolledAttribute((string)a["id"], E<GearStat>(a["stat"]), (double)a["value"]));
                list.Add(item);
            }
            return list;
        }

        private static JObject Points(Dictionary<AttributeId, int> points)
        {
            var o = new JObject();
            foreach (var kv in points) o[kv.Key.ToString()] = kv.Value;
            return o;
        }

        private static void ReadPoints(JObject j, Dictionary<AttributeId, int> into)
        {
            foreach (var p in j.Properties()) into[E<AttributeId>(p.Name)] = (int)p.Value;
        }

        private static T E<T>(JToken token) where T : struct, Enum => E<T>((string)token);

        private static T E<T>(string name) where T : struct, Enum
        {
            if (name == null || !Enum.TryParse(name, false, out T value) || !Enum.IsDefined(typeof(T), value))
                throw new FormatException($"Unknown {typeof(T).Name} '{name}'");
            return value;
        }

        private static string U(ulong v) => v.ToString(CultureInfo.InvariantCulture);
        private static ulong ParseU(string s) => ulong.Parse(s, CultureInfo.InvariantCulture);
        private static string L(long v) => v.ToString(CultureInfo.InvariantCulture);
        private static long ParseL(string s) => long.Parse(s, CultureInfo.InvariantCulture);
    }
}
