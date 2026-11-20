using System;
using DarkFantasy.Core.Session;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Save
{
    /// <summary>Everything written to disk: the game state, RNG state and timing metadata (SAVE_SYSTEM.md).</summary>
    public sealed class SaveData
    {
        public GameState State;
        public ulong RngState;
        public ulong RngIncrement;

        /// <summary>UTC ticks when saved (offline credit, M4-3).</summary>
        public long SavedUtcTicks;

        /// <summary>Device uptime in seconds when saved (clock-jump clamp, R2-19).</summary>
        public double SavedUptimeSeconds;

        public double PlaytimeSeconds;
        public string GameVersion = "0.1.0";
    }

    public enum SaveError
    {
        NotASave,
        UnknownCodec,
        Corrupt,
        ChecksumMismatch,
        NewerSchema,
    }

    public sealed class SaveFormatException : Exception
    {
        public SaveError Error { get; }

        public SaveFormatException(SaveError error, string message, Exception inner = null) : base(message, inner)
        {
            Error = error;
        }
    }

    /// <summary>Upgrades a save payload from schema <see cref="From"/> to From + 1 (pure; tested with fixtures).</summary>
    public interface IMigration
    {
        int From { get; }
        JObject Apply(JObject payload);
    }
}
