using System;
using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Util;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Placeholder HUD (M2): location and kills, level and XP, gold, attribute points with
    /// manual + and preset auto-allocation, and the "Fight boss when available" toggle.
    /// </summary>
    internal sealed class HudView
    {
        internal static readonly (AttributeId Id, string Name)[] AttributeNames =
        {
            (AttributeId.Str, "STR"), (AttributeId.Dex, "DEX"), (AttributeId.Int, "INT"),
            (AttributeId.Vit, "VIT"), (AttributeId.Def, "DEF"), (AttributeId.Counter, "COUNTER"),
            (AttributeId.CritChance, "CRIT"), (AttributeId.CritDamage, "CRIT DMG"), (AttributeId.Luck, "LUCK"),
        };

        public readonly VisualElement Top;
        public readonly VisualElement Bottom;

        private readonly GameSession _session;
        private readonly GameFormulas _formulas;
        private readonly Label _location;
        private readonly Label _kills;
        private readonly Label _level;
        private readonly Bar _xp;
        private readonly Label _gold;
        private readonly Label _unspent;
        private readonly Dictionary<AttributeId, Label> _values = new Dictionary<AttributeId, Label>();
        private readonly List<Button> _plusButtons = new List<Button>();
        private readonly Toggle _bossToggle;
        private readonly Toggle _autoToggle;

        public HudView(GameSession session, BalanceConfig config, Action openInventory, Action openCustomPreset)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _formulas = new GameFormulas(config);
            var character = session.State.Character;

            // ---- Top: where am I, how strong am I ----
            Top = Ui.PanelBox();
            var row1 = Ui.Row(Justify.SpaceBetween);
            _location = Ui.Label("", 30, Ui.Text, true);
            _kills = Ui.Label("", 28, Ui.Muted);
            row1.Add(_location);
            row1.Add(_kills);
            Top.Add(row1);

            var row2 = Ui.Row(Justify.SpaceBetween);
            row2.style.marginTop = 8;
            _level = Ui.Label("", 28, Ui.Text, true);
            _xp = new Bar(Ui.Xp, 360, 16);
            _gold = Ui.Label("", 28, Ui.Gold, true);
            row2.Add(_level);
            row2.Add(_xp.Root);
            row2.Add(_gold);
            row2.Add(Ui.Button("Bag", openInventory));
            Top.Add(row2);

            // ---- Bottom: attribute points and boss toggle ----
            Bottom = Ui.PanelBox();
            var header = Ui.Row(Justify.SpaceBetween);
            _unspent = Ui.Label("", 28, Ui.Gold, true);
            header.Add(_unspent);

            var presetKeys = config.AllocationPresets.Keys.OrderBy(k => k).ToList();
            if (!presetKeys.Contains(ProgressionSystem.CustomPreset)) presetKeys.Add(ProgressionSystem.CustomPreset);
            int presetIndex = Math.Max(0, presetKeys.IndexOf(character.Preset));
            var preset = new DropdownField("Preset", presetKeys, presetIndex);
            preset.style.fontSize = 24;
            preset.style.minWidth = 260;
            preset.labelElement.style.color = Ui.Text;
            preset.RegisterValueChangedCallback(evt => character.Preset = evt.newValue);
            header.Add(preset);
            header.Add(Ui.Button("Edit", openCustomPreset, 22));
            header.Add(Ui.Button("Auto", () => _session.Progression.AutoAllocate(character)));
            Bottom.Add(header);

            _autoToggle = new Toggle("Auto-allocate on level-up") { value = character.AutoAllocateOnLevelUp };
            _autoToggle.style.fontSize = 24;
            _autoToggle.labelElement.style.color = Ui.Text;
            _autoToggle.RegisterValueChangedCallback(evt =>
            {
                character.AutoAllocateOnLevelUp = evt.newValue;
                if (evt.newValue) _session.Progression.AutoAllocate(character); // spend what is already waiting
            });
            Bottom.Add(_autoToggle);

            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 8;
            foreach (var (id, name) in AttributeNames)
            {
                var cell = Ui.Row(Justify.SpaceBetween);
                cell.style.width = new Length(33f, LengthUnit.Percent);
                var value = Ui.Label(name, 24, Ui.Text);
                _values[id] = value;
                cell.Add(value);
                var attribute = id;
                var plus = Ui.Button("+", () => _session.Progression.SpendPoint(character, attribute), 24);
                plus.style.minWidth = 56;
                _plusButtons.Add(plus);
                cell.Add(plus);
                grid.Add(cell);
            }
            Bottom.Add(grid);

            _bossToggle = new Toggle("Fight boss when available") { value = session.State.World.FightBossWhenAvailable };
            _bossToggle.style.fontSize = 26;
            _bossToggle.style.marginTop = 8;
            _bossToggle.labelElement.style.color = Ui.Text;
            _bossToggle.RegisterValueChangedCallback(evt => _session.State.World.FightBossWhenAvailable = evt.newValue);
            Bottom.Add(_bossToggle);
        }

        public void Refresh()
        {
            var st = _session.State;
            var w = st.World;
            var c = st.Character;
            int k = _session.World.KillsToAdvance(w);
            bool retreating = _session.World.IsRetreating(w, st.GameTime);

            _location.text = $"Region {w.Region} · Subzone {w.Region}-{w.UnlockedSubzone}" + (retreating ? " (retreating)" : "");
            _kills.text = _session.World.IsBossAvailable(w)
                ? (w.FightBossWhenAvailable ? "Boss ready" : "Boss ready (toggle off)")
                : $"Kills {w.KillsInSubzone(w.UnlockedSubzone)} / {k}";
            _level.text = $"Level {c.Level}";
            _xp.Set(c.Xp / _formulas.XpToNext(c.Level));
            _gold.text = "Gold " + NumberFormat.Short(st.Gold) + " · Mat " + NumberFormat.Short(st.Wallet.Materials);
            _unspent.text = $"Points: {c.UnspentPoints}";

            foreach (var (id, name) in AttributeNames)
                _values[id].text = $"{name} {_session.Progression.Total(c, id)}";
            foreach (var b in _plusButtons)
                b.SetEnabled(c.UnspentPoints > 0);
            if (_bossToggle.value != w.FightBossWhenAvailable)
                _bossToggle.SetValueWithoutNotify(w.FightBossWhenAvailable);
        }
    }
}
