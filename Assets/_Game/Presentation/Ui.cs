using System;
using System.Collections.Generic;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Look;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Helpers to build UI Toolkit elements in code. Colours are the Creative Direction tokens
    /// (Docs/CREATIVE_DIRECTION.md §3.3); panel and button looks come from Resources/UI/IronEmber.uss.
    /// </summary>
    internal static class Ui
    {
        public static readonly Color Background = Hex("#0B0D12");
        public static readonly Color Panel = Hex("#191C24");
        public static readonly Color Iron = Hex("#3A3F4C");
        public static readonly Color Silhouette = Hex("#07080B");
        public static readonly Color Text = Hex("#E6E1D6");
        public static readonly Color Muted = Hex("#9A978F");
        public static readonly Color Gold = Hex("#E8A24A");
        public static readonly Color Player = Hex("#5FAE6A");
        public static readonly Color Enemy = Hex("#D0464A");
        public static readonly Color Boss = Hex("#D0464A");
        public static readonly Color Hp = Hex("#5FAE6A");
        public static readonly Color Xp = Hex("#9B7BDC");
        public static readonly Color Up = Hex("#7FD18A");
        public static readonly Color Down = Hex("#E86A6A");
        public static readonly Color Selected = Hex("#2B3040");
        public static readonly Color Damage = Hex("#E86A6A");
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

        private static readonly Dictionary<Rarity, Color> RarityColors = new Dictionary<Rarity, Color>();

        /// <summary>Takes the rarity colours from look_config.json (call once at boot).</summary>
        public static void UseLook(LookConfig look)
        {
            RarityColors.Clear();
            foreach (Rarity r in Enum.GetValues(typeof(Rarity)))
                if (look.RarityColors.TryGetValue(r.ToString().ToLowerInvariant(), out var hex)) RarityColors[r] = Hex(hex);
        }

        public static Color RarityColor(Rarity r) => RarityColors.TryGetValue(r, out var c) ? c : Muted;

        /// <summary>"#RRGGBB" → Color (via the Core parser, so both layers read colours the same way).</summary>
        public static Color Hex(string hex)
        {
            var rgb = Rgb.Parse(hex);
            return new Color(rgb.R, rgb.G, rgb.B);
        }

        public static Color ToColor(Rgb rgb, float alpha = 1f) => new Color(rgb.R, rgb.G, rgb.B, alpha);

        /// <summary>Carved-capital title text (Cinzel): screen titles, region and boss names only.</summary>
        public static Label Title(string text, int size = 32, Color? color = null)
        {
            var l = Label(text, size, color ?? Text);
            l.AddToClassList("title");
            return l;
        }

        public static Label Label(string text, int size = 28, Color? color = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color ?? Text;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = 6;
            l.style.marginRight = 6;
            return l;
        }

        public static VisualElement Row(Justify justify = Justify.FlexStart)
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            e.style.justifyContent = justify;
            return e;
        }

        public static VisualElement Column()
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Column;
            e.style.alignItems = Align.Center;
            return e;
        }

        public static VisualElement Box(Color color, float width, float height)
        {
            var e = new VisualElement();
            e.style.backgroundColor = color;
            e.style.width = width;
            e.style.height = height;
            return e;
        }

        public static VisualElement PanelBox()
        {
            var e = new VisualElement();
            e.AddToClassList("panel");
            e.style.paddingTop = 12;
            e.style.paddingBottom = 12;
            e.style.paddingLeft = 12;
            e.style.paddingRight = 12;
            e.style.marginBottom = 8;
            return e;
        }

        /// <summary>A panel with ember corner marks (top bar, screen headers).</summary>
        public static VisualElement TitlePanel()
        {
            var e = PanelBox();
            e.AddToClassList("panel--title");
            var left = new VisualElement();
            left.AddToClassList("corner");
            left.AddToClassList("corner--left");
            left.pickingMode = PickingMode.Ignore;
            var right = new VisualElement();
            right.AddToClassList("corner");
            right.AddToClassList("corner--right");
            right.pickingMode = PickingMode.Ignore;
            e.Add(left);
            e.Add(right);
            return e;
        }

        /// <summary>A full-screen panel drawn over the game, hidden until shown.</summary>
        public static VisualElement Overlay()
        {
            var e = new VisualElement();
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            e.style.backgroundColor = Background;
            e.style.paddingTop = 24;
            e.style.paddingLeft = 16;
            e.style.paddingRight = 16;
            e.style.display = DisplayStyle.None;
            return e;
        }

        /// <summary>
        /// A pop-up window: the game behind is dimmed and a centred title panel holds the content.
        /// Returns the full-screen backdrop (show/hide this) and the window (add content here).
        /// </summary>
        public static (VisualElement Backdrop, VisualElement Window) Modal(float widthPercent = 88f, float maxWidth = 900f)
        {
            var backdrop = new VisualElement();
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0;
            backdrop.style.right = 0;
            backdrop.style.top = 0;
            backdrop.style.bottom = 0;
            backdrop.style.backgroundColor = Dim;
            backdrop.style.alignItems = Align.Center;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.style.display = DisplayStyle.None;

            var window = TitlePanel();
            window.style.width = Length.Percent(widthPercent);
            window.style.maxWidth = maxWidth;
            window.style.maxHeight = Length.Percent(85);
            window.style.paddingTop = 20;
            window.style.paddingBottom = 20;
            window.style.paddingLeft = 20;
            window.style.paddingRight = 20;
            window.style.alignItems = Align.Center;
            backdrop.Add(window);
            return (backdrop, window);
        }

        /// <summary>"8 h 05 min" style duration.</summary>
        public static string Duration(double seconds)
        {
            long total = (long)System.Math.Max(0, seconds);
            long h = total / 3600, m = total % 3600 / 60, s = total % 60;
            if (h > 0) return $"{h} h {m:00} min";
            if (m > 0) return $"{m} min {s:00} s";
            return $"{s} s";
        }

        public static Button Button(string text, System.Action onClick, int size = 26)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("iron-button");
            b.style.fontSize = size;
            b.style.minHeight = 56;
            b.style.minWidth = 72;
            return b;
        }
    }

    /// <summary>A horizontal fill bar (HP, XP).</summary>
    internal sealed class Bar
    {
        public readonly VisualElement Root;
        private readonly VisualElement _fill;

        public Bar(Color color, float width, float height)
        {
            Root = Ui.Box(new Color(0, 0, 0, 0.5f), width, height);
            _fill = Ui.Box(color, 0, height);
            Root.Add(_fill);
        }

        public void Set(double fraction)
        {
            float f = Mathf.Clamp01(double.IsNaN(fraction) ? 0f : (float)fraction);
            _fill.style.width = new Length(f * 100f, LengthUnit.Percent);
        }
    }
}
