using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Session;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>Editor for the per-character Custom auto-allocate preset (M4-2): a weight per attribute.</summary>
    internal sealed class CustomPresetView
    {
        public readonly VisualElement Root;
        private readonly GameSession _session;
        private readonly Dictionary<AttributeId, Label> _values = new Dictionary<AttributeId, Label>();
        private readonly Label _hint;

        public CustomPresetView(GameSession session, BalanceConfig config)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            int max = config.AllocationRules.CustomWeightMax;
            Root = Ui.Overlay();

            var top = Ui.Row(Justify.SpaceBetween);
            top.Add(Ui.Label("Custom preset", 34, Ui.Text, true));
            top.Add(Ui.Button("Done", () => Root.style.display = DisplayStyle.None));
            top.style.flexShrink = 0;
            Root.Add(top);
            var help = Ui.Label($"Weight 0–{max} per attribute. New points are shared in proportion to the weights. " +
                                "All at 0 = everything into the class primary attribute.", 24, Ui.Muted);
            help.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(help);

            // rows scroll if the screen is short, so Done and every +/- stay reachable
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.flexBasis = 0;
            Root.Add(scroll);

            foreach (var (id, name) in HudView.AttributeNames)
            {
                var row = Ui.Row(Justify.SpaceBetween);
                row.style.marginTop = 6;
                row.Add(Ui.Label(name, 28));
                var controls = Ui.Row();
                var attribute = id;
                controls.Add(Ui.Button("-", () => Change(attribute, -1)));
                var value = Ui.Label("0", 28, Ui.Gold, true);
                value.style.minWidth = 60;
                value.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                _values[id] = value;
                controls.Add(value);
                controls.Add(Ui.Button("+", () => Change(attribute, +1)));
                row.Add(controls);
                scroll.Add(row);
            }
            _hint = Ui.Label("", 24, Ui.Muted);
            Root.Add(_hint);
        }

        public void Open()
        {
            Refresh();
            Root.style.display = DisplayStyle.Flex;
        }

        private void Change(AttributeId a, int delta)
        {
            var c = _session.State.Character;
            c.CustomWeights.TryGetValue(a, out int w);
            _session.Progression.SetCustomWeight(c, a, w + delta);
            Refresh();
        }

        private void Refresh()
        {
            var c = _session.State.Character;
            foreach (var kv in _values)
                kv.Value.text = (c.CustomWeights.TryGetValue(kv.Key, out int w) ? w : 0).ToString();
            _hint.text = c.Preset == ProgressionSystem.CustomPreset ? "" : "Tip: choose the \"custom\" preset to use these weights.";
        }
    }
}
