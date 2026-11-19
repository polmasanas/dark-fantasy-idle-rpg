using System;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;
using DarkFantasy.Core.World;

namespace DarkFantasy.Core.Session
{
    /// <summary>
    /// Runs the idle loop: choose an encounter, resolve the fight, apply rewards and world rules.
    /// Two-phase so the view can replay a fight before its results land
    /// (<see cref="PrepareNextFight"/> then <see cref="Apply"/>); <see cref="Advance"/> does both
    /// for headless play (tests, and offline progress in M4).
    /// </summary>
    public sealed class GameSession
    {
        private readonly BalanceConfig _c;
        private readonly IRng _rng;
        private readonly GameFormulas _f;
        private readonly CombatEngine _engine;
        private readonly StatCalculator _stats;
        private readonly CharacterStatsBuilder _builder;
        private readonly EnemyFactory _enemies;

        public GameState State { get; }
        public EventBus Events { get; }
        public ProgressionSystem Progression { get; }
        public WorldSystem World { get; }

        public GameSession(BalanceConfig config, GameState state, IRng rng, EventBus events)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            _f = new GameFormulas(config);
            _engine = new CombatEngine(config);
            _stats = new StatCalculator(config);
            _builder = new CharacterStatsBuilder(config);
            _enemies = new EnemyFactory(config);
            Progression = new ProgressionSystem(config);
            World = new WorldSystem(config);
        }

        /// <summary>Pause between a fight ending and the next enemy appearing (R2-1).</summary>
        public double RespawnSeconds => _c.Combat.EnemyRespawnS;

        /// <summary>Current combat stats of the active character.</summary>
        public CombatStats PlayerStats() => _stats.Compute(_builder.Build(State.Character));

        /// <summary>Resolves the next fight without changing the game state (only the RNG advances).</summary>
        public PreparedFight PrepareNextFight()
        {
            var encounter = World.NextEncounter(State.World, State.GameTime);
            var enemy = _enemies.Create(encounter, _rng);
            var player = PlayerStats();
            return new PreparedFight
            {
                Encounter = encounter,
                Enemy = enemy,
                Player = player,
                Result = _engine.Resolve(player, enemy, _rng, true),
            };
        }

        /// <summary>Applies a prepared fight: game time, rewards, levels and world progression.</summary>
        public void Apply(PreparedFight fight)
        {
            if (fight == null) throw new ArgumentNullException(nameof(fight));
            State.GameTime += fight.Result.Duration + _c.Combat.EnemyRespawnS;

            if (fight.Result.Outcome == FightOutcome.Won)
            {
                var e = fight.Encounter;
                double xp = _f.EnemyXp(e.Level, State.Character.Level, e.IsBoss);
                double gold = _f.GoldDrop(e.Level, e.IsBoss);
                State.Gold += gold;
                int before = State.Character.Level;
                Progression.AddXp(State.Character, xp);
                Events.Publish(new EnemyKilled(e, xp, gold));
                for (int lvl = before + 1; lvl <= State.Character.Level; lvl++)
                    Events.Publish(new LevelUp(lvl));
            }

            var change = World.OnFightFinished(State.World, fight.Encounter, fight.Result.Outcome, State.GameTime);
            Events.Publish(new FightResolved(fight));
            if (change != WorldChange.None)
                Events.Publish(new WorldChanged(change, State.World));
        }

        /// <summary>Plays fights headlessly until <paramref name="seconds"/> of game time are used. Returns fights played.</summary>
        public int Advance(double seconds)
        {
            double end = State.GameTime + Math.Max(0, seconds);
            int fights = 0;
            while (State.GameTime < end)
            {
                Apply(PrepareNextFight());
                fights++;
            }
            return fights;
        }
    }
}
