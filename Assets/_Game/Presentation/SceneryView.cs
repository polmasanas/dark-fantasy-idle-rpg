using DarkFantasy.Core.Look;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// The region's layered background (CD-3): sky colour, a tinted sky gradient, far and near layers
    /// drawn in white and tinted from the depth-shaded biome palette, and the ground strip in front of them.
    /// The layers are wider than the arena so the boss camera can pull back without showing edges.
    /// </summary>
    internal sealed class SceneryView
    {
        private const float FarDriftShare = 0.015f;   // of the layer width, peak
        private const float NearDriftShare = 0.03f;
        private const float FarDriftPeriodS = 120f;
        private const float NearDriftPeriodS = 75f;
        public const float GroundShare = 0.08f;        // ground strip height, share of the arena

        public readonly VisualElement Sky;   // fills the arena, never scaled
        public readonly VisualElement World;  // far + near layers, scaled by the boss camera
        public readonly VisualElement Ground; // floor strip, never scaled: characters and halos stand behind/on it
        private readonly VisualElement _gradient;
        private readonly VisualElement _far;
        private readonly VisualElement _near;
        private int _region = -1;
        private float _time;

        public SceneryView()
        {
            Sky = Fill();
            _gradient = Fill();
            _gradient.style.backgroundImage = Tex("bg_sky_gradient");
            _gradient.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Sky.Add(_gradient);

            World = Fill();
            World.style.left = Length.Percent(-35);
            World.style.right = Length.Percent(-35);
            World.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            _far = Layer();
            _near = Layer();
            Ground = new VisualElement { pickingMode = PickingMode.Ignore };
            Ground.style.position = Position.Absolute;
            Ground.style.left = 0;
            Ground.style.right = 0;
            Ground.style.bottom = 0;
            Ground.style.height = Length.Percent(GroundShare * 100f);
            Ground.style.backgroundColor = Ui.Background;
            World.Add(_far);
            World.Add(_near);
        }

        public void SetRegion(RegionLook look, bool insideWalls)
        {
            if (look.Region == _region) return;
            _region = look.Region;
            var p = look.Palette;
            Sky.style.backgroundColor = Ui.ToColor(p.SkyTop);
            _gradient.style.unityBackgroundImageTintColor = Ui.ToColor(p.SkyBottom);
            _far.style.backgroundImage = Tex(insideWalls ? "bg_far_walls" : "bg_far_hills");
            _far.style.unityBackgroundImageTintColor = Ui.ToColor(p.Far);
            _near.style.backgroundImage = Tex(insideWalls ? "bg_near_yard" : "bg_near_hills");
            _near.style.unityBackgroundImageTintColor = Ui.ToColor(p.Near);
        }

        public void Tick(float dt)
        {
            _time += dt;
            _far.style.translate = new Translate(Length.Percent(Mathf.Sin(_time * 2f * Mathf.PI / FarDriftPeriodS) * FarDriftShare * 100f), 0);
            _near.style.translate = new Translate(Length.Percent(Mathf.Sin(_time * 2f * Mathf.PI / NearDriftPeriodS) * NearDriftShare * 100f), 0);
        }

        private static StyleBackground Tex(string name)
        {
            var t = ArtLibrary.Get(name);
            return t != null ? new StyleBackground(t) : new StyleBackground(StyleKeyword.None);
        }

        private static VisualElement Fill()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            return e;
        }

        private static VisualElement Layer()
        {
            var e = Fill();
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return e;
        }
    }
}
