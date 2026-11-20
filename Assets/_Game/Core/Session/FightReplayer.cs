using System;
using DarkFantasy.Core.Combat;

namespace DarkFantasy.Core.Session
{
    /// <summary>What the presentation layer implements to show fights.</summary>
    public interface IFightView
    {
        void BeginFight(PreparedFight fight);
        void ShowEvent(PreparedFight fight, CombatEvent e);
        void EndFight(PreparedFight fight);
    }

    /// <summary>
    /// Replays resolved fights in (scaled) real time: shows each combat event when its time comes,
    /// then applies the fight to the session after its duration plus the respawn gap. Results never
    /// depend on frame rate or game speed; only when they appear on screen does.
    /// </summary>
    public sealed class FightReplayer
    {
        private const double Epsilon = 1e-9;

        private readonly GameSession _session;
        private readonly IFightView _view;
        private PreparedFight _current;
        private double _elapsed;
        private int _next;

        public FightReplayer(GameSession session, IFightView view)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public PreparedFight Current => _current;
        public double Elapsed => _elapsed;

        /// <summary>
        /// Discards the fight being shown without applying it (e.g. before simulating time away, so a
        /// fight prepared from the old state never lands afterwards). The next tick starts a new fight.
        /// </summary>
        public void Abandon()
        {
            if (_current != null) _session.DropPreparedFight();
            _current = null;
            _elapsed = 0;
            _next = 0;
        }

        /// <summary>Advances the replay by <paramref name="seconds"/> of game time.</summary>
        public void Tick(double seconds)
        {
            if (!(seconds > 0) || double.IsInfinity(seconds)) return;
            double remaining = seconds;
            while (true)
            {
                if (_current == null)
                {
                    _current = _session.PrepareNextFight();
                    _elapsed = 0;
                    _next = 0;
                    _view.BeginFight(_current);
                }

                double total = _current.Result.Duration + _session.RespawnSeconds;
                double step = Math.Min(remaining, total - _elapsed);
                _elapsed += step;
                remaining -= step;
                ShowDueEvents(_elapsed);

                if (_elapsed < total - Epsilon) return;

                ShowDueEvents(double.PositiveInfinity);
                var finished = _current;
                _current = null;
                _session.Apply(finished);
                _view.EndFight(finished);
                if (remaining <= Epsilon) return;
            }
        }

        private void ShowDueEvents(double upTo)
        {
            var events = _current.Result.Events;
            while (_next < events.Count && events[_next].Time <= upTo + Epsilon)
            {
                _view.ShowEvent(_current, events[_next]);
                _next++;
            }
        }
    }
}
