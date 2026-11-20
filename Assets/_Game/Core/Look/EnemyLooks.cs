using System;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Look
{
    public enum LookRank
    {
        Normal,
        Elite,
        Boss,
    }

    /// <summary>What to draw for one encounter: a body sprite plus accents (CD-9).</summary>
    public sealed class EnemyLook
    {
        public string Body;
        public LookRank Rank;

        /// <summary>Eye and weapon glow: the element colour when the enemy has one (M5), else the region accent.</summary>
        public string GlowHex;

        public string DisplayName;

        /// <summary>Region bosses pull the camera back (CD-2); the first-run training boss does not.</summary>
        public bool UseBossCamera;
    }

    public sealed class EnemyLooks
    {
        private readonly LookConfig _c;
        private readonly RegionLooks _regions;

        public EnemyLooks(LookConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _regions = new RegionLooks(config);
        }

        public EnemyLook For(Encounter e, string element = null)
        {
            var region = _regions.For(e.Region);
            string glow = element != null && _c.ElementColors.TryGetValue(element, out var hex) ? hex : region.Palette.Accent.ToHex();
            if (e.IsTraining) return Boss(_c.TrainingBoss, glow, false);
            if (e.IsBoss) return Boss(region.Boss, glow, true);
            var kit = _c.Kits[region.Kit];
            string body = kit[(Math.Max(1, e.Subzone) - 1) % kit.Length];
            return new EnemyLook { Body = body, Rank = LookRank.Normal, GlowHex = glow, DisplayName = Pretty(body) };
        }

        public ClassLook ForClass(ClassId cls) => _c.Classes[cls];

        private static EnemyLook Boss(CharacterLook who, string glow, bool camera) => new EnemyLook
        {
            Body = who.Body,
            Rank = LookRank.Boss,
            GlowHex = glow,
            DisplayName = who.Name + " · " + who.Title,
            UseBossCamera = camera,
        };

        /// <summary>"target_post" → "Target post".</summary>
        private static string Pretty(string key)
        {
            string s = key.Replace('_', ' ');
            return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
