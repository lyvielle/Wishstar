using System.Text.Json.Serialization;
using Wishstar.Models;

namespace Wishstar.Configs {
    public class VendorConfig : IConfig {
        private static readonly ConfigLoader<VendorConfig> _Loader = new("vendors.json", AppConfig.ConfigBasePath);
        private static readonly object _Sync = new();

        [JsonInclude]
        public List<Vendor> Vendors { get; set; } = [];

        public void Save() {
            lock(_Sync) {
                _Loader.SaveConfig();
            }
        }

        public void TryCleanConfig() { }

        public static VendorConfig Load() {
            lock(_Sync) {
                return _Loader.LoadConfig();
            }
        }
    }
}