using System;
using System.Collections.Generic;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Combat
{
    public sealed partial class CombatEngine
    {
        /// <summary>
        /// Resolves a fight with skills (M6). <paramref name="cooldownLeft"/>[i] is the time left on
        /// <c>kit.Actives[i]</c> at the start and is written back with the time left at the end (M6-2). An empty kit
        /// takes the exact M5 path, so results without skills never change.
        /// </summary>
        public FightResult Resolve(CombatStats player, EnemySnapshot enemy, IRng rng, bool recordEvents, SkillKit kit, double[] cooldownLeft)
        {
            if (kit == null || kit.IsEmpty) return Resolve(player, enemy, rng, recordEvents);
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!(player.AttackInterval > 0) || !(enemy.AttackInterval > 0))
                throw new ArgumentException("Attack intervals must be positive.");
            if (cooldownLeft != null && cooldownLeft.Length < kit.Actives.Count)
                throw new ArgumentException("cooldownLeft needs one entry per active skill.", nameof(cooldownLeft));
            return new SkillFight(this, player, enemy, rng, recordEvents ? new List<CombatEvent>() : null, kit, cooldownLeft).Run();
        }

        /// <summary>One fight's mutable state on the skill path. Times are fight seconds from 0.</summary>
        private sealed class SkillFight
        {
            private readonly CombatEngine _e;
            private readonly CombatStats _p;
            private readonly EnemySnapshot _enemy;
            private readonly IRng _rng;
            private readonly List<CombatEvent> _events;
            private readonly SkillKit _kit;
            private readonly double[] _cooldownLeft;
            private readonly double[] _readyAt;

            private readonly double _limit;
            private readonly double _hitChance;
            private readonly double _damageTaken;
            private readonly double _tick;
            private double _enemyHp;
            private double _playerHp;
            private double _nextPlayer;
            private double _nextEnemy;

            // Damage over time: one per skill (a recast refreshes it).
            private readonly List<Dot> _dots = new List<Dot>();

            // Timed effects
            private double _buffDamage;
            private double _buffUntil = double.NegativeInfinity;
            private double _slow;
            private double _slowUntil = double.NegativeInfinity;

            // Once-per-fight passives and stacks
            private bool _healUsed;
            private bool _cheatUsed;
            private int _momentum;

            private sealed class Dot
            {
                public string SkillId;
                public Element Element;
                public double PerTick;
                public double NextTick;
                public int TicksLeft;
            }

            public SkillFight(CombatEngine engine, CombatStats p, EnemySnapshot enemy, IRng rng, List<CombatEvent> events,
                SkillKit kit, double[] cooldownLeft)
            {
                _e = engine;
                _p = p;
                _enemy = enemy;
                _rng = rng;
                _events = events;
                _kit = kit;
                _cooldownLeft = cooldownLeft;
                _readyAt = new double[kit.Actives.Count];
                for (int i = 0; i < _readyAt.Length; i++)
                    _readyAt[i] = cooldownLeft == null ? 0 : Math.Max(0, cooldownLeft[i]);
                _limit = enemy.IsBoss ? engine._c.Enemy.BossTimeLimitS : engine._c.Combat.EscapeTimerS;
                _hitChance = engine.HitChance(p, enemy);
                double taken = engine.DamageTaken(p, enemy) * (1 - Math.Min(1, kit.DamageTakenCut));
                if (enemy.IsBoss) taken *= 1 - Math.Min(1, kit.BossDamageTakenCut);
                _damageTaken = taken;
                _tick = engine._c.SkillRules != null && engine._c.SkillRules.DotTickS > 0 ? engine._c.SkillRules.DotTickS : 1.0;
                _enemyHp = enemy.Hp;
                _playerHp = p.MaxHp;
                _nextPlayer = p.AttackInterval;
                _nextEnemy = enemy.AttackInterval;
            }

            public FightResult Run()
            {
                while (true)
                {
                    // Order at equal times: damage-over-time ticks, then the hero, then the enemy.
                    double tDot = NextDotTime(out var dot);
                    double t = Math.Min(tDot, Math.Min(_nextPlayer, _nextEnemy));

                    if (t > _limit + TimeEpsilon)
                    {
                        Add(_events, _limit, _enemy.IsBoss ? CombatEventType.TimedOut : CombatEventType.Escaped, 0);
                        return End(_enemy.IsBoss ? FightOutcome.TimedOut : FightOutcome.Escaped, _limit);
                    }

                    if (dot != null && tDot <= _nextPlayer + TimeEpsilon && tDot <= _nextEnemy + TimeEpsilon)
                    {
                        Tick(dot, tDot);
                        if (_enemyHp <= 0) return Won(tDot);
                    }
                    else if (_nextPlayer <= _nextEnemy + TimeEpsilon)
                    {
                        t = _nextPlayer;
                        int skill = ReadySkill(t);
                        if (skill >= 0) Cast(skill, t);
                        else Deal(_e.Strike(_p, _enemy, _hitChance, DamageMult(t), _rng, _events, t, false));
                        _nextPlayer = t + PlayerInterval();
                        if (_enemyHp <= 0) return Won(t);
                    }
                    else
                    {
                        t = _nextEnemy;
                        _nextEnemy = t + _enemy.AttackInterval * (t < _slowUntil - TimeEpsilon ? 1 + _slow : 1.0);
                        if (EnemyAttack(t)) return End(FightOutcome.Died, t);
                        if (_enemyHp <= 0) return Won(t);
                    }
                }
            }

            /// <summary>Returns true when the hero died.</summary>
            private bool EnemyAttack(double t)
            {
                if (_kit.Dodge > 0 && _rng.Chance(_kit.Dodge))
                {
                    Add(_events, t, CombatEventType.Dodge, 0);
                    return false;
                }
                Add(_events, t, CombatEventType.EnemyHit, _damageTaken);
                _playerHp -= _damageTaken;
                if (_playerHp <= 0)
                {
                    bool canDie = _enemy.IsBoss && _enemy.CanKillPlayer;
                    if (canDie && _kit.CheatDeathHealPct > 0 && !_cheatUsed)
                    {
                        _cheatUsed = true; // Unbreakable: once per boss fight
                        _playerHp = 1;
                        Heal(t, _p.MaxHp * _kit.CheatDeathHealPct);
                    }
                    else if (canDie)
                    {
                        Add(_events, t, CombatEventType.PlayerDied, 0);
                        _playerHp = 0;
                        return true;
                    }
                    else _playerHp = 1; // R2-1c, ON-1
                }
                if (_kit.HealBelowPct > 0 && !_healUsed && _playerHp < _p.MaxHp * _kit.HealBelowAt)
                {
                    _healUsed = true; // Second Wind: once per fight
                    Heal(t, _p.MaxHp * _kit.HealBelowPct);
                }
                if (_p.CounterChance > 0 && _rng.Chance(_p.CounterChance))
                    Deal(_e.Strike(_p, _enemy, _hitChance, _p.CounterDamage * DamageMult(t), _rng, _events, t, true));
                return false;
            }

            private void Heal(double t, double amount)
            {
                double before = _playerHp;
                _playerHp = Math.Min(_p.MaxHp, _playerHp + amount);
                Add(_events, t, CombatEventType.Heal, _playerHp - before);
            }

            /// <summary>Applies hero damage to the enemy, with lifesteal and momentum.</summary>
            private void Deal(double damage)
            {
                if (damage <= 0) return;
                _enemyHp -= damage;
                if (_kit.Lifesteal > 0) _playerHp = Math.Min(_p.MaxHp, _playerHp + damage * _kit.Lifesteal);
                if (_kit.MomentumPerHit > 0 && _momentum < _kit.MomentumMaxStacks) _momentum++;
            }

            private double PlayerInterval()
            {
                if (_momentum == 0) return _p.AttackInterval;
                return Math.Max(_e._c.Combat.AttackIntervalFloorS, _p.AttackInterval / (1 + _momentum * _kit.MomentumPerHit));
            }

            /// <summary>Buffs and conditional passives that multiply every hero hit at time <paramref name="t"/>.</summary>
            private double DamageMult(double t)
            {
                double mult = t < _buffUntil - TimeEpsilon ? 1 + _buffDamage : 1.0;
                if (_kit.ConditionalDamage.Count == 0) return mult;
                var best = new Dictionary<string, double>();
                foreach (var b in _kit.ConditionalDamage)
                {
                    if (!Matches(b)) continue;
                    best.TryGetValue(b.NodeId, out double v);
                    best[b.NodeId] = Math.Max(v, b.Value);
                }
                double bonus = 0;
                foreach (var v in best.Values) bonus += v;
                return mult * (1 + bonus);
            }

            private bool Matches(ConditionalBonus b)
            {
                var c = b.Condition;
                if (c == "vs_boss") return _enemy.IsBoss;
                if (c == "own_hp_above") return _playerHp > _p.MaxHp * b.Threshold;
                if (c.StartsWith("vs_family:")) return _enemy.Family == c.Substring("vs_family:".Length);
                if (c.StartsWith("vs_archetype:"))
                    return string.Equals(_enemy.Archetype.ToString(), c.Substring("vs_archetype:".Length), StringComparison.OrdinalIgnoreCase);
                return false;
            }

            /// <summary>The first active in priority order that is off cooldown and whose condition holds, or −1.</summary>
            private int ReadySkill(double t)
            {
                for (int i = 0; i < _kit.Actives.Count; i++)
                    if (_readyAt[i] <= t + TimeEpsilon && ConditionHolds(_kit.Actives[i])) return i;
                return -1;
            }

            private bool ConditionHolds(ActiveSkill a)
            {
                foreach (var fx in a.Node.Effects)
                    if (fx.Kind == EffectKind.Hit && fx.Condition == "enemy_hp_below"
                        && !(_enemyHp / _enemy.Hp < fx.At("threshold", a.Rank)))
                        return false;
                return true;
            }

            private void Cast(int index, double t)
            {
                var a = _kit.Actives[index];
                _readyAt[index] = t + a.Cooldown;
                Apply(a, t);
                if (_enemyHp > 0 && _kit.EchoChance > 0 && _rng.Chance(_kit.EchoChance)) Apply(a, t); // Echo: no second cooldown
            }

            private void Apply(ActiveSkill a, double t)
            {
                foreach (var fx in a.Node.Effects)
                {
                    switch (fx.Kind)
                    {
                        case EffectKind.Hit:
                            double scale = fx.At("percent", a.Rank);
                            for (int h = 0; h < fx.Hits && _enemyHp > 0; h++) Deal(SkillStrike(a, fx, scale, t));
                            break;
                        case EffectKind.Dot:
                            StartDot(a, fx, t);
                            break;
                        case EffectKind.Buff:
                            _buffDamage = fx.At("value", a.Rank);
                            _buffUntil = t + fx.At("duration", a.Rank);
                            _events?.Add(new CombatEvent(t, CombatEventType.BuffStart, _buffDamage, Element.Physical, a.Node.Id));
                            break;
                        case EffectKind.EnemySlow:
                            _slow = fx.At("value", a.Rank);
                            _slowUntil = t + fx.At("duration", a.Rank);
                            break;
                    }
                }
            }

            private double SkillStrike(ActiveSkill a, SkillEffect fx, double scale, double t)
            {
                if (!fx.CannotMiss && !_rng.Chance(_hitChance))
                {
                    _events?.Add(new CombatEvent(t, CombatEventType.PlayerMiss, 0, Element.Physical, a.Node.Id));
                    return 0;
                }
                bool crit = fx.AlwaysCrit || _rng.Chance(_p.CritChance);
                double baseDamage = _e.Components(_p, _enemy, fx.Element, out var element);
                double critMult = crit ? _p.CritDamage + _kit.SkillCritDamage : 1.0;
                double dmg = baseDamage * _e._triangle.DealtBy(_p.Class, _enemy.Archetype) * scale * critMult * DamageMult(t);
                _events?.Add(new CombatEvent(t, crit ? CombatEventType.SkillCrit : CombatEventType.SkillHit, dmg, element, a.Node.Id));
                return dmg;
            }

            /// <summary>Total = percent × the hit at cast (no crit), split in equal ticks every dot_tick_s (spec §3.4).</summary>
            private void StartDot(ActiveSkill a, SkillEffect fx, double t)
            {
                double hit = _e.Components(_p, _enemy, fx.Element, out var element)
                             * _e._triangle.DealtBy(_p.Class, _enemy.Archetype) * DamageMult(t);
                int ticks = Math.Max(1, (int)Math.Round(fx.At("duration", a.Rank) / _tick));
                var dot = _dots.Find(d => d.SkillId == a.Node.Id);
                if (dot == null) _dots.Add(dot = new Dot { SkillId = a.Node.Id });
                dot.Element = element;
                dot.PerTick = fx.At("percent", a.Rank) * hit / ticks;
                dot.NextTick = t + _tick;
                dot.TicksLeft = ticks;
            }

            private double NextDotTime(out Dot next)
            {
                next = null;
                double best = double.PositiveInfinity;
                foreach (var d in _dots)
                    if (d.TicksLeft > 0 && d.NextTick < best)
                    {
                        best = d.NextTick;
                        next = d;
                    }
                return best;
            }

            private void Tick(Dot d, double t)
            {
                d.TicksLeft--;
                d.NextTick = t + _tick;
                _events?.Add(new CombatEvent(t, CombatEventType.DotTick, d.PerTick, d.Element, d.SkillId));
                _enemyHp -= d.PerTick;
                if (_kit.Lifesteal > 0) _playerHp = Math.Min(_p.MaxHp, _playerHp + d.PerTick * _kit.Lifesteal);
            }

            private FightResult Won(double t)
            {
                Add(_events, t, CombatEventType.EnemyDied, 0);
                return End(FightOutcome.Won, t);
            }

            private FightResult End(FightOutcome outcome, double duration)
            {
                if (_cooldownLeft != null)
                    for (int i = 0; i < _readyAt.Length; i++)
                        _cooldownLeft[i] = Math.Max(0, _readyAt[i] - duration);
                return Result(outcome, duration, _playerHp, _events);
            }
        }
    }
}
