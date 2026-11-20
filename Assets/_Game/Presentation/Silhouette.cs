using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// One character on stage: the black body sprite, its glow sprite tinted from data, a soft halo
    /// behind it, idle sway, an attack lunge and a hit flash. Presentation only (CD-4, CD-9).
    /// </summary>
    internal sealed class Silhouette
    {
        private const float SwayDegrees = 1.5f;
        private const float SwayPeriodS = 3f;
        private const float LungeS = 0.15f;
        private const float LungeShare = 0.06f; // of the arena width
        private const float FlashS = 0.15f;

        public readonly VisualElement Root;
        private readonly VisualElement _halo;
        private readonly VisualElement _body;
        private readonly VisualElement _glow;
        private readonly bool _facesLeft;
        private float _aspect = 0.6f;
        private float _time;
        private float _lunge = -1f;
        private float _flash = -1f;
        private Color _haloColor = Color.white;

        public Silhouette(bool facesLeft)
        {
            _facesLeft = facesLeft;
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));

            _halo = Layer(ArtLibrary.Get("fx_glow"));
            _halo.style.left = Length.Percent(-35);
            _halo.style.right = Length.Percent(-35);
            _halo.style.top = Length.Percent(10);
            _halo.style.bottom = Length.Percent(-10);
            _body = Layer(null);
            _glow = Layer(null);
            Root.Add(_halo);
            Root.Add(_body);
            Root.Add(_glow);
        }

        /// <summary>Extra horizontal offset in pixels (the boss slide-in); the lunge is added on top.</summary>
        public float BaseOffsetX { get; set; }

        /// <summary>Width / height of the current body sprite (to size the element).</summary>
        public float Aspect => _aspect;

        public void Show(string body, Color glow)
        {
            var bodyTex = ArtLibrary.Get(body);
            var glowTex = ArtLibrary.Get(body + "_glow");
            _body.style.backgroundImage = bodyTex != null ? new StyleBackground(bodyTex) : new StyleBackground(StyleKeyword.None);
            _body.style.backgroundColor = bodyTex != null ? new Color(0, 0, 0, 0) : Ui.Silhouette; // missing art: plain box
            _glow.style.backgroundImage = glowTex != null ? new StyleBackground(glowTex) : new StyleBackground(StyleKeyword.None);
            _glow.style.unityBackgroundImageTintColor = glow;
            _haloColor = new Color(glow.r, glow.g, glow.b, 0.35f);
            _halo.style.unityBackgroundImageTintColor = _haloColor;
            _aspect = bodyTex != null ? (float)bodyTex.width / bodyTex.height : 0.6f;
            Root.style.opacity = 1f;
            _lunge = _flash = -1f;
        }

        public void Lunge() => _lunge = 0f;

        public void Flash() => _flash = 0f;

        public void Fade() => Root.style.opacity = 0.25f;

        /// <summary>Advances idle and hit animations; <paramref name="arenaWidth"/> in pixels.</summary>
        public void Tick(float dt, float arenaWidth)
        {
            _time += dt;
            float sway = Mathf.Sin(_time * 2f * Mathf.PI / SwayPeriodS) * SwayDegrees;
            Root.style.rotate = new Rotate(new Angle(_facesLeft ? -sway : sway, AngleUnit.Degree));
            Root.style.scale = new Scale(new Vector3(_facesLeft ? -1f : 1f, 1f, 1f));

            float dx = 0f;
            if (_lunge >= 0f)
            {
                _lunge += dt;
                float p = Mathf.Clamp01(_lunge / LungeS);
                dx = Mathf.Sin(p * Mathf.PI) * LungeShare * arenaWidth * (_facesLeft ? -1f : 1f);
                if (p >= 1f) _lunge = -1f;
            }
            Root.style.translate = new Translate(BaseOffsetX + dx, 0f);

            if (_flash >= 0f)
            {
                _flash += dt;
                float p = Mathf.Clamp01(_flash / FlashS);
                _halo.style.unityBackgroundImageTintColor = Color.Lerp(new Color(1f, 1f, 1f, 0.8f), _haloColor, p);
                if (p >= 1f) _flash = -1f;
            }
        }

        private static VisualElement Layer(Texture2D tex)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }
    }
}
