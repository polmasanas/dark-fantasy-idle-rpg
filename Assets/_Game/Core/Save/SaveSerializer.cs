using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFantasy.Core.Save
{
    /// <summary>
    /// Save container (D-030): "DFRS" magic, 1 codec byte, then the codec's bytes. Codec 1 = gzip(JSON
    /// envelope) with a SHA-256 checksum of the payload. A binary codec can be added at release (M12)
    /// without touching migrations, which work on the format-neutral JSON payload.
    /// </summary>
    public sealed class SaveSerializer
    {
        public const int CurrentSchemaVersion = 1;
        public const byte JsonGzipCodec = 1;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DFRS");

        private readonly int _currentSchema;
        private readonly Dictionary<int, IMigration> _migrations = new Dictionary<int, IMigration>();

        public SaveSerializer() : this(CurrentSchemaVersion, Migrations.All) { }

        public SaveSerializer(int currentSchema, IEnumerable<IMigration> migrations)
        {
            _currentSchema = currentSchema;
            foreach (var m in migrations) _migrations[m.From] = m;
        }

        public byte[] Serialize(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var payload = StateMapper.ToJson(data);
            string payloadText = payload.ToString(Formatting.None);
            var envelope = new JObject
            {
                ["format"] = "DFIRPG-SAVE",
                ["schemaVersion"] = _currentSchema,
                ["gameVersion"] = data.GameVersion,
                ["savedUtcTicks"] = data.SavedUtcTicks,
                ["savedUptimeSeconds"] = data.SavedUptimeSeconds,
                ["playtimeSeconds"] = data.PlaytimeSeconds,
                ["checksum"] = Sha256(payloadText),
                ["payload"] = payload,
            };

            var ms = new MemoryStream();
            ms.Write(Magic, 0, Magic.Length);
            ms.WriteByte(JsonGzipCodec);
            using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
            {
                var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.None));
                gz.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }

        public SaveData Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Magic.Length + 1) throw new SaveFormatException(SaveError.NotASave, "Not a save file.");
            for (int i = 0; i < Magic.Length; i++)
                if (bytes[i] != Magic[i]) throw new SaveFormatException(SaveError.NotASave, "Not a save file.");
            if (bytes[Magic.Length] != JsonGzipCodec)
                throw new SaveFormatException(SaveError.UnknownCodec, "Unknown save codec " + bytes[Magic.Length] + ".");

            JObject envelope;
            try
            {
                using (var gz = new GZipStream(new MemoryStream(bytes, Magic.Length + 1, bytes.Length - Magic.Length - 1), CompressionMode.Decompress))
                using (var reader = new StreamReader(gz, Encoding.UTF8))
                    envelope = JObject.Parse(reader.ReadToEnd());
            }
            catch (Exception e) when (e is InvalidDataException || e is JsonException || e is IOException || e is EndOfStreamException)
            {
                throw new SaveFormatException(SaveError.Corrupt, "Save file is damaged: " + e.Message, e);
            }

            if ((string)envelope["format"] != "DFIRPG-SAVE" || !(envelope["payload"] is JObject payload))
                throw new SaveFormatException(SaveError.Corrupt, "Save envelope is incomplete.");
            int schema = (int?)envelope["schemaVersion"] ?? -1;
            if (schema > _currentSchema)
                throw new SaveFormatException(SaveError.NewerSchema, $"Save is from a newer version (schema {schema} > {_currentSchema}).");
            if ((string)envelope["checksum"] != Sha256(payload.ToString(Formatting.None)))
                throw new SaveFormatException(SaveError.ChecksumMismatch, "Save checksum does not match.");

            payload = Migrate(payload, schema);
            try
            {
                var data = StateMapper.FromJson(payload);
                data.GameVersion = (string)envelope["gameVersion"];
                data.SavedUtcTicks = (long)envelope["savedUtcTicks"];
                data.SavedUptimeSeconds = (double)envelope["savedUptimeSeconds"];
                data.PlaytimeSeconds = (double)envelope["playtimeSeconds"];
                return data;
            }
            catch (Exception e) when (!(e is SaveFormatException))
            {
                throw new SaveFormatException(SaveError.Corrupt, "Save data is invalid: " + e.Message, e);
            }
        }

        /// <summary>Applies migrations from <paramref name="fromSchema"/> up to the current schema, in order.</summary>
        public JObject Migrate(JObject payload, int fromSchema)
        {
            for (int v = fromSchema; v < _currentSchema; v++)
            {
                if (!_migrations.TryGetValue(v, out var m))
                    throw new SaveFormatException(SaveError.Corrupt, $"No migration from schema {v}.");
                payload = m.Apply(payload);
            }
            return payload;
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>The registry of real migrations. Schema 1 is the first; add entries as the format changes.</summary>
    public static class Migrations
    {
        public static readonly IMigration[] All = new IMigration[0];
    }
}
