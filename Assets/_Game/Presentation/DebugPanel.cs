#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>Development-only controls (never in release builds): game speed, quick levels, test drops, save and offline tests.</summary>
    internal sealed class DebugPanel
    {
        public readonly VisualElement Root;
        private readonly GameBootstrap _game;
        private readonly GameSession _session;
        private readonly GameFormulas _formulas;
        private readonly LootSystem _loot;
        private readonly Pcg32 _rng = new Pcg32((ulong)System.DateTime.UtcNow.Ticks, 7);
        private readonly Label _info;

        public DebugPanel(GameBootstrap game, GameSession session, BalanceConfig config)
        {
            _game = game;
            _session = session;
            _formulas = new GameFormulas(config);
            _loot = new LootSystem(config);

            Root = Ui.PanelBox();
            _info = Ui.Label("", 22, Ui.Muted);
            Root.Add(_info);

            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.Add(Ui.Button("x1", () => _game.GameSpeed = 1f, 22));
            row.Add(Ui.Button("x5", () => _game.GameSpeed = 5f, 22));
            row.Add(Ui.Button("x20", () => _game.GameSpeed = 20f, 22));
            row.Add(Ui.Button("+5 levels", AddLevels, 22));
            row.Add(Ui.Button("Drop Rare", () => Drop(Rarity.Rare), 22));
            row.Add(Ui.Button("Drop Epic", () => Drop(Rarity.Epic), 22));
            row.Add(Ui.Button("Drop Legendary", () => Drop(Rarity.Legendary), 22));
            Root.Add(row);

            var saveRow = Ui.Row();
            saveRow.style.flexWrap = Wrap.Wrap;
            saveRow.Add(Ui.Button("Simulate 12 h away", () => _game.SimulateAway(12 * 3600), 22));
            saveRow.Add(Ui.Button("Save now", () => _game.Toast(_game.SaveNow() ? "DEV: saved" : "DEV: saving is off"), 22));
            saveRow.Add(Ui.Button("Delete save", _game.DeleteSaveAndRestart, 22));
            Root.Add(saveRow);
        }

        public void Refresh()
        {
            _info.text = $"DEV · speed x{_game.GameSpeed:0}";
        }

        private void Drop(Rarity rarity)
        {
            var w = _session.State.World;
            int level = _formulas.SubzoneLevel(w.Region, w.UnlockedSubzone);
            var item = _loot.Roll(level, _rng, rarity);
            var st = _session.State;
            _session.Inventory.Add(st.Inventory, st.Wallet, item, _ => true);
            _game.Toast($"DEV: dropped {rarity} {_session.Items.DisplayName(item)} (lvl {level})");
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
