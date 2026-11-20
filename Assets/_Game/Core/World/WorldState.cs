using System;

namespace DarkFantasy.Core.World
{
    /// <summary>Zone progress for the current run (saved, reset on prestige).</summary>
    public sealed class WorldState
    {
        public int Region = 1;

        /// <summary>Highest unlocked subzone of the current region (the frontier).</summary>
        public int UnlockedSubzone = 1;

        /// <summary>Kills per subzone of the current region; index = subzone - 1.</summary>
        public int[] Kills = new int[8];

        public bool FightBossWhenAvailable = true;
        public int HighestBossDefeated;
        public int ConsecutiveEscapes;

        /// <summary>The first-run training boss is still to be fought (ON-1); set by the session from the account.</summary>
        public bool TrainingBossPending;

        /// <summary>Game time until which the character farms one subzone below the frontier (R2-1c).</summary>
        public double RetreatUntil;

        public int KillsInSubzone(int subzone) => subzone >= 1 && subzone <= Kills.Length ? Kills[subzone - 1] : 0;

        internal void AddKill(int subzone)
        {
            if (subzone > Kills.Length) Array.Resize(ref Kills, subzone);
            Kills[subzone - 1]++;
        }
    }

    /// <summary>What the character fights next.</summary>
    public readonly struct Encounter
    {
        public readonly int Region;
        public readonly int Subzone;
        public readonly int Level;
        public readonly bool IsBoss;

        /// <summary>The first-run training boss (ON-1): boss timer, cannot kill, weak.</summary>
        public readonly bool IsTraining;

        public Encounter(int region, int subzone, int level, bool isBoss, bool isTraining = false)
        {
            Region = region;
            Subzone = subzone;
            Level = level;
            IsBoss = isBoss;
            IsTraining = isTraining;
        }

        public override string ToString() => IsTraining ? "Training boss"
            : IsBoss ? $"Region {Region} boss (lvl {Level})" : $"Subzone {Region}-{Subzone} (lvl {Level})";
    }

    public enum WorldChange
    {
        None,
        SubzoneUnlocked,
        TrainingBossDefeated,
        BossDefeated,
        BossFailed,
        Escaped,
        Retreated,
    }
}
