using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Save
{
    /// <summary>
    /// Schema 1 → 2 (M5): the single character becomes the Fighter in a list of three heroes; the Archer and
    /// Mage start at level 1 with no gear (their starter weapons are added when the session starts);
    /// the heroes count as unlocked when the region 1 boss was already beaten (M5-4).
    /// </summary>
    public sealed class V1ToV2 : IMigration
    {
        public int From => 1;

        public JObject Apply(JObject p)
        {
            var heroes = new JArray
            {
                p["character"],
                StateMapper.Character(CharacterState.Create(ClassId.Archer)),
                StateMapper.Character(CharacterState.Create(ClassId.Mage)),
            };
            p.Remove("character");
            p["heroes"] = heroes;
            p["activeHero"] = 0;
            var account = p["account"] as JObject ?? new JObject();
            account["heroesUnlocked"] = ((int?)p["world"]?["highestBossDefeated"] ?? 0) >= 1;
            p["account"] = account;
            return p;
        }
    }
}
