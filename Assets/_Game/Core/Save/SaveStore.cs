using System;
using System.Collections.Generic;

namespace DarkFantasy.Core.Save
{
    public sealed class LoadResult
    {
        /// <summary>The loaded save, or null on first launch or when nothing is readable.</summary>
        public SaveData Data;
        public string LoadedFrom;

        /// <summary>Files that exist but could not be read (damaged), newest first.</summary>
        public List<string> Rejected = new List<string>();

        /// <summary>Set when the newest save comes from a newer game version: it is left untouched.</summary>
        public bool NewerVersionFound;
    }

    /// <summary>
    /// Atomic saving with three rolling backups (SAVE_SYSTEM.md section 3): write a temp file, rotate
    /// main → bak1 → bak2 → bak3, rename the temp file in. Loading falls back to the newest readable file.
    /// </summary>
    public sealed class SaveStore
    {
        public const string MainFile = "save.dfrs";
        public const string TempFile = "save.tmp";
        private static readonly string[] Backups = { "save.bak1", "save.bak2", "save.bak3" };

        private readonly IFileStore _fs;
        private readonly SaveSerializer _serializer;

        public SaveStore(IFileStore fs, SaveSerializer serializer = null)
        {
            _fs = fs ?? throw new ArgumentNullException(nameof(fs));
            _serializer = serializer ?? new SaveSerializer();
        }

        public void Save(SaveData data)
        {
            _fs.Write(TempFile, _serializer.Serialize(data));
            _fs.Delete(Backups[Backups.Length - 1]);
            for (int i = Backups.Length - 1; i > 0; i--)
                if (_fs.Exists(Backups[i - 1])) _fs.Replace(Backups[i - 1], Backups[i]);
            if (_fs.Exists(MainFile)) _fs.Replace(MainFile, Backups[0]);
            _fs.Replace(TempFile, MainFile);
        }

        public LoadResult Load()
        {
            var result = new LoadResult();
            var candidates = new List<string> { MainFile };
            candidates.AddRange(Backups);
            foreach (var name in candidates)
            {
                if (!_fs.Exists(name)) continue;
                try
                {
                    result.Data = _serializer.Deserialize(_fs.Read(name));
                    result.LoadedFrom = name;
                    return result;
                }
                catch (SaveFormatException e) when (e.Error == SaveError.NewerSchema)
                {
                    result.NewerVersionFound = true; // never fall back past a newer save: it would lose progress
                    return result;
                }
                catch (SaveFormatException)
                {
                    result.Rejected.Add(name);
                }
            }
            return result;
        }

        public void DeleteAll()
        {
            _fs.Delete(MainFile);
            _fs.Delete(TempFile);
            foreach (var b in Backups) _fs.Delete(b);
        }
    }
}
