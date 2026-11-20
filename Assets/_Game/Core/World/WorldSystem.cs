using System;
using DarkFantasy.Core.Combat;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Formulas;

namespace DarkFantasy.Core.World
{
    /// <summary>
    /// Subzone and boss progression (GDD section 8, R2-1c, R2-2: a failed boss returns after a new wave of kills). <see cref="NextEncounter"/> is
    /// pure; only <see cref="OnFightFinished"/> changes the <see cref="WorldState"/>.
    /// </summary>
    public sealed class WorldSystem
    {
        private readonly BalanceConfig _c;
        private readonly GameFormulas _f;

        public WorldSystem(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _f = new GameFormulas(config);
        }

        public int KillsToAdvance(WorldState w) => _f.KillsToAdvance(w.Region);

        public bool IsRetreating(WorldState w, double now) => now < w.RetreatUntil;

        public bool IsBossAvailable(WorldState w) =>
            w.UnlockedSubzone == _c.World.SubzonesPerRegion
            && w.KillsInSubzone(w.UnlockedSubzone) >= KillsToAdvance(w);

        /// <summary>The first-run training boss waits at the end of its subzone's wave (ON-1).</summary>
        private bool IsTrainingGate(WorldState w) =>
            w.TrainingBossPending && _c.Onboarding.TrainingBossEnabled
            && w.Region == 1 && w.UnlockedSubzone == _c.Onboarding.TrainingBossAfterSubzone;

        public bool IsTrainingBossReady(WorldState w) =>
            IsTrainingGate(w) && w.KillsInSubzone(w.UnlockedSubzone) >= KillsToAdvance(w);

        public Encounter NextEncounter(WorldState w, double now)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            bool retreating = IsRetreating(w, now);
            if (!retreating && IsTrainingBossReady(w))
            {
                int trainingSub = w.UnlockedSubzone;
                return new Encounter(w.Region, trainingSub, _f.SubzoneLevel(w.Region, trainingSub), true, true);
            }
            if (!retreating && w.FightBossWhenAvailable && IsBossAvailable(w))
                return new Encounter(w.Region, w.UnlockedSubzone, _f.BossLevel(w.Region), true);

            int sub = retreating ? Math.Max(1, w.UnlockedSubzone - 1) : w.UnlockedSubzone;
            return new Encounter(w.Region, sub, _f.SubzoneLevel(w.Region, sub), false);
        }

        public WorldChange OnFightFinished(WorldState w, Encounter e, FightOutcome outcome, double now)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            if (e.IsTraining)
            {
                if (outcome == FightOutcome.Won)
                {
                    w.TrainingBossPending = false;
                    if (w.UnlockedSubzone < _c.World.SubzonesPerRegion) w.UnlockedSubzone++;
                    return WorldChange.TrainingBossDefeated;
                }
                w.Kills[e.Subzone - 1] = 0; // like a real boss: clear a new wave first (R2-2)
                return WorldChange.BossFailed;
            }
            if (e.IsBoss)
            {
                if (outcome == FightOutcome.Won)
                {
                    w.HighestBossDefeated = Math.Max(w.HighestBossDefeated, e.Region);
                    w.Region = e.Region + 1;
                    w.UnlockedSubzone = 1;
                    w.Kills = new int[w.Kills.Length];
                    w.ConsecutiveEscapes = 0;
                    w.RetreatUntil = 0;
                    return WorldChange.BossDefeated;
                }
                // R2-2 (owner change): the boss returns after clearing a new wave in the last subzone.
                int k = KillsToAdvance(w);
                int needed = (int)Math.Ceiling(k * _c.Combat.BossRetryKillFraction);
                w.Kills[e.Subzone - 1] = Math.Max(0, k - needed);
                return WorldChange.BossFailed;
            }

            if (outcome == FightOutcome.Won)
            {
                w.ConsecutiveEscapes = 0;
                w.AddKill(e.Subzone);
                if (e.Subzone == w.UnlockedSubzone
                    && w.UnlockedSubzone < _c.World.SubzonesPerRegion
                    && w.KillsInSubzone(e.Subzone) >= KillsToAdvance(w)
                    && !IsTrainingGate(w))
                {
                    w.UnlockedSubzone++;
                    return WorldChange.SubzoneUnlocked;
                }
                return WorldChange.None;
            }

            // Escaped (normal enemies cannot kill the player, R2-1c)
            w.ConsecutiveEscapes++;
            if (w.ConsecutiveEscapes >= _c.Combat.EscapesBeforeRetreat)
            {
                w.ConsecutiveEscapes = 0;
                w.RetreatUntil = now + _c.Combat.RetreatRetryS;
                return WorldChange.Retreated;
            }
            return WorldChange.Escaped;
        }
    }
}
