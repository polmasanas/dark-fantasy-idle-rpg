using System;
using System.Globalization;

namespace DarkFantasy.Core.Look
{
    /// <summary>How one region looks: name, biome, enemy kit, boss, and its palette shaded for depth (CD-3).</summary>
    public sealed class RegionLook
    {
        public int Region;
        public string Name;
        public string BiomeKey;
        public string Kit;
        public CharacterLook Boss;
        public BiomePalette Palette;

        /// <summary>Brightness factor: 1.0 at region 1, falling by depth.darkening per region, never below depth.floor.</summary>
        public double Depth;
    }

    public sealed class RegionLooks
    {
        private readonly LookConfig _c;

        public RegionLooks(LookConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>The body kit shown in a subzone (per-subzone kits when the region lists them, M5-6).</summary>
        public string KitFor(int region, int subzone)
        {
            if (region < 1) throw new ArgumentOutOfRangeException(nameof(region), region, "Regions start at 1");
            var entry = region <= _c.Regions.Count ? _c.Regions[region - 1] : _c.Fallback;
            if (entry.SubzoneKits == null || entry.SubzoneKits.Length == 0) return entry.Kit;
            return entry.SubzoneKits[Math.Min(Math.Max(1, subzone), entry.SubzoneKits.Length) - 1];
        }

        public RegionLook For(int region)
        {
            if (region < 1) throw new ArgumentOutOfRangeException(nameof(region), region, "Regions start at 1");
            bool listed = region <= _c.Regions.Count;
            var entry = listed ? _c.Regions[region - 1] : _c.Fallback;
            string biome = listed ? entry.Biome : _c.BiomeCycle[(region - _c.Regions.Count - 1) % _c.BiomeCycle.Length];
            double depth = Math.Max(_c.DepthFloor, 1 - _c.DepthDarkening * (region - 1));
            return new RegionLook
            {
                Region = region,
                Name = listed ? entry.Name : string.Format(CultureInfo.InvariantCulture, entry.Name, region),
                BiomeKey = biome,
                Kit = entry.Kit,
                Boss = entry.Boss,
                Palette = _c.Biomes[biome].Shaded(depth),
                Depth = depth,
            };
        }
    }
}
