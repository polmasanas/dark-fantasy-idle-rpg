#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Session;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Development-only controls (never in release builds): game speed, placeholder gear level
    /// (stands in for loot until M3) and quick levels.
    /// </summary>
    internal sealed class DebugPanel
    {
        public readonly VisualElement Root;
        private readonly GameBootstrap _game;
        private readonly GameSession _session;
        private readonly GameFormulas _formulas;
        private readonly Label _info;

        public DebugPanel(GameBootstrap game, GameSession session, BalanceConfig config)
        {
            _game = game;
            _session = session;
            _formulas = new GameFormulas(config);

            Root = Ui.PanelBox();
            _info = Ui.Label("", 22, Ui.Muted);
            Root.Add(_info);

            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.Add(Ui.Button("x1", () => _game.GameSpeed = 1f, 22));
            row.Add(Ui.Button("x5", () => _game.GameSpeed = 5f, 22));
            row.Add(Ui.Button("x20", () => _game.GameSpeed = 20f, 22));
            row.Add(Ui.Button("Gear +10", AddGearLevels, 22));
            row.Add(Ui.Button("+5 levels", AddLevels, 22));
            Root.Add(row);
        }

        public void Refresh()
        {
            var c = _session.State.Character;
            _info.text = $"DEV · speed x{_game.GameSpeed:0} · gear item level {c.WeaponItemLevel:0}"
                         + (c.PlaceholderArmourItemLevel > 0 ? " (+ placeholder armour)" : " (no armour)");
        }

        private void AddGearLevels()
        {
            var c = _session.State.Character;
            c.WeaponItemLevel += 10;
            c.PlaceholderArmourItemLevel = c.WeaponItemLevel;
        }

        private void AddLevels()
        {
            var c = _session.State.Character;
            double xp = -c.Xp;
            for (int l = c.Level; l < c.Level + 5; l++) xp += _formulas.XpToNext(l);
            _session.Progression.AddXp(c, xp + 1e-6);
        }
    }
}
#endif
