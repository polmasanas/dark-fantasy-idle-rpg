using System;
using System.Diagnostics;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Look;
using DarkFantasy.Core.Offline;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Entry point: loads the balance config and the save (M4), credits time away, and replays fights
    /// in the placeholder views. Put it on a GameObject with a UIDocument (see Docs/M2_SCENE_SETUP.md).
    /// Saves every save.autosave_interval_s, when the app is paused or closed, and after key moments.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Assets/_Game/Data/balance_config.json")]
        [SerializeField] private TextAsset balanceConfig;

        [Tooltip("Assets/_Game/Data/look_config.json (filled in automatically in the editor)")]
        [SerializeField] private TextAsset lookConfig;

        [Tooltip("Assets/_Game/Data/skills_config.json (filled in automatically in the editor)")]
        [SerializeField] private TextAsset skillsConfig;

        [Tooltip("0 = random seed for a new run; any other value replays the same run. Ignored when a save exists.")]
        [SerializeField] private int seed;

        public float GameSpeed { get; set; } = 1f;

        private BalanceConfig _config;
        private LookConfig _look;
        private RegionLooks _regions;
        private EnemyLooks _enemyLooks;
        private MusicService _music;
        private readonly IClock _clock = new UnityClock();
        private SaveService _saves;
        private OfflineCalculator _offline;
        private Pcg32 _rng;
        private double _playtime;
        private float _sinceSave;
        private bool _saveRequested;
        private bool _pausedWithSave;

        private GameSession _session;
        private FightReplayer _replayer;
        private CombatView _combat;
        private HudView _hud;
        private InventoryView _inventory;
        private SkillsView _skills;
        private CustomPresetView _customPreset;
        private OfflineReportView _report;
        private HeroBarView _heroBar;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private DebugPanel _debug;
#endif

#if UNITY_EDITOR
        /// <summary>Fills in the look config in the editor so existing scenes need no manual step.</summary>
        private void OnValidate()
        {
            if (lookConfig == null)
                lookConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Data/look_config.json");
            if (skillsConfig == null)
                skillsConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Data/skills_config.json");
        }
#endif

        private void Start()
        {
            if (balanceConfig == null)
            {
                Debug.LogError("GameBootstrap: assign Assets/_Game/Data/balance_config.json to 'Balance Config'.");
                enabled = false;
                return;
            }

            try
            {
                _config = BalanceConfigLoader.FromJson(balanceConfig.text);
#if UNITY_EDITOR
                if (skillsConfig == null)
                    skillsConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Data/skills_config.json");
#endif
                if (skillsConfig == null)
                {
                    Debug.LogError("GameBootstrap: assign Assets/_Game/Data/skills_config.json to 'Skills Config'.");
                    enabled = false;
                    return;
                }
                _config.Skills = SkillsConfigLoader.FromJson(skillsConfig.text);
            }
            catch (BalanceConfigException e)
            {
                Debug.LogError("GameBootstrap: " + e.Message);
                enabled = false;
                return;
            }

            if (lookConfig == null)
            {
                Debug.LogError("GameBootstrap: assign Assets/_Game/Data/look_config.json to 'Look Config'.");
                enabled = false;
                return;
            }

            try
            {
                _look = LookConfigLoader.FromJson(lookConfig.text);
            }
            catch (LookConfigException e)
            {
                Debug.LogError("GameBootstrap: look_config.json: " + e.Message);
                enabled = false;
                return;
            }

            Ui.UseLook(_look);
            _regions = new RegionLooks(_look);
            _enemyLooks = new EnemyLooks(_look);
            _music = new MusicService(gameObject, _look.Audio);
            _offline = new OfflineCalculator(_config);
            Boot(new SaveService(Application.persistentDataPath, _clock));
        }

        /// <summary>Loads the save (or starts a new run) and builds the views.</summary>
        private void Boot(SaveService saves)
        {
            _saves = saves;
            _sinceSave = 0;
            _saveRequested = false;
            _pausedWithSave = false;

            var load = _saves.Load();
            GameState state;
            OfflineCredit? credit = null;
            string notice = null;
            if (load.Data != null)
            {
                state = load.Data.State;
                _rng = Pcg32.FromState(load.Data.RngState, load.Data.RngIncrement);
                _playtime = load.Data.PlaytimeSeconds;
                credit = _offline.Compute(load.Data.SavedUtcTicks, load.Data.SavedUptimeSeconds, _clock.UtcNowTicks, _clock.UptimeSeconds);
            }
            else
            {
                ulong runSeed = seed != 0 ? (ulong)seed : (ulong)DateTime.UtcNow.Ticks;
                Debug.Log("GameBootstrap: no save, starting a Fighter run with seed " + runSeed);
                state = GameState.NewRun(ClassId.Fighter);
                _rng = new Pcg32(runSeed);
                _playtime = 0;
                if (load.NewerVersionFound)
                    notice = "Your save comes from a newer version of the game. Update the game to continue it. " +
                             "Nothing is saved until then, so the save stays safe.";
                else if (load.Rejected.Count > 0)
                    notice = "The save files could not be read, so a new game has started.";
            }

            _session = new GameSession(_config, state, _rng, new EventBus());
            BuildViews();

            if (credit.HasValue) ApplyOffline(credit.Value, null);
            else if (_session.State.PendingOfflineLoot.Count > 0) _report.Show(null, default, _config.Offline.Efficiency);
            if (notice != null) Toast(notice);
        }

        private void BuildViews()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            var theme = Resources.Load<StyleSheet>("UI/IronEmber");
            if (theme != null && !root.styleSheets.Contains(theme)) root.styleSheets.Add(theme);
            else if (theme == null) Debug.LogWarning("GameBootstrap: Resources/UI/IronEmber.uss not found; using default UI colours.");
            root.style.flexGrow = 1;
            root.style.backgroundColor = Ui.Background;
            root.style.paddingTop = 24;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;

            _inventory = new InventoryView(_session, _config);
            _customPreset = new CustomPresetView(_session, _config);
            _skills = new SkillsView(_session, _config, _look);
            _hud = new HudView(_session, _config, _regions, _inventory.Open, _customPreset.Open, _skills.Open);
            _combat = new CombatView(_look, _regions, _enemyLooks, _session.State.Character.Class);
            _combat.BossCameraChanged += on => _music.SetBoss(on);
            _music.SetBoss(false);
            _combat.Reset(_session.State.World.Region);
            _report = new OfflineReportView(_session, OnCollected);
            _heroBar = new HeroBarView(_session, OnHeroTapped);
            _hud.Top.Add(_heroBar.Root);
            root.Add(_hud.Top);
            root.Add(_combat.Root);
            root.Add(_hud.Bottom);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debug = new DebugPanel(this, _session, _config);
            root.Add(_debug.Root);
#endif
            // overlays, added last so they draw on top
            root.Add(_inventory.Root);
            root.Add(_customPreset.Root);
            root.Add(_skills.Root);
            root.Add(_report.Root);

            _session.Events.Subscribe<WorldChanged>(OnWorldChanged);
            _session.Events.Subscribe<LevelUp>(e => _combat.SetStatus($"Level up! Level {e.Level}"));
            _session.Events.Subscribe<ItemDropped>(OnItemDropped);
            _session.Events.Subscribe<HeroesUnlocked>(_ =>
            {
                Toast("The Archer and the Mage join you. Tap a hero above to switch.");
                _heroBar.Pulse();
                _saveRequested = true;
            });
            _replayer = new FightReplayer(_session, _combat);
        }

        private void Update()
        {
            if (_replayer == null) return;
            _replayer.Tick(Time.deltaTime * GameSpeed);
            _combat.Tick(Time.unscaledDeltaTime);
            _heroBar.Refresh(Time.unscaledDeltaTime);
            _music.Tick(Time.unscaledDeltaTime);
            _hud.Refresh();
            _inventory.Tick(Time.deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debug.Refresh();
#endif
            _playtime += Time.unscaledDeltaTime;
            _sinceSave += Time.unscaledDeltaTime;
            if (_saveRequested || _sinceSave >= _config.Save.AutosaveIntervalS) SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_session == null) return;
            if (paused)
            {
                _pausedWithSave = SaveNow();
            }
            else if (_pausedWithSave)
            {
                // Back from the background: credit the time away like a fresh start.
                _pausedWithSave = false;
                ApplyOffline(_offline.Compute(_saves.LastSavedUtcTicks, _saves.LastSavedUptime, _clock.UtcNowTicks, _clock.UptimeSeconds), null);
            }
        }

        private void OnApplicationQuit()
        {
            if (_session != null) SaveNow();
        }

        /// <summary>Saves now. Returns false when saving is off or failed.</summary>
        public bool SaveNow()
        {
            _saveRequested = false;
            _sinceSave = 0;
            return _saves != null && _saves.Save(_session.State, _rng, _playtime);
        }

        /// <summary>Simulates the credited time silently, then shows the report (or collects at once for short absences).</summary>
        private void ApplyOffline(OfflineCredit credit, string extra)
        {
            if (!(credit.CreditedSeconds > 0) && credit.Adjustment != OfflineAdjustment.ClockRolledBack) return;
            _replayer.Abandon(); // a fight prepared before the absence must not land afterwards
            _combat.Reset(_session.State.World.Region);
            _combat.SetStatus("");
            var report = _session.SimulateOffline(credit.CreditedSeconds);
            if (credit.ShowReport || credit.Adjustment == OfflineAdjustment.ClockRolledBack)
                _report.Show(report, credit, _config.Offline.Efficiency, extra);
            else
                _session.Collect(); // short absence: credited silently (M4-1)
            _saveRequested = true;
        }

        private void OnCollected(CollectReport c)
        {
            string text = $"Collected {c.Kept} items";
            if (c.SalvagedBagFull > 0) text += $" · bag full: {c.SalvagedBagFull} salvaged";
            Toast(text);
            _saveRequested = true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Dev: pretend the player was away for <paramref name="seconds"/> (after a reboot, so only the cap applies).</summary>
        internal void SimulateAway(double seconds)
        {
            var credit = _offline.Compute(0, 0, (long)(seconds * TimeSpan.TicksPerSecond), -1);
            var sw = Stopwatch.StartNew();
            ApplyOffline(credit, null);
            sw.Stop();
            string ms = $"DEV: simulated {Ui.Duration(credit.CreditedSeconds)} of fighting in {sw.ElapsedMilliseconds} ms";
            Debug.Log(ms);
            Toast(ms);
        }

        /// <summary>Dev: deletes every save file and starts a new game.</summary>
        internal void DeleteSaveAndRestart()
        {
            _saves.DeleteAll();
            Boot(new SaveService(Application.persistentDataPath, _clock));
            Toast("DEV: save deleted, new game started");
        }
#endif

        /// <summary>Music on/off (dev panel for now; a settings screen comes in M11).</summary>
        internal bool SoundOn
        {
            get => _music != null && !_music.Muted;
            set
            {
                if (_music != null) _music.Muted = !value;
            }
        }

        /// <summary>Hero bar tap (M5-3): switch now, dropping the normal fight on screen; refused during boss fights.</summary>
        private void OnHeroTapped(ClassId hero)
        {
            switch (_session.SwitchTo(hero))
            {
                case SwitchResult.Switched:
                    _replayer.Abandon();
                    _combat.Reset(_session.State.World.Region);
                    _combat.ShowHero(hero);
                    Toast($"The {hero} takes the field");
                    _saveRequested = true;
                    break;
                case SwitchResult.Locked:
                    Toast("Beat the region 1 boss to unlock the Archer and the Mage");
                    break;
                case SwitchResult.BossFight:
                    Toast("You can't switch heroes during a boss fight");
                    break;
            }
        }

        /// <summary>Shows a short message in the combat status line.</summary>
        public void Toast(string text) => _combat?.SetStatus(text);

        private void OnItemDropped(ItemDropped e)
        {
            string name = $"{e.Item.Rarity} {_session.Items.DisplayName(e.Item)}";
            switch (e.Outcome)
            {
                case AddOutcome.Kept:
                    if (e.Item.Rarity >= Rarity.Rare || e.FromBoss)
                        Toast((_session.IsUpgrade(e.Item) ? "Upgrade! " : "Loot: ") + name);
                    break;
                case AddOutcome.SalvagedInventoryFull:
                    Toast("Bag full: salvaged " + name);
                    break;
            }
        }

        private void OnWorldChanged(WorldChanged e)
        {
            switch (e.Change)
            {
                case WorldChange.SubzoneUnlocked:
                    _combat.SetStatus($"Subzone {e.World.Region}-{e.World.UnlockedSubzone} unlocked");
                    break;
                case WorldChange.TrainingBossDefeated:
                    _combat.SetStatus("Training complete! Bosses appear after a full wave of kills. Untick 'Fight boss when available' to keep farming.");
                    _saveRequested = true;
                    break;
                case WorldChange.BossDefeated:
                    _combat.SetStatus($"Boss defeated! Welcome to {_regions.For(e.World.Region).Name}");
                    _saveRequested = true;
                    break;
                case WorldChange.Retreated:
                    _combat.SetStatus("Too strong: retreating one subzone for 60 s");
                    break;
            }
        }
    }
}
