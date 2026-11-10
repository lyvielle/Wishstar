using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wishstar.Extensions;
using Wishstar.Factories;

namespace Wishstar.Configs {
    public interface IConfig {
        public void TryCleanConfig();
        public void Save();
    }

    public class ConfigLoader<T>(string configName, string configBasePath) where T : IConfig, new() {
        private static T? _Instance = default;
        private static readonly JsonSerializerOptions _JsonOptions = new() {
            WriteIndented = true
        };

        public bool IsEncrypted { get; set; } = false;
        public string ConfigName { get; } = configName;
        public string ConfigBasePath { get; } = configBasePath;

        public string ConfigPath {
            get {
                return Path.Combine(ConfigBasePath, ConfigName);
            }
        }

        private byte[] _EncryptionKey = [];
        public byte[] EncryptionKey {
            get {
                if (_EncryptionKey.Length != 32) {
                    throw new InvalidOperationException("Key length must be 32 bytes");
                }

                return _EncryptionKey;
            }

            set {
                if (value.Length != 32) {
                    throw new InvalidOperationException("Key length must be 32 bytes");
                }

                _EncryptionKey = value;
            }
        }

        private byte[] _EncryptionIV = [];
        public byte[] EncryptionIV {
            get {
                if (_EncryptionIV.Length != 16) {
                    throw new InvalidOperationException("IV length must be 16 bytes");
                }

                return _EncryptionIV;
            }

            set {
                if (value.Length != 16) {
                    throw new InvalidOperationException("IV length must be 16 bytes");
                }

                _EncryptionIV = value;
            }
        }

        public T LoadConfig() {
            if (_Instance != null) {
                return _Instance;
            }

            if (!Directory.Exists(ConfigBasePath)) {
                Directory.CreateDirectory(ConfigBasePath);
            }

            if (!File.Exists(ConfigPath)) {
                return _Instance ??= new T();
            }

            string cfgJson;
            if (IsEncrypted) {
                try {
                    using Aes aes = AesFactory.Create(EncryptionKey, EncryptionIV);
                    cfgJson = Encoding.UTF8.GetString(aes.DecryptCbc(File.ReadAllBytes(ConfigPath), aes.IV));
                } catch (CryptographicException) {
                    // Fall back to a plain-text config (e.g. encryption was enabled after the fact).
                    cfgJson = File.ReadAllText(ConfigPath);
                }
            } else {
                cfgJson = File.ReadAllText(ConfigPath);
            }

            try {
                _Instance = JsonSerializer.Deserialize<T>(cfgJson, _JsonOptions) ?? new T();
            } catch (JsonException ex) {
                // Never continue with an empty config here: the next save would overwrite (and lose) the
                // real data. Keep a copy of the unreadable file and refuse to start instead.
                string backupPath = $"{ConfigPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(ConfigPath, backupPath, overwrite: false);
                throw new InvalidDataException($"Config '{ConfigPath}' could not be parsed. A copy was saved to '{backupPath}'.", ex);
            }

            _Instance.TryCleanConfig();
            return _Instance;
        }

        public void SaveConfig() {
            if (_Instance == null) {
                return;
            }

            _Instance.TryCleanConfig();

            if (!Directory.Exists(ConfigBasePath)) {
                Directory.CreateDirectory(ConfigBasePath);
            }

            byte[] cfgBytes = JsonSerializer.SerializeToUtf8Bytes(_Instance, _JsonOptions);
            if (IsEncrypted) {
                using Aes aes = AesFactory.Create(EncryptionKey, EncryptionIV);
                cfgBytes = aes.EncryptCbc(cfgBytes);
            }

            // Write to a temporary file first and swap it in, so a crash or full disk mid-write
            // can never leave a truncated config behind.
            string tempPath = ConfigPath + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(cfgBytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, ConfigPath, overwrite: true);
        }
    }
}
