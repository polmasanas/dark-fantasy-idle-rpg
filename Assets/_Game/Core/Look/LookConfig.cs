using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Look
{
    /// <summary>A colour as 0–1 channels, parsed from "#RRGGBB" (look_config.json). No UnityEngine here.</summary>
    public readonly struct Rgb
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>Parses "#RRGGBB"; returns false for anything else.</summary>
        public static bool TryParse(string hex, out Rgb rgb)
        {
            rgb = default;
            if (hex == null || hex.Length != 7 || hex[0] != '#') return false;
            if (!int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            rgb = new Rgb(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
            return true;
        }

        public static Rgb Parse(string hex)
        {
            if (!TryParse(hex, out var rgb)) throw new FormatException("Not a #RRGGBB colour: " + hex);
            return rgb;
        }

        public Rgb Scale(double f) => new Rgb((float)(R * f), (float)(G * f), (float)(B * f));

        public string ToHex() => "#" + Byte(R) + Byte(G) + Byte(B);

        private static string Byte(float c) =>
            ((int)Math.Round(Math.Max(0, Math.Min(1, c)) * 255)).ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>The five colours of a biome (CD-3). The accent is never darkened by depth.</summary>
    public sealed class BiomePalette
    {
        public Rgb SkyTop;
        public Rgb SkyBottom;
        public Rgb Far;
        public Rgb Near;
        public Rgb Accent;

        public BiomePalette Shaded(double depth) => new BiomePalette
        {
            SkyTop = SkyTop.Scale(depth),
            SkyBottom = SkyBottom.Scale(depth),
            Far = Far.Scale(depth),
            Near = Near.Scale(depth),
            Accent = Accent,
        };
    }

    /// <summary>A named character drawn from a body sprite (bosses, the training boss).</summary>
    public sealed class CharacterLook
    {
        public string Name;
        public string Title;
        public string Body;
    }

    public sealed class RegionEntry
    {
        /// <summary>Display name; for the fallback entry a format string where {0} is the region number.</summary>
        public string Name;
        public string Biome;
        public string Kit;

        /// <summary>Optional kit per subzone (M5-6: Frost Pass subzone 5 shows wizards); null = <see cref="Kit"/> everywhere.</summary>
        public string[] SubzoneKits;
        public CharacterLook Boss;
    }

    public sealed class ClassLook
    {
        public string Body;
        public string Glow;
    }

    public sealed class CameraLook
    {
        public double BossZoom;
        public double ZoomInS;
        public double ZoomOutS;

        /// <summary>Region boss size vs a regular enemy: first + per_region × (region − 1), capped at max (owner, CD playtest).</summary>
        public double BossScaleFirst;
        public double BossScalePerRegion;
        public double BossScaleMax;
    }

    /// <summary>How one body part moves (owner, CD playtest: characters move part by part).</summary>
    public sealed class PartMotion
    {
        public double IdleDeg;
        public double PeriodS;
        public double AttackDeg;
        public double HitDeg;
    }

    public sealed class AudioLook
    {
        public double CrossfadeS;
        public double AmbientVolume;
        public double BossVolume;
    }

    /// <summary>A skill's player-facing name and description (M6); numbers come from skills_config.json.</summary>
    public sealed class SkillLook
    {
        public string Name;
        public string Description;
    }

    /// <summary>Everything in Assets/_Game/Data/look_config.json (Docs/CREATIVE_DIRECTION.md).</summary>
    public sealed class LookConfig
    {
        public Dictionary<string, BiomePalette> Biomes = new Dictionary<string, BiomePalette>();
        public string[] BiomeCycle;
        public double DepthDarkening;
        public double DepthFloor;
        public List<RegionEntry> Regions = new List<RegionEntry>();
        public RegionEntry Fallback;
        public CharacterLook TrainingBoss;
        public Dictionary<string, string[]> Kits = new Dictionary<string, string[]>();
        public Dictionary<ClassId, ClassLook> Classes = new Dictionary<ClassId, ClassLook>();
        public Dictionary<string, string> RarityColors = new Dictionary<string, string>();
        public Dictionary<string, string> ElementColors = new Dictionary<string, string>();
        public CameraLook Camera;
        public AudioLook Audio;
        public Dictionary<string, PartMotion> Motions = new Dictionary<string, PartMotion>();
        public Dictionary<string, SkillLook> Skills = new Dictionary<string, SkillLook>();
    }

    public sealed class LookConfigException : Exception
    {
        public LookConfigException(string message) : base(message)
        {
        }
    }
}
