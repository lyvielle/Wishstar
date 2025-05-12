using System.Text.Json.Serialization;
using Wishstar.Models;

namespace Wishstar.Configs {
    public class UserConfig : IConfig {
        private static readonly ConfigLoader<UserConfig> _Loader = new("users.json", AppConfig.ConfigBasePath);
        private static readonly object _Sync = new();

        [JsonInclude]
        public List<User> Users { get; set; } = [];

        public void Save() {
            lock(_Sync) {
                _Loader.SaveConfig();
            }
        }

        public void TryCleanConfig() { }

        public static UserConfig Load() {
            lock(_Sync) {
                return _Loader.LoadConfig();
            }
        }
    }
}
