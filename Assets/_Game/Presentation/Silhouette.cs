using System.Collections.Generic;
using DarkFantasy.Core.Look;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// One character on stage, assembled from its parts (rigs.json): each part is a black sprite, an
    /// optional glow sprite tinted from data and a part-shaped halo behind it, all rotating together
    /// around the part's pivot. Parts sway on their own rhythm, swing on attack and recoil on a hit
    /// (motions in look_config.json). Presentation only (CD-4, CD-9, owner playtest).
    /// </summary>
    internal sealed class Silhouette
    {
        private const float AttackS = 0.3f;
        private const float HitS = 0.25f;
        private const float LungeS = 0.15f;
        private const float LungeShare = 0.06f; // of the arena width
        private const float HaloAlpha = 0.35f;
        private const float FlashAlpha = 0.85f;

        public readonly VisualElement Root;
        private readonly bool _facesLeft;
        private readonly RigLibrary _rigs;
        private readonly Dictionary<string, PartMotion> _motions;
        private readonly List<PartView> _parts = new List<PartView>();
        private float _aspect = 0.6f;
        private float _time;
        private float _attack = -1f;
        private float _hit = -1f;
        private Color _glow = Color.white;

        private sealed class PartView
        {
            public VisualElement Root;
            public VisualElement Aura;
            public PartMotion Motion;
            public float Phase;
        }

        public Silhouette(bool facesLeft, RigLibrary rigs, Dictionary<string, PartMotion> motions)
        {
            _facesLeft = facesLeft;
            _rigs = rigs;
            _motions = motions ?? new Dictionary<string, PartMotion>();
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
        }

        /// <summary>Extra horizontal offset in pixels (the boss slide-in); the lunge is added on top.</summary>
        public float BaseOffsetX { get; set; }

        /// <summary>Width / height of the current body (to size the element).</summary>
        public float Aspect => _aspect;

        public void Show(string body, Color glow)
        {
            Root.Clear();
            _parts.Clear();
            _glow = glow;
            var rig = _rigs?.Get(body);
            if (rig == null)
            {
                // No rig: draw the whole sprite as one still part, or a plain box if even that is missing.
                var tex = ArtLibrary.Get(body);
                _aspect = tex != null ? (float)tex.width / tex.height : 0.6f;
                AddPart(body, body + "_glow", null, 0.5f, 1f, 0f, null);
            }
            else
            {
                _aspect = (float)rig.Aspect;
                for (int i = 0; i < rig.Parts.Length; i++)
                {
                    var p = rig.Parts[i];
                    string name = body + "__" + p.Id;
                    _motions.TryGetValue(p.Id, out var motion);
                    AddPart(name, p.Glow ? name + "_glow" : null, name + "_aura", (float)p.PivotX, (float)p.PivotY, (float)rig.AuraPad, motion);
                    _parts[_parts.Count - 1].Phase = i * 1.3f;
                }
            }
            Root.style.opacity = 1f;
            _attack = _hit = -1f;
        }

        public void Attack()
        {
            _attack = 0f;
        }

        public void Hit()
        {
            _hit = 0f;
        }

        public void Fade() => Root.style.opacity = 0.25f;

        /// <summary>Advances the part motions; <paramref name="arenaWidth"/> in pixels.</summary>
        public void Tick(float dt, float arenaWidth)
        {
            _time += dt;
            float attack = Pulse(ref _attack, AttackS, dt);
            float hit = Pulse(ref _hit, HitS, dt);

            Root.style.scale = new Scale(new Vector3(_facesLeft ? -1f : 1f, 1f, 1f));
            float lunge = _attack >= 0f ? Mathf.Sin(Mathf.Clamp01(_attack / LungeS) * Mathf.PI) : 0f;
            float dx = lunge * LungeShare * arenaWidth * (_facesLeft ? -1f : 1f);
            Root.style.translate = new Translate(BaseOffsetX + dx, 0f);

            var haloColor = Color.Lerp(new Color(_glow.r, _glow.g, _glow.b, HaloAlpha), new Color(1f, 1f, 1f, FlashAlpha), hit);
            foreach (var part in _parts)
            {
                if (part.Aura != null) part.Aura.style.unityBackgroundImageTintColor = haloColor;
                var m = part.Motion;
                if (m == null) continue;
                float idle = m.PeriodS > 0 ? Mathf.Sin(_time * 2f * Mathf.PI / (float)m.PeriodS + part.Phase) * (float)m.IdleDeg : 0f;
                float angle = idle + attack * (float)m.AttackDeg + hit * (float)m.HitDeg;
                part.Root.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }
        }

        /// <summary>0 → 1 → 0 over <paramref name="length"/> seconds after a trigger; -1 means idle.</summary>
        private static float Pulse(ref float t, float length, float dt)
        {
            if (t < 0f) return 0f;
            t += dt;
            float p = Mathf.Clamp01(t / length);
            if (p >= 1f) t = -1f;
            return Mathf.Sin(p * Mathf.PI);
        }

        private void AddPart(string sprite, string glowSprite, string auraSprite, float pivotX, float pivotY, float auraPad, PartMotion motion)
        {
            var root = Layer();
            root.style.transformOrigin = new TransformOrigin(Length.Percent(pivotX * 100f), Length.Percent(pivotY * 100f));

            VisualElement aura = null;
            var auraTex = auraSprite != null ? ArtLibrary.Get(auraSprite) : null;
            if (auraTex != null)
            {
                aura = Layer();
                float padY = auraPad * 100f;
                float padX = _aspect > 0 ? auraPad / _aspect * 100f : padY;
                aura.style.left = Length.Percent(-padX);
                aura.style.right = Length.Percent(-padX);
                aura.style.top = Length.Percent(-padY);
                aura.style.bottom = Length.Percent(-padY);
                aura.style.backgroundImage = new StyleBackground(auraTex);
                aura.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                root.Add(aura);
            }

            var body = Layer();
            var bodyTex = ArtLibrary.Get(sprite);
            if (bodyTex != null)
            {
                body.style.backgroundImage = new StyleBackground(bodyTex);
                body.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            else
            {
                body.style.backgroundColor = Ui.Silhouette; // missing art: plain box
            }
            root.Add(body);

            var glowTex = glowSprite != null ? ArtLibrary.Get(glowSprite) : null;
            if (glowTex != null)
            {
                var glow = Layer();
                glow.style.backgroundImage = new StyleBackground(glowTex);
                glow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                glow.style.unityBackgroundImageTintColor = _glow;
                root.Add(glow);
            }

            Root.Add(root);
            _parts.Add(new PartView { Root = root, Aura = aura, Motion = motion });
        }

        private static VisualElement Layer()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            return e;
        }
    }
}
