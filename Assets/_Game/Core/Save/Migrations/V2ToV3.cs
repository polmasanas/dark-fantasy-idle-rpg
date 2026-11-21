using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Save
{
    /// <summary>Schema 2 → 3 (M6): every hero gets empty skill ranks, an empty 3-slot loadout and auto-spend off.</summary>
    public sealed class V2ToV3 : IMigration
    {
        public int From => 2;

        public JObject Apply(JObject p)
        {
            if (p["heroes"] is JArray heroes)
                foreach (var h in heroes)
                {
                    if (!(h is JObject hero)) continue;
                    if (!(hero["skills"] is JObject)) hero["skills"] = new JObject();
                    if (!(hero["loadout"] is JArray)) hero["loadout"] = new JArray(null, null, null);
                    if (hero["autoSpendSkills"] == null) hero["autoSpendSkills"] = false;
                }
            return p;
        }
    }
}
