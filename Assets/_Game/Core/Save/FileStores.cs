using System.Collections.Generic;
using System.IO;

namespace DarkFantasy.Core.Save
{
    /// <summary>The few file operations the save store needs, so Core stays testable and engine-free.</summary>
    public interface IFileStore
    {
        bool Exists(string name);
        byte[] Read(string name);
        void Write(string name, byte[] data);

        /// <summary>Moves <paramref name="source"/> to <paramref name="destination"/>, replacing it if present.</summary>
        void Replace(string source, string destination);

        void Delete(string name);
    }

    /// <summary>In-memory store for tests.</summary>
    public sealed class MemoryFileStore : IFileStore
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();

        public bool Exists(string name) => _files.ContainsKey(name);
        public byte[] Read(string name) => _files[name];
        public void Write(string name, byte[] data) => _files[name] = (byte[])data.Clone();

        public void Replace(string source, string destination)
        {
            _files[destination] = _files[source];
            _files.Remove(source);
        }

        public void Delete(string name) => _files.Remove(name);
    }

    /// <summary>Files in one directory (Unity passes Application.persistentDataPath).</summary>
    public sealed class DirectoryFileStore : IFileStore
    {
        private readonly string _dir;

        public DirectoryFileStore(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        private string P(string name) => Path.Combine(_dir, name);

        public bool Exists(string name) => File.Exists(P(name));
        public byte[] Read(string name) => File.ReadAllBytes(P(name));

        public void Write(string name, byte[] data)
        {
            using (var fs = new FileStream(P(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true); // reach the disk before the rename (SAVE_SYSTEM.md section 3)
            }
        }

        public void Replace(string source, string destination)
        {
            if (File.Exists(P(destination))) File.Replace(P(source), P(destination), null);
            else File.Move(P(source), P(destination));
        }

        public void Delete(string name)
        {
            if (File.Exists(P(name))) File.Delete(P(name));
        }
    }
}
