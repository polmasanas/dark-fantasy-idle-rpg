using System;
using DarkFantasy.Core.Offline;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using UnityEngine;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Saves to Application.persistentDataPath through the Core save store (atomic write, 3 backups).
    /// Never throws: a failed save is logged and retried at the next trigger.
    /// </summary>
    internal sealed class SaveService
    {
        private readonly SaveStore _store;
        private readonly IClock _clock;

        /// <summary>False after loading a newer-version save (it must not be overwritten) or after Delete.</summary>
        public bool Enabled { get; private set; } = true;
        public string Directory { get; }
        public long LastSavedUtcTicks { get; private set; }
        public double LastSavedUptime { get; private set; }

        public SaveService(string directory, IClock clock)
        {
            Directory = directory;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _store = new SaveStore(new DirectoryFileStore(directory));
        }

        public LoadResult Load()
        {
            try
            {
                var result = _store.Load();
                if (result.NewerVersionFound) Enabled = false;
                foreach (var name in result.Rejected) Debug.LogWarning("SaveService: damaged save file skipped: " + name);
                if (result.Data != null) Debug.Log("SaveService: loaded " + result.LoadedFrom + " from " + Directory);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveService: load failed: " + e);
                Enabled = false; // unknown state on disk: do not overwrite it
                return new LoadResult();
            }
        }

        public bool Save(GameState state, Pcg32 rng, double playtimeSeconds)
        {
            if (!Enabled) return false;
            var data = new SaveData
            {
                State = state,
                RngState = rng.State,
                RngIncrement = rng.Increment,
                SavedUtcTicks = _clock.UtcNowTicks,
                SavedUptimeSeconds = _clock.UptimeSeconds,
                PlaytimeSeconds = playtimeSeconds,
                GameVersion = Application.version,
            };
            try
            {
                _store.Save(data);
                LastSavedUtcTicks = data.SavedUtcTicks;
                LastSavedUptime = data.SavedUptimeSeconds;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveService: save failed: " + e);
                return false;
            }
        }

        /// <summary>Deletes every save file and stops saving until the next start.</summary>
        public void DeleteAll()
        {
            Enabled = false;
            try
            {
                _store.DeleteAll();
            }
            catch (Exception e)
            {
                Debug.LogError("SaveService: delete failed: " + e);
            }
        }
    }
}
