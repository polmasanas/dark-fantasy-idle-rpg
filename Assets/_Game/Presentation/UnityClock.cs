using System;
using DarkFantasy.Core.Offline;
using UnityEngine;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Wall clock plus a since-boot clock for the clock-jump check (R2-19). On Android the boot clock is
    /// SystemClock.elapsedRealtime, which keeps counting while the phone sleeps. Elsewhere (editor,
    /// desktop) it is Environment.TickCount, which wraps every 49.7 days; a wrap looks like a reboot,
    /// so the 12 h cap still applies.
    /// </summary>
    internal sealed class UnityClock : IClock
    {
        public long UtcNowTicks => DateTime.UtcNow.Ticks;

        public double UptimeSeconds
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var clock = new AndroidJavaClass("android.os.SystemClock"))
                        return clock.CallStatic<long>("elapsedRealtime") / 1000.0;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("UnityClock: elapsedRealtime unavailable, using TickCount. " + e.Message);
                }
#endif
                return (uint)Environment.TickCount / 1000.0;
            }
        }
    }
}
