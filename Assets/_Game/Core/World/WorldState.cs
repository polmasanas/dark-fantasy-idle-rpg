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

        /// <summary>Game time before which a failed boss is not retried (R2-2).</summary>
        public double BossRetryAt;

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

        public Encounter(int region, int subzone, int level, bool isBoss)
        {
            Region = region;
            Subzone = subzone;
            Level = level;
            IsBoss = isBoss;
        }

        public override string ToString() => IsBoss ? $"Region {Region} boss (lvl {Level})" : $"Subzone {Region}-{Subzone} (lvl {Level})";
    }

    public enum WorldChange
    {
        None,
        SubzoneUnlocked,
        BossDefeated,
        BossFailed,
        Escaped,
        Retreated,
    }
}
