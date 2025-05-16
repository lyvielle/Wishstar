namespace Wishstar {
    public static class AppConfig {
        public const string ConfigBasePath = "./app/content/cfg/";
        public const string ImageBasePath = "./app/content/img/";

        public static string CurrentDomain { get; set; } = "wishstar.lyvielle.io";
        public static bool UseHttps { get; set; } = false;

        public static string FullCurrentDomain {
            get {
                return UseHttps ? $"https://{CurrentDomain}" : $"http://{CurrentDomain}";
            }
        }

        public static readonly byte[] EncryptionKey = [
            0x8F, 0x2F, 0x35, 0x12, 0xD8, 0xF8, 0x12, 0x36,
            0xC2, 0x34, 0xBD, 0xA8, 0xAC, 0x71, 0xA8, 0x98,
            0x62, 0xCF, 0xEE, 0xBF, 0xB0, 0xAC, 0xD4, 0x88,
            0x4E, 0xD8, 0xA1, 0x3F, 0x59, 0xC0, 0xBE, 0xB0
        ];

        public static readonly byte[] EncryptionIV = [
            0xFB, 0x8E, 0x64, 0x94, 0x73, 0x21, 0x1A, 0x21,
            0xC5, 0xE8, 0x37, 0xEF, 0x5A, 0x93, 0x1C, 0x59
        ];
    }
}