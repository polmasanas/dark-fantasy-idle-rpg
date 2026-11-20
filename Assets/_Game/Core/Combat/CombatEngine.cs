using System;
using System.Collections.Generic;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Stats;

namespace DarkFantasy.Core.Combat
{
    /// <summary>
    /// Event-driven, deterministic fight resolution (decision D-013). Each side acts at multiples
    /// of its attack interval; on equal times the player acts first. Animations never influence
    /// results: the same inputs and RNG state always give the same <see cref="FightResult"/>.
    ///
    /// Rules: normal enemies never kill the player (HP floor 1) and escape after the escape timer
    /// (R2-1c); bosses can kill the player and time out at the boss limit (R2-2). Each enemy hit
    /// may trigger a Counter (D-033): an immediate extra attack at CounterDamage x hit damage.
    /// </summary>
    public sealed class CombatEngine
    {
        private const double TimeEpsilon = 1e-9;

        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;
        private readonly ElementTable _elements;
        private readonly TriangleTable _triangle;

        public CombatEngine(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
            _elements = new ElementTable(config);
            _triangle = new TriangleTable(config);
        }

        public FightResult Resolve(CombatStats player, EnemySnapshot enemy, IRng rng, bool recordEvents)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!(player.AttackInterval > 0) || !(enemy.AttackInterval > 0))
                throw new ArgumentException("Attack intervals must be positive.");

            var events = recordEvents ? new List<CombatEvent>() : null;
            double limit = enemy.IsBoss ? _c.Enemy.BossTimeLimitS : _c.Combat.EscapeTimerS;
            double hitChance = HitChance(player, enemy);
            double damageTaken = DamageTaken(player, enemy);

            double enemyHp = enemy.Hp;
            double playerHp = player.MaxHp;
            int playerSwings = 1, enemySwings = 1;

            while (true)
            {
                // Times are computed as n x interval (not accumulated) so long fights do not drift.
                double tPlayer = playerSwings * player.AttackInterval;
                double tEnemy = enemySwings * enemy.AttackInterval;
                bool playerTurn = tPlayer <= tEnemy + TimeEpsilon;
                double t = playerTurn ? tPlayer : tEnemy;

                if (t > limit + TimeEpsilon)
                {
                    var outcome = enemy.IsBoss ? FightOutcome.TimedOut : FightOutcome.Escaped;
                    Add(events, limit, enemy.IsBoss ? CombatEventType.TimedOut : CombatEventType.Escaped, 0);
                    return Result(outcome, limit, playerHp, events);
                }

                if (playerTurn)
                {
                    playerSwings++;
                    enemyHp -= Strike(player, enemy, hitChance, 1.0, rng, events, t, false);
                    if (enemyHp <= 0)
                    {
                        Add(events, t, CombatEventType.EnemyDied, 0);
                        return Result(FightOutcome.Won, t, playerHp, events);
                    }
                }
                else
                {
                    enemySwings++;
                    Add(events, t, CombatEventType.EnemyHit, damageTaken);
                    playerHp -= damageTaken;
                    if (playerHp <= 0)
                    {
                        if (enemy.IsBoss && enemy.CanKillPlayer)
                        {
                            Add(events, t, CombatEventType.PlayerDied, 0);
                            return Result(FightOutcome.Died, t, 0, events);
                        }
                        playerHp = 1; // normal enemies and the training boss can never kill (R2-1c, ON-1)
                    }

                    if (player.CounterChance > 0 && rng.Chance(player.CounterChance))
                    {
                        enemyHp -= Strike(player, enemy, hitChance, player.CounterDamage, rng, events, t, true);
                        if (enemyHp <= 0)
                        {
                            Add(events, t, CombatEventType.EnemyDied, 0);
                            return Result(FightOutcome.Won, t, playerHp, events);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Hit chance = clamp(base x Acc / (Acc + Eva / 4), min, max) (GDD section 4.5, R2-21).
        /// </summary>
        public double HitChance(CombatStats player, EnemySnapshot enemy)
        {
            double acc = Math.Max(0, player.Accuracy);
            double eva = Math.Max(0, enemy.Evasion);
            double ratio = acc + eva / 4 > 0 ? acc / (acc + eva / 4) : 0;
            return Clamp(player.BaseHitChance * ratio, _c.Combat.HitChanceMin, _c.Combat.HitChanceMax);
        }

        /// <summary>DR = min(cap, Armour / (Armour + K(enemy level))) (GAME_BALANCE section 4.2).</summary>
        public double DamageReduction(double armour, int enemyLevel)
        {
            if (armour <= 0) return 0;
            return Math.Min(_c.Player.DamageReductionCap, armour / (armour + _f.ArmourK(enemyLevel)));
        }

        /// <summary>Damage one enemy hit does to the hero: attack × (1 − DR) × triangle (D-017).</summary>
        public double DamageTaken(CombatStats player, EnemySnapshot enemy) =>
            enemy.Attack * (1 - DamageReduction(player.Armour, enemy.Level)) * _triangle.TakenBy(player.Class, enemy.Archetype);

        /// <summary>
        /// One hero hit (GDD 4.2, R2-21): a Physical component plus one per element the hero adds; each is
        /// multiplied by the element matrix and the target's clamped resistance, then the sum by the triangle,
        /// the damage scale (1, or the counter share) and the crit multiplier.
        /// </summary>
        public double HitAmount(CombatStats p, EnemySnapshot enemy, double damageScale, bool crit) =>
            Components(p, enemy, out _) * _triangle.DealtBy(p.Class, enemy.Archetype) * damageScale * (crit ? p.CritDamage : 1.0);

        private double Components(CombatStats p, EnemySnapshot enemy, out Element largest)
        {
            double total = 0, best = -1;
            largest = Element.Physical;
            for (int i = 0; i < Elements.Count; i++)
            {
                double share = i == 0 ? 1.0 : p.ElementShare[i] * (1 + p.PctElemental);
                if (share <= 0) continue;
                double resist = enemy.Resist[i];
                resist = resist < _c.Elements.ResistMin ? _c.Elements.ResistMin : resist > _c.Elements.ResistMax ? _c.Elements.ResistMax : resist;
                double part = p.HitDamage * share * _elements.Multiplier((Element)i, enemy.Element) * (1 - resist);
                if (part > best)
                {
                    best = part;
                    largest = (Element)i;
                }
                total += part;
            }
            return total;
        }

        private double Strike(CombatStats p, EnemySnapshot enemy, double hitChance, double damageScale, IRng rng,
            List<CombatEvent> events, double t, bool isCounter)
        {
            if (!rng.Chance(hitChance))
            {
                Add(events, t, CombatEventType.PlayerMiss, 0);
                return 0;
            }
            bool crit = rng.Chance(p.CritChance);
            double baseDamage = Components(p, enemy, out var element);
            double dmg = baseDamage * _triangle.DealtBy(p.Class, enemy.Archetype) * damageScale * (crit ? p.CritDamage : 1.0);
            events?.Add(new CombatEvent(t, isCounter ? CombatEventType.Counter : crit ? CombatEventType.PlayerCrit : CombatEventType.PlayerHit, dmg, element));
            return dmg;
        }

        private static void Add(List<CombatEvent> events, double t, CombatEventType type, double amount)
        {
            events?.Add(new CombatEvent(t, type, amount));
        }

        private static FightResult Result(FightOutcome outcome, double duration, double hpLeft, List<CombatEvent> events) =>
            new FightResult
            {
                Outcome = outcome,
                Duration = duration,
                PlayerHpLeft = hpLeft,
                Events = (IReadOnlyList<CombatEvent>)events ?? Array.Empty<CombatEvent>(),
            };

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
