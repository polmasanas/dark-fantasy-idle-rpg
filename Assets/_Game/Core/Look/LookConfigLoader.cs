using System;
using System.Collections.Generic;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Look
{
    /// <summary>Reads look_config.json; every problem is reported with its JSON path.</summary>
    public static class LookConfigLoader
    {
        public static LookConfig FromJson(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new LookConfigException("look_config.json is not valid JSON: " + e.Message);
            }

            var c = new LookConfig();
            foreach (var prop in Obj(root, "biomes", "biomes").Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                string path = "biomes." + prop.Name;
                var b = prop.Value as JObject ?? throw new LookConfigException(path + " must be an object");
                c.Biomes[prop.Name] = new BiomePalette
                {
                    SkyTop = Color(b, path, "sky_top"),
                    SkyBottom = Color(b, path, "sky_bottom"),
                    Far = Color(b, path, "far"),
                    Near = Color(b, path, "near"),
                    Accent = Color(b, path, "accent"),
                };
            }

            var cycle = Arr(root, "biome_cycle", "biome_cycle");
            c.BiomeCycle = new string[cycle.Count];
            for (int i = 0; i < cycle.Count; i++) c.BiomeCycle[i] = BiomeKey(c, (string)cycle[i], "biome_cycle[" + i + "]");
            if (c.BiomeCycle.Length == 0) throw new LookConfigException("biome_cycle must not be empty");

            var depth = Obj(root, "depth", "depth");
            c.DepthDarkening = Num(depth, "depth", "darkening");
            c.DepthFloor = Num(depth, "depth", "floor");

            foreach (var prop in Obj(root, "kits", "kits").Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                var list = prop.Value as JArray ?? throw new LookConfigException("kits." + prop.Name + " must be a list");
                var bodies = new string[list.Count];
                for (int i = 0; i < list.Count; i++) bodies[i] = (string)list[i];
                if (bodies.Length == 0) throw new LookConfigException("kits." + prop.Name + " must not be empty");
                c.Kits[prop.Name] = bodies;
            }

            var regions = Arr(root, "regions", "regions");
            for (int i = 0; i < regions.Count; i++)
                c.Regions.Add(Region(c, regions[i] as JObject, "regions[" + i + "]", true));
            c.Fallback = Region(c, Obj(root, "fallback", "fallback"), "fallback", false);
            c.TrainingBoss = Character(Obj(root, "training_boss", "training_boss"), "training_boss");

            var classes = Obj(root, "classes", "classes");
            foreach (ClassId cls in Enum.GetValues(typeof(ClassId)))
            {
                string key = cls.ToString().ToLowerInvariant();
                var o = Obj(classes, key, "classes." + key);
                Color(o, "classes." + key, "glow");
                c.Classes[cls] = new ClassLook { Body = Str(o, "classes." + key, "body"), Glow = Str(o, "classes." + key, "glow") };
            }

            c.RarityColors = Colors(Obj(root, "rarity_colors", "rarity_colors"), "rarity_colors");
            foreach (Rarity r in Enum.GetValues(typeof(Rarity)))
            {
                string key = r.ToString().ToLowerInvariant();
                if (!c.RarityColors.ContainsKey(key)) throw new LookConfigException("rarity_colors." + key + ": missing (one colour per rarity)");
            }
            c.ElementColors = Colors(Obj(root, "element_colors", "element_colors"), "element_colors");

            var cam = Obj(root, "camera", "camera");
            c.Camera = new CameraLook
            {
                BossZoom = Num(cam, "camera", "boss_zoom"),
                ZoomInS = Num(cam, "camera", "zoom_in_s"),
                ZoomOutS = Num(cam, "camera", "zoom_out_s"),
                BossScaleFirst = Num(cam, "camera", "boss_scale_first"),
                BossScalePerRegion = Num(cam, "camera", "boss_scale_per_region"),
                BossScaleMax = Num(cam, "camera", "boss_scale_max"),
            };
            foreach (var prop in Obj(root, "motions", "motions").Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                string path = "motions." + prop.Name;
                var m = prop.Value as JObject ?? throw new LookConfigException(path + " must be an object");
                c.Motions[prop.Name] = new PartMotion
                {
                    IdleDeg = Num(m, path, "idle_deg"),
                    PeriodS = Num(m, path, "period_s"),
                    AttackDeg = Num(m, path, "attack_deg"),
                    HitDeg = Num(m, path, "hit_deg"),
                };
            }
            var audio = Obj(root, "audio", "audio");
            c.Audio = new AudioLook
            {
                CrossfadeS = Num(audio, "audio", "crossfade_s"),
                AmbientVolume = Num(audio, "audio", "ambient_volume"),
                BossVolume = Num(audio, "audio", "boss_volume"),
            };
            foreach (var prop in Obj(root, "skills", "skills").Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                string path = "skills." + prop.Name;
                var o = prop.Value as JObject ?? throw new LookConfigException(path + " must be an object");
                c.Skills[prop.Name] = new SkillLook { Name = Str(o, path, "name"), Description = Str(o, path, "description") };
            }
            return c;
        }

        private static RegionEntry Region(LookConfig c, JObject o, string path, bool needsBiome)
        {
            if (o == null) throw new LookConfigException(path + " must be an object");
            string kit = Str(o, path, "kit");
            if (!c.Kits.ContainsKey(kit)) throw new LookConfigException(path + ".kit: unknown kit '" + kit + "'");
            string[] subzoneKits = null;
            if (o["subzone_kits"] is JArray sk)
            {
                subzoneKits = new string[sk.Count];
                for (int i = 0; i < sk.Count; i++)
                {
                    subzoneKits[i] = (string)sk[i];
                    if (subzoneKits[i] == null || !c.Kits.ContainsKey(subzoneKits[i]))
                        throw new LookConfigException(path + ".subzone_kits[" + i + "]: unknown kit '" + subzoneKits[i] + "'");
                }
            }
            return new RegionEntry
            {
                SubzoneKits = subzoneKits,
                Name = Str(o, path, "name"),
                Biome = needsBiome ? BiomeKey(c, Str(o, path, "biome"), path + ".biome") : null,
                Kit = kit,
                Boss = Character(Obj(o, "boss", path + ".boss"), path + ".boss"),
            };
        }

        private static CharacterLook Character(JObject o, string path) => new CharacterLook
        {
            Name = Str(o, path, "name"),
            Title = Str(o, path, "title"),
            Body = Str(o, path, "body"),
        };

        private static string BiomeKey(LookConfig c, string key, string path)
        {
            if (key == null || !c.Biomes.ContainsKey(key)) throw new LookConfigException(path + ": unknown biome '" + key + "'");
            return key;
        }

        private static Dictionary<string, string> Colors(JObject o, string path)
        {
            var result = new Dictionary<string, string>();
            foreach (var prop in o.Properties())
            {
                if (prop.Name.StartsWith("_")) continue;
                Color(o, path, prop.Name);
                result[prop.Name] = (string)prop.Value;
            }
            return result;
        }

        private static Rgb Color(JObject o, string path, string key)
        {
            string hex = Str(o, path, key);
            if (!Rgb.TryParse(hex, out var rgb)) throw new LookConfigException(path + "." + key + ": '" + hex + "' is not a #RRGGBB colour");
            return rgb;
        }

        private static JToken Req(JObject o, string path, string key) =>
            o[key] ?? throw new LookConfigException(path + ": missing '" + key + "'");

        private static JObject Obj(JObject o, string key, string path) =>
            o[key] as JObject ?? throw new LookConfigException(path + ": missing or not an object");

        private static JArray Arr(JObject o, string key, string path) =>
            o[key] as JArray ?? throw new LookConfigException(path + ": missing or not a list");

        private static string Str(JObject o, string path, string key)
        {
            var t = Req(o, path, key);
            if (t.Type != JTokenType.String) throw new LookConfigException(path + "." + key + " must be text");
            return (string)t;
        }

        private static double Num(JObject o, string path, string key)
        {
            var t = Req(o, path, key);
            if (t.Type != JTokenType.Float && t.Type != JTokenType.Integer) throw new LookConfigException(path + "." + key + " must be a number");
            return (double)t;
        }
    }
}
