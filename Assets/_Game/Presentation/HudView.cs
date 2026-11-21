using System;
using System.Collections.Generic;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Look;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Util;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Placeholder HUD (M2): location and kills, level and XP, gold, attribute points with
    /// manual + and auto-allocation (default or Custom weights), and the "Fight boss when available" toggle.
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

        /// <summary>The active hero (M5: changes when the player switches).</summary>
        private CharacterState Hero => _session.State.Character;
        private readonly RegionLooks _regions;
        private readonly Label _location;
        private readonly Label _kills;
        private readonly Label _level;
        private readonly Bar _xp;
        private readonly Label _gold;
        private readonly Label _unspent;
        private readonly Button _skills;
        private readonly Dictionary<AttributeId, Label> _values = new Dictionary<AttributeId, Label>();
        private readonly List<Button> _plusButtons = new List<Button>();
        private readonly Toggle _bossToggle;
        private readonly Toggle _autoToggle;
        private readonly Toggle _custom;

        public HudView(GameSession session, BalanceConfig config, RegionLooks regions, Action openInventory, Action openCustomPreset, Action openSkills)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _formulas = new GameFormulas(config);
            _regions = regions ?? throw new ArgumentNullException(nameof(regions));

            // ---- Top: where am I, how strong am I ----
            Top = Ui.TitlePanel();
            var row1 = Ui.Row(Justify.SpaceBetween);
            _location = Ui.Title("", 28);
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
            _skills = Ui.Button("Skills", openSkills); // M6: shows a dot while skill points wait to be spent
            row2.Add(_skills);
            Top.Add(row2);

            // ---- Bottom: attribute points and boss toggle ----
            Bottom = Ui.PanelBox();
            var header = Ui.Row(Justify.SpaceBetween);
            _unspent = Ui.Label("", 28, Ui.Gold, true);
            header.Add(_unspent);

            // Owner, M4 playtest: only two choices, default (class primary) or the player's custom weights.
            var custom = _custom = new Toggle("Custom") { value = Hero.Preset == ProgressionSystem.CustomPreset };
            custom.style.fontSize = 24;
            custom.labelElement.style.color = Ui.Text;
            custom.RegisterValueChangedCallback(evt =>
                Hero.Preset = evt.newValue ? ProgressionSystem.CustomPreset : ProgressionSystem.DefaultPreset);
            header.Add(custom);
            header.Add(Ui.Button("Edit", openCustomPreset, 22));
            header.Add(Ui.Button("Auto", () => _session.Progression.AutoAllocate(Hero)));
            Bottom.Add(header);

            _autoToggle = new Toggle("Auto-allocate on level-up") { value = Hero.AutoAllocateOnLevelUp };
            _autoToggle.style.fontSize = 24;
            _autoToggle.labelElement.style.color = Ui.Text;
            _autoToggle.RegisterValueChangedCallback(evt =>
            {
                Hero.AutoAllocateOnLevelUp = evt.newValue;
                if (evt.newValue) _session.Progression.AutoAllocate(Hero); // spend what is already waiting
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
                var plus = Ui.Button("+", () => _session.Progression.SpendPoint(Hero, attribute), 24);
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

            _location.text = $"{Capitalise(_regions.For(w.Region).Name)} · {w.Region}-{w.UnlockedSubzone}" + (retreating ? " (retreating)" : "");
            _kills.text = _session.World.IsBossAvailable(w)
                ? (w.FightBossWhenAvailable ? "Boss ready" : "Boss ready (toggle off)")
                : $"Kills {w.KillsInSubzone(w.UnlockedSubzone)} / {k}";
            _level.text = $"Level {c.Level}";
            _xp.Set(c.Xp / _formulas.XpToNext(c.Level));
            _gold.text = "Gold " + NumberFormat.Short(st.Gold) + " · Mat " + NumberFormat.Short(st.Wallet.Materials);
            _unspent.text = $"Points: {c.UnspentPoints}";
            bool skillPoints = _session.Skills != null && _session.Skills.Unspent(c) > 0;
            _skills.text = skillPoints ? "Skills •" : "Skills";
            _skills.style.color = skillPoints ? Ui.Gold : Ui.Text;

            foreach (var (id, name) in AttributeNames)
                _values[id].text = $"{name} {_session.Progression.Total(c, id)}";
            foreach (var b in _plusButtons)
                b.SetEnabled(c.UnspentPoints > 0);
            bool isCustom = c.Preset == ProgressionSystem.CustomPreset;
            if (_custom.value != isCustom) _custom.SetValueWithoutNotify(isCustom);
            if (_autoToggle.value != c.AutoAllocateOnLevelUp)
                _autoToggle.SetValueWithoutNotify(c.AutoAllocateOnLevelUp);
            if (_bossToggle.value != w.FightBossWhenAvailable)
                _bossToggle.SetValueWithoutNotify(w.FightBossWhenAvailable);
        }

        /// <summary>"the Training Grounds" → "The Training Grounds" at the start of a line.</summary>
        internal static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
