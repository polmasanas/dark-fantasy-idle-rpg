using System;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Look;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// The fight scene (CD-2): the region's layered background, the hero and the enemy as silhouettes,
    /// HP bars and floating numbers driven by the fight's event log. Region bosses pull the camera back:
    /// the world and the hero shrink while the boss looms in from the right. Presentation only: turning
    /// animations off would not change any result (M2).
    /// </summary>
    internal sealed class CombatView : IFightView
    {
        private const float HeroHeightShare = 0.34f;   // of the arena height (spec §3.1: about 1/6 of the screen)
        private const float EnemyScale = 0.9f;         // normal enemy vs hero
        private const float BossScale = 4f;            // boss vs hero, drawn larger than the frame
        private const float HeroLeftShare = 0.08f;
        private const float BossSlideShare = 0.4f;     // boss enters from this far right, share of arena width

        public readonly VisualElement Root;

        /// <summary>True when the boss camera starts pulling back, false when it returns (music hooks on this).</summary>
        public event Action<bool> BossCameraChanged;

        private readonly LookConfig _look;
        private readonly RegionLooks _regions;
        private readonly EnemyLooks _enemies;
        private readonly ClassLook _heroLook;
        private readonly Label _status;
        private readonly VisualElement _arena;
        private readonly SceneryView _scenery;
        private readonly Silhouette _hero;
        private readonly Silhouette _enemy;
        private readonly Label _enemyName;
        private readonly Bar _playerHp;
        private readonly Bar _enemyHp;
        private readonly Label _playerHpText;
        private readonly Label _enemyHpText;

        private double _playerHpNow, _playerHpMax, _enemyHpNow, _enemyHpMax;
        private bool _bossFight;
        private bool _enemyIsBoss;
        private bool _cameraBack;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private float _zoomSpeed = 1f;

        public CombatView(LookConfig look, RegionLooks regions, EnemyLooks enemies, ClassId heroClass)
        {
            _look = look ?? throw new ArgumentNullException(nameof(look));
            _regions = regions ?? throw new ArgumentNullException(nameof(regions));
            _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            _heroLook = enemies.ForClass(heroClass);

            Root = new VisualElement();
            Root.style.flexGrow = 1;
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.marginBottom = 8;

            _status = Ui.Label("", 28, Ui.Gold, true);
            _status.style.minHeight = 40;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.unityTextAlign = TextAnchor.MiddleCenter;
            Root.Add(_status);

            _arena = new VisualElement();
            _arena.style.flexGrow = 1;
            _arena.style.minHeight = 320;
            _arena.style.overflow = Overflow.Hidden;
            _arena.style.borderTopWidth = 2;
            _arena.style.borderBottomWidth = 2;
            _arena.style.borderTopColor = Ui.Iron;
            _arena.style.borderBottomColor = Ui.Iron;
            Root.Add(_arena);

            _scenery = new SceneryView();
            _arena.Add(_scenery.Sky);
            _arena.Add(_scenery.World);
            _hero = new Silhouette(false);
            _enemy = new Silhouette(true);
            _scenery.World.Add(_hero.Root);   // the hero shrinks with the world when the camera pulls back
            _arena.Add(_enemy.Root);          // the enemy stays full size, so a boss looms

            _enemyName = Ui.Title("", 26);
            _enemyName.style.position = Position.Absolute;
            _enemyName.style.top = 8;
            _enemyName.style.right = 12;
            _enemyName.style.unityTextAlign = TextAnchor.UpperRight;
            _enemyName.pickingMode = PickingMode.Ignore;
            _arena.Add(_enemyName);

            var bars = Ui.Row(Justify.SpaceBetween);
            bars.style.marginTop = 8;
            var left = Ui.Column();
            left.style.alignItems = Align.FlexStart;
            left.Add(Ui.Title(Capitalise(heroClass.ToString()), 24));
            _playerHp = new Bar(Ui.Hp, 300, 14);
            left.Add(_playerHp.Root);
            _playerHpText = Ui.Label("", 22, Ui.Muted);
            left.Add(_playerHpText);
            var right = Ui.Column();
            right.style.alignItems = Align.FlexEnd;
            right.Add(Ui.Label("Enemy", 22, Ui.Muted));
            _enemyHp = new Bar(Ui.Enemy, 300, 14);
            right.Add(_enemyHp.Root);
            _enemyHpText = Ui.Label("", 22, Ui.Muted);
            right.Add(_enemyHpText);
            bars.Add(left);
            bars.Add(right);
            Root.Add(bars);

            _hero.Show(_heroLook.Body, Ui.Hex(_heroLook.Glow));
        }

        public void SetStatus(string text) => _status.text = text;

        /// <summary>Snaps the camera back to normal (boot, offline catch-up, a fight thrown away) and shows the region.</summary>
        public void Reset(int region)
        {
            _zoom = _zoomTarget = 1f;
            _scenery.World.style.scale = new Scale(Vector3.one);
            _enemy.Root.style.translate = new Translate(0, 0);
            ShowRegion(region);
            if (_cameraBack)
            {
                _cameraBack = false;
                BossCameraChanged?.Invoke(false);
            }
        }

        public void BeginFight(PreparedFight f)
        {
            _bossFight = f.Encounter.IsBoss && f.Enemy.CanKillPlayer;
            ShowRegion(f.Encounter.Region);
            var look = _enemies.For(f.Encounter);
            _enemyIsBoss = look.Rank == LookRank.Boss && look.UseBossCamera;
            _enemy.Show(look.Body, Ui.Hex(look.GlowHex));
            _enemyName.text = look.Rank == LookRank.Boss ? look.DisplayName : $"{look.DisplayName} · lvl {f.Encounter.Level}";
            _enemyName.style.fontSize = look.Rank == LookRank.Boss ? 28 : 22;
            if (f.Encounter.IsTraining) SetStatus("The drillmaster wants a sparring bout. Beat her within 60 s. She cannot defeat you.");

            if (look.UseBossCamera) MoveCamera(true);
            else if (_cameraBack) MoveCamera(false);

            _playerHpMax = _playerHpNow = f.Player.MaxHp;
            _enemyHpMax = _enemyHpNow = f.Enemy.Hp;
            UpdateBars();
        }

        public void ShowEvent(PreparedFight f, CombatEvent e)
        {
            switch (e.Type)
            {
                case CombatEventType.PlayerHit:
                    _hero.Lunge();
                    _enemy.Flash();
                    DamageEnemy(e.Amount);
                    Float(true, NumberFormat.Short(e.Amount), Ui.Text, 34);
                    break;
                case CombatEventType.PlayerCrit:
                    _hero.Lunge();
                    _enemy.Flash();
                    DamageEnemy(e.Amount);
                    Float(true, NumberFormat.Short(e.Amount) + "!", Ui.Gold, 46);
                    break;
                case CombatEventType.Counter:
                    _hero.Lunge();
                    _enemy.Flash();
                    DamageEnemy(e.Amount);
                    Float(true, "Counter " + NumberFormat.Short(e.Amount), Ui.Gold, 34);
                    break;
                case CombatEventType.PlayerMiss:
                    _hero.Lunge();
                    Float(true, "Miss", Ui.Muted, 30);
                    break;
                case CombatEventType.EnemyHit:
                    _enemy.Lunge();
                    _hero.Flash();
                    _playerHpNow -= e.Amount;
                    if (!_bossFight && _playerHpNow < 1) _playerHpNow = 1; // normal enemies cannot kill (R2-1c)
                    Float(false, "-" + NumberFormat.Short(e.Amount), Ui.Damage, 32);
                    break;
                case CombatEventType.EnemyDied:
                    _enemy.Fade();
                    break;
                case CombatEventType.PlayerDied:
                    _hero.Fade();
                    SetStatus("Defeated by the boss. Clear a new wave to challenge it again.");
                    break;
                case CombatEventType.Escaped:
                    SetStatus("The enemy escaped!");
                    break;
                case CombatEventType.TimedOut:
                    SetStatus("The boss enraged: out of time. Clear a new wave to challenge it again.");
                    break;
            }
            UpdateBars();
        }

        public void EndFight(PreparedFight f)
        {
            if (_cameraBack) MoveCamera(false);
        }

        /// <summary>Animations: call every frame with real time (not game speed).</summary>
        public void Tick(float dt)
        {
            float w = _arena.layout.width, h = _arena.layout.height;
            if (float.IsNaN(w) || w <= 0 || h <= 0) return;

            if (!Mathf.Approximately(_zoom, _zoomTarget))
                _zoom = Mathf.MoveTowards(_zoom, _zoomTarget, _zoomSpeed * dt);
            _scenery.World.style.scale = new Scale(new Vector3(_zoom, _zoom, 1f));
            _scenery.Tick(dt);

            float ground = h * SceneryView.GroundShare;
            float heroH = h * HeroHeightShare;
            Place(_hero, heroH, ground);
            // the hero lives inside the 170%-wide world: convert the arena share to world coordinates
            _hero.Root.style.left = w * 0.35f + w * HeroLeftShare;

            float enemyH = heroH * (_enemyIsBoss ? BossScale : EnemyScale);
            Place(_enemy, enemyH, ground);
            float enemyW = enemyH * _enemy.Aspect;
            _enemy.Root.style.left = _enemyIsBoss ? w - enemyW * 0.8f : w * 0.62f;
            if (_enemyIsBoss)
            {
                float back = Mathf.InverseLerp(1f, (float)_look.Camera.BossZoom, _zoom); // 0 → 1 as the camera pulls back
                _enemy.Root.style.translate = new Translate(w * BossSlideShare * (1f - back), 0);
            }

            _hero.Tick(dt, w);
            _enemy.Tick(dt, w);
        }

        private void ShowRegion(int region)
        {
            var look = _regions.For(Math.Max(1, region));
            _scenery.SetRegion(look, look.Kit == "training");
        }

        private void MoveCamera(bool back)
        {
            _cameraBack = back;
            float from = _zoom;
            _zoomTarget = back ? (float)_look.Camera.BossZoom : 1f;
            float seconds = (float)(back ? _look.Camera.ZoomInS : _look.Camera.ZoomOutS);
            _zoomSpeed = seconds > 0 ? Mathf.Abs(from - _zoomTarget) / seconds : 1000f;
            BossCameraChanged?.Invoke(back);
        }

        private static void Place(Silhouette s, float height, float ground)
        {
            s.Root.style.height = height;
            s.Root.style.width = height * s.Aspect;
            s.Root.style.bottom = ground;
        }

        private void DamageEnemy(double amount)
        {
            _enemyHpNow = Math.Max(0, _enemyHpNow - amount);
        }

        private void UpdateBars()
        {
            _playerHp.Set(_playerHpMax > 0 ? _playerHpNow / _playerHpMax : 0);
            _enemyHp.Set(_enemyHpMax > 0 ? _enemyHpNow / _enemyHpMax : 0);
            _playerHpText.text = NumberFormat.Short(_playerHpNow) + " / " + NumberFormat.Short(_playerHpMax);
            _enemyHpText.text = NumberFormat.Short(_enemyHpNow) + " / " + NumberFormat.Short(_enemyHpMax);
        }

        /// <summary>A number in carved capitals that rises and fades over the hero or the enemy.</summary>
        private void Float(bool overEnemy, string text, Color color, int size)
        {
            var label = Ui.Title(text, size, color);
            label.style.position = Position.Absolute;
            float w = _arena.layout.width;
            float x = (overEnemy ? 0.62f : HeroLeftShare) + UnityEngine.Random.Range(0f, 0.12f);
            label.style.left = float.IsNaN(w) ? 0 : w * x;
            label.style.top = Length.Percent(30);
            label.pickingMode = PickingMode.Ignore;
            _arena.Add(label);

            float age = 0f;
            const int stepMs = 30;
            const float lifeMs = 900f;
            label.schedule.Execute(() =>
            {
                age += stepMs;
                label.style.translate = new Translate(0, -age * 0.12f);
                label.style.opacity = Mathf.Clamp01(1f - age / lifeMs);
            }).Every(stepMs).Until(() => age >= lifeMs);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn((long)lifeMs + 50);
        }

        private static string Capitalise(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
