namespace Wishstar {
    public static class AppConfig {
        public const string ConfigBasePath = "./app/content/cfg/";
        public const string ImageBasePath = "./app/content/img/";
        public const string KeysBasePath = "./app/content/keys/";
        public const string WellKnownBasePath = "./wwwroot/.well-known/";

        public const string AuthCookieName = "wishstar_auth";
        public static readonly TimeSpan AuthLifetime = TimeSpan.FromDays(30);

        public static string CurrentDomain { get; set; } = "localhost";
        public static bool UseHttps { get; set; } = false;

        public static string FullCurrentDomain {
            get {
                return UseHttps ? $"https://{CurrentDomain}" : $"http://{CurrentDomain}";
            }
        }
    }
}
