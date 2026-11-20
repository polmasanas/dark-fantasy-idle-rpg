using System;
using DarkFantasy.Core.Config;

namespace DarkFantasy.Core.Offline
{
    /// <summary>Wall clock and device uptime, supplied by the platform layer (Game.Core never reads time itself).</summary>
    public interface IClock
    {
        long UtcNowTicks { get; }

        /// <summary>Seconds since the device booted; resets on reboot.</summary>
        double UptimeSeconds { get; }
    }

    /// <summary>Why the counted time differs from the raw wall-clock difference.</summary>
    public enum OfflineAdjustment
    {
        None,
        ClockRolledBack,
        ClampedToUptime,
        Capped,
    }

    public readonly struct OfflineCredit
    {
        /// <summary>Time away that counts (after rollback/uptime checks and the cap).</summary>
        public readonly double CountedSeconds;

        /// <summary>Game time to simulate: counted × efficiency.</summary>
        public readonly double CreditedSeconds;

        public readonly bool ShowReport;
        public readonly OfflineAdjustment Adjustment;

        public OfflineCredit(double counted, double credited, bool showReport, OfflineAdjustment adjustment)
        {
            CountedSeconds = counted;
            CreditedSeconds = credited;
            ShowReport = showReport;
            Adjustment = adjustment;
        }
    }

    /// <summary>
    /// Turns "saved at / now" into offline credit (M4-1, R2-19): 50 % efficiency, 12 h cap,
    /// no credit for a clock moved backwards, and in the same boot no more than the uptime that passed.
    /// </summary>
    public sealed class OfflineCalculator
    {
        private const double TicksPerSecond = 10_000_000.0;
        private readonly BalanceConfig _c;

        public OfflineCalculator(BalanceConfig config)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
        }

        public OfflineCredit Compute(long savedUtcTicks, double savedUptime, long nowUtcTicks, double nowUptime)
        {
            double elapsed = ((double)nowUtcTicks - savedUtcTicks) / TicksPerSecond;
            if (elapsed < 0) return new OfflineCredit(0, 0, false, OfflineAdjustment.ClockRolledBack);

            var adjustment = OfflineAdjustment.None;
            bool sameBoot = nowUptime >= savedUptime;
            if (sameBoot)
            {
                double uptimeDelta = nowUptime - savedUptime;
                if (elapsed > uptimeDelta + _c.OfflineRules.ClockToleranceS)
                {
                    elapsed = uptimeDelta;
                    adjustment = OfflineAdjustment.ClampedToUptime;
                }
            }

            double cap = _c.Offline.CapHours * 3600;
            if (elapsed > cap)
            {
                elapsed = cap;
                adjustment = OfflineAdjustment.Capped;
            }

            return new OfflineCredit(elapsed, elapsed * _c.Offline.Efficiency,
                elapsed >= _c.OfflineRules.ReportMinSeconds, adjustment);
        }
    }

    /// <summary>What happened while the player was away (shown in the report panel).</summary>
    public sealed class OfflineReport
    {
        public double CreditedSeconds;
        public int Kills;
        public int BossesDefeated;
        public int BossesFailed;
        public int LevelsGained;
        public double Gold;
        public double Materials;
        public int HeldItems;
        public int AutoSalvaged;
    }

    /// <summary>Result of moving held offline loot into the bag.</summary>
    public readonly struct CollectReport
    {
        public readonly int Kept;
        public readonly int SalvagedBagFull;
        public readonly double Materials;

        public CollectReport(int kept, int salvagedBagFull, double materials)
        {
            Kept = kept;
            SalvagedBagFull = salvagedBagFull;
            Materials = materials;
        }
    }
}
