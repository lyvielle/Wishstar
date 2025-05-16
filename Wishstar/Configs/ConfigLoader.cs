using System.Text.Json;
using System.Text;
using Wishstar.Factories;
using System.Security.Cryptography;
using Wishstar.Extensions;

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

            if (File.Exists(ConfigPath)) {
                string cfgJson;
                if (IsEncrypted) {
                    byte[] cfgBytes = File.ReadAllBytes(ConfigPath);
                    try {
                        using Aes aes = AesFactory.Create(EncryptionKey, EncryptionIV);

                        cfgBytes = aes.DecryptCbc(cfgBytes, aes.IV);
                        cfgJson = Encoding.UTF8.GetString(cfgBytes);
                    } catch (Exception) {
                        try {
                            cfgJson = File.ReadAllText(ConfigPath);
                            JsonDocument.Parse(cfgJson);
                        } catch (Exception) {
                            return _Instance ??= new T();
                        }
                    }
                } else {
                    cfgJson = File.ReadAllText(ConfigPath);
                }

                _Instance = JsonSerializer.Deserialize<T>(cfgJson, _JsonOptions) ?? new T();
                _Instance.TryCleanConfig();

                return _Instance;
            } else {
                return _Instance ??= new T();
            }
        }

        public void SaveConfig() {
            if (_Instance == null) {
                return;
            }

            _Instance.TryCleanConfig();

            if (!Directory.Exists(ConfigBasePath)) {
                Directory.CreateDirectory(ConfigBasePath);
            }

            string cfgJson = JsonSerializer.Serialize(_Instance, _JsonOptions);

            if (IsEncrypted) {
                using Aes aes = AesFactory.Create(EncryptionKey, EncryptionIV);

                byte[] cfgBytes = Encoding.UTF8.GetBytes(cfgJson);
                cfgBytes = aes.EncryptCbc(cfgBytes);

                File.WriteAllBytes(ConfigPath, cfgBytes);
            } else {
                File.WriteAllText(ConfigPath, cfgJson);
            }
        }
    }
}
