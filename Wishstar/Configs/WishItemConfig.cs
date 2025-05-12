using System.Text.Json.Serialization;
using Wishstar.Models;

namespace Wishstar.Configs {
    public class WishItemConfig : IConfig {
        private static readonly ConfigLoader<WishItemConfig> _Loader = new("wishes.json", AppConfig.ConfigBasePath);
        private static readonly object _Sync = new();

        [JsonInclude]
        public List<WishItem> Wishes { get; set; } = [];

        public void Save() {
            lock(_Sync) {
                _Loader.SaveConfig();
            }
        }

        public void TryCleanConfig() { }

        public static WishItemConfig Load() {
            lock(_Sync) {
                return _Loader.LoadConfig();
            }
        }
    }
}