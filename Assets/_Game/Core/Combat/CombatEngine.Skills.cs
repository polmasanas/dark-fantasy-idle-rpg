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
            private double _enemyHp;
            private double _playerHp;
            private double _nextPlayer;
            private double _nextEnemy;

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
                _damageTaken = engine.DamageTaken(p, enemy);
                _enemyHp = enemy.Hp;
                _playerHp = p.MaxHp;
                _nextPlayer = p.AttackInterval;
                _nextEnemy = enemy.AttackInterval;
            }

            public FightResult Run()
            {
                while (true)
                {
                    bool playerTurn = _nextPlayer <= _nextEnemy + TimeEpsilon;
                    double t = playerTurn ? _nextPlayer : _nextEnemy;

                    if (t > _limit + TimeEpsilon)
                    {
                        Add(_events, _limit, _enemy.IsBoss ? CombatEventType.TimedOut : CombatEventType.Escaped, 0);
                        return End(_enemy.IsBoss ? FightOutcome.TimedOut : FightOutcome.Escaped, _limit);
                    }

                    if (playerTurn)
                    {
                        _nextPlayer = t + _p.AttackInterval;
                        int skill = ReadySkill(t);
                        if (skill >= 0) Cast(skill, t);
                        else _enemyHp -= _e.Strike(_p, _enemy, _hitChance, 1.0, _rng, _events, t, false);
                        if (_enemyHp <= 0) return Won(t);
                    }
                    else
                    {
                        _nextEnemy = t + _enemy.AttackInterval;
                        Add(_events, t, CombatEventType.EnemyHit, _damageTaken);
                        _playerHp -= _damageTaken;
                        if (_playerHp <= 0)
                        {
                            if (_enemy.IsBoss && _enemy.CanKillPlayer)
                            {
                                Add(_events, t, CombatEventType.PlayerDied, 0);
                                _playerHp = 0;
                                return End(FightOutcome.Died, t);
                            }
                            _playerHp = 1; // R2-1c, ON-1
                        }
                        if (_p.CounterChance > 0 && _rng.Chance(_p.CounterChance))
                        {
                            _enemyHp -= _e.Strike(_p, _enemy, _hitChance, _p.CounterDamage, _rng, _events, t, true);
                            if (_enemyHp <= 0) return Won(t);
                        }
                    }
                }
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
                foreach (var fx in a.Node.Effects)
                {
                    if (fx.Kind != EffectKind.Hit) continue;
                    double scale = fx.At("percent", a.Rank);
                    for (int h = 0; h < fx.Hits && _enemyHp > 0; h++)
                        _enemyHp -= SkillStrike(a, fx, scale, t);
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
                double dmg = baseDamage * _e._triangle.DealtBy(_p.Class, _enemy.Archetype) * scale * critMult;
                _events?.Add(new CombatEvent(t, crit ? CombatEventType.SkillCrit : CombatEventType.SkillHit, dmg, element, a.Node.Id));
                return dmg;
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
