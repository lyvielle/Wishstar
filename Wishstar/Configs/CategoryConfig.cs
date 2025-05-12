using System.Text.Json.Serialization;
using Wishstar.Models;

namespace Wishstar.Configs {
    public class CategoryConfig : IConfig {
        private static readonly ConfigLoader<CategoryConfig> _Loader = new("category.json", AppConfig.ConfigBasePath);
        private static readonly object _Sync = new();

        [JsonInclude]
        public List<WishCategory> Categories { get; set; } = [];

        public void Save() {
            lock(_Sync) {
                _Loader.SaveConfig();
            }
        }

        public void TryCleanConfig() { }

        public static CategoryConfig Load() {
            lock(_Sync) {
                return _Loader.LoadConfig();
            }
        }
    }
}
