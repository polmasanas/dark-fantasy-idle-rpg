using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Placeholder combat view (M2): coloured boxes, HP bars and floating numbers driven by the
    /// fight's event log. Real sprites and animations replace this without touching game logic.
    /// </summary>
    internal sealed class CombatView : IFightView
    {
        public readonly VisualElement Root;
        private readonly Label _status;
        private readonly Label _enemyName;
        private readonly VisualElement _playerBox;
        private readonly VisualElement _enemyBox;
        private readonly Bar _playerHp;
        private readonly Bar _enemyHp;
        private readonly Label _playerHpText;
        private readonly Label _enemyHpText;

        private double _playerHpNow, _playerHpMax, _enemyHpNow, _enemyHpMax;
        private bool _bossFight;

        public CombatView()
        {
            Root = Ui.Column();
            Root.style.flexGrow = 1;
            Root.style.justifyContent = Justify.Center;

            _status = Ui.Label("", 30, Ui.Gold, true);
            _status.style.marginBottom = 24;
            Root.Add(_status);

            var arena = Ui.Row(Justify.SpaceAround);
            arena.style.width = new Length(100, LengthUnit.Percent);
            Root.Add(arena);

            var playerCol = Ui.Column();
            playerCol.Add(Ui.Label("Fighter", 30, Ui.Text, true));
            _playerBox = Ui.Box(Ui.Player, 140, 220);
            playerCol.Add(_playerBox);
            _playerHp = new Bar(Ui.Hp, 220, 18);
            _playerHp.Root.style.marginTop = 10;
            playerCol.Add(_playerHp.Root);
            _playerHpText = Ui.Label("", 24, Ui.Muted);
            playerCol.Add(_playerHpText);
            arena.Add(playerCol);

            var enemyCol = Ui.Column();
            _enemyName = Ui.Label("", 30, Ui.Text, true);
            enemyCol.Add(_enemyName);
            _enemyBox = Ui.Box(Ui.Enemy, 140, 220);
            enemyCol.Add(_enemyBox);
            _enemyHp = new Bar(Ui.Hp, 220, 18);
            _enemyHp.Root.style.marginTop = 10;
            enemyCol.Add(_enemyHp.Root);
            _enemyHpText = Ui.Label("", 24, Ui.Muted);
            enemyCol.Add(_enemyHpText);
            arena.Add(enemyCol);
        }

        public void SetStatus(string text) => _status.text = text;

        public void BeginFight(PreparedFight f)
        {
            _bossFight = f.Encounter.IsBoss;
            _enemyName.text = _bossFight
                ? $"Region {f.Encounter.Region} Boss · lvl {f.Encounter.Level}"
                : $"Enemy · lvl {f.Encounter.Level}";
            _enemyBox.style.backgroundColor = _bossFight ? Ui.Boss : Ui.Enemy;
            _enemyBox.style.width = _bossFight ? 190 : 140;
            _enemyBox.style.height = _bossFight ? 280 : 220;
            _enemyBox.style.opacity = 1f;
            _playerBox.style.opacity = 1f;

            _playerHpMax = _playerHpNow = f.Player.MaxHp;
            _enemyHpMax = _enemyHpNow = f.Enemy.Hp;
            UpdateBars();
        }

        public void ShowEvent(PreparedFight f, CombatEvent e)
        {
            switch (e.Type)
            {
                case CombatEventType.PlayerHit:
                    DamageEnemy(e.Amount);
                    Float(_enemyBox, NumberFormat.Short(e.Amount), Ui.Text, 34);
                    break;
                case CombatEventType.PlayerCrit:
                    DamageEnemy(e.Amount);
                    Float(_enemyBox, NumberFormat.Short(e.Amount) + "!", Ui.Gold, 46);
                    break;
                case CombatEventType.Counter:
                    DamageEnemy(e.Amount);
                    Float(_enemyBox, "Counter " + NumberFormat.Short(e.Amount), new Color(1f, 0.6f, 0.2f), 34);
                    break;
                case CombatEventType.PlayerMiss:
                    Float(_enemyBox, "Miss", Ui.Muted, 30);
                    break;
                case CombatEventType.EnemyHit:
                    _playerHpNow -= e.Amount;
                    if (!_bossFight && _playerHpNow < 1) _playerHpNow = 1; // normal enemies cannot kill (R2-1c)
                    Float(_playerBox, "-" + NumberFormat.Short(e.Amount), new Color(1f, 0.35f, 0.35f), 32);
                    break;
                case CombatEventType.EnemyDied:
                    _enemyBox.style.opacity = 0.25f;
                    break;
                case CombatEventType.PlayerDied:
                    _playerBox.style.opacity = 0.25f;
                    SetStatus("Defeated by the boss. Retrying in 60 s.");
                    break;
                case CombatEventType.Escaped:
                    SetStatus("The enemy escaped!");
                    break;
                case CombatEventType.TimedOut:
                    SetStatus("The boss enraged: out of time. Retrying in 60 s.");
                    break;
            }
            UpdateBars();
        }

        public void EndFight(PreparedFight f)
        {
        }

        private void DamageEnemy(double amount)
        {
            _enemyHpNow = System.Math.Max(0, _enemyHpNow - amount);
        }

        private void UpdateBars()
        {
            _playerHp.Set(_playerHpMax > 0 ? _playerHpNow / _playerHpMax : 0);
            _enemyHp.Set(_enemyHpMax > 0 ? _enemyHpNow / _enemyHpMax : 0);
            _playerHpText.text = NumberFormat.Short(_playerHpNow) + " / " + NumberFormat.Short(_playerHpMax);
            _enemyHpText.text = NumberFormat.Short(_enemyHpNow) + " / " + NumberFormat.Short(_enemyHpMax);
        }

        /// <summary>A number that rises and fades above a box, then removes itself.</summary>
        private static void Float(VisualElement target, string text, Color color, int size)
        {
            var label = Ui.Label(text, size, color, true);
            label.style.position = Position.Absolute;
            label.style.left = UnityEngine.Random.Range(0f, 60f);
            label.style.top = 40;
            label.pickingMode = PickingMode.Ignore;
            target.Add(label);

            float age = 0f;
            const int stepMs = 30;
            const float lifeMs = 900f;
            label.schedule.Execute(() =>
            {
                age += stepMs;
                label.style.top = 40 - age * 0.12f;
                label.style.opacity = Mathf.Clamp01(1f - age / lifeMs);
            }).Every(stepMs).Until(() => age >= lifeMs);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn((long)lifeMs + 50);
        }
    }
}
