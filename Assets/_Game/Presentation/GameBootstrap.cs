using System;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// M2 entry point: loads the balance config, starts a Fighter run and replays fights in the
    /// placeholder views. Put it on a GameObject with a UIDocument (see Docs/M2_SCENE_SETUP.md).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Assets/_Game/Data/balance_config.json")]
        [SerializeField] private TextAsset balanceConfig;

        [Tooltip("0 = random seed each run; any other value replays the same run.")]
        [SerializeField] private int seed;

        public float GameSpeed { get; set; } = 1f;

        private GameSession _session;
        private FightReplayer _replayer;
        private CombatView _combat;
        private HudView _hud;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private DebugPanel _debug;
#endif

        private void Start()
        {
            if (balanceConfig == null)
            {
                Debug.LogError("GameBootstrap: assign Assets/_Game/Data/balance_config.json to 'Balance Config'.");
                enabled = false;
                return;
            }

            BalanceConfig config;
            try
            {
                config = BalanceConfigLoader.FromJson(balanceConfig.text);
            }
            catch (BalanceConfigException e)
            {
                Debug.LogError("GameBootstrap: " + e.Message);
                enabled = false;
                return;
            }

            ulong runSeed = seed != 0 ? (ulong)seed : (ulong)DateTime.UtcNow.Ticks;
            Debug.Log("GameBootstrap: starting a Fighter run with seed " + runSeed);
            _session = new GameSession(config, GameState.NewRun(ClassId.Fighter), new Pcg32(runSeed), new EventBus());

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = Ui.Background;
            root.style.paddingTop = 24;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;

            _hud = new HudView(_session, config);
            _combat = new CombatView();
            root.Add(_hud.Top);
            root.Add(_combat.Root);
            root.Add(_hud.Bottom);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debug = new DebugPanel(this, _session, config);
            root.Add(_debug.Root);
#endif

            _session.Events.Subscribe<WorldChanged>(OnWorldChanged);
            _session.Events.Subscribe<LevelUp>(e => _combat.SetStatus($"Level up! Level {e.Level}"));
            _replayer = new FightReplayer(_session, _combat);
        }

        private void Update()
        {
            if (_replayer == null) return;
            _replayer.Tick(Time.deltaTime * GameSpeed);
            _hud.Refresh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debug.Refresh();
#endif
        }

        private void OnWorldChanged(WorldChanged e)
        {
            switch (e.Change)
            {
                case WorldChange.SubzoneUnlocked:
                    _combat.SetStatus($"Subzone {e.World.Region}-{e.World.UnlockedSubzone} unlocked");
                    break;
                case WorldChange.BossDefeated:
                    _combat.SetStatus($"Boss defeated! Welcome to Region {e.World.Region}");
                    break;
                case WorldChange.Retreated:
                    _combat.SetStatus("Too strong: retreating one subzone for 60 s");
                    break;
            }
        }
    }
}
