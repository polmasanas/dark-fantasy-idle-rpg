using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>Small helpers to build placeholder UI Toolkit elements in code (M2).</summary>
    internal static class Ui
    {
        public static readonly Color Background = new Color(0.08f, 0.07f, 0.10f);
        public static readonly Color Panel = new Color(0.14f, 0.12f, 0.17f);
        public static readonly Color Text = new Color(0.92f, 0.90f, 0.86f);
        public static readonly Color Muted = new Color(0.62f, 0.60f, 0.58f);
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.30f);
        public static readonly Color Player = new Color(0.25f, 0.45f, 0.85f);
        public static readonly Color Enemy = new Color(0.75f, 0.22f, 0.22f);
        public static readonly Color Boss = new Color(0.45f, 0.08f, 0.10f);
        public static readonly Color Hp = new Color(0.30f, 0.75f, 0.35f);
        public static readonly Color Xp = new Color(0.55f, 0.40f, 0.90f);

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
            e.style.backgroundColor = Panel;
            e.style.paddingTop = 12;
            e.style.paddingBottom = 12;
            e.style.paddingLeft = 12;
            e.style.paddingRight = 12;
            e.style.marginBottom = 8;
            return e;
        }

        public static Button Button(string text, System.Action onClick, int size = 26)
        {
            var b = new Button(onClick) { text = text };
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
