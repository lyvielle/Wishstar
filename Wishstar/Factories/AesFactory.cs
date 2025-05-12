using System.Security.Cryptography;

namespace Wishstar.Factories {
    public static class AesFactory {
        public static Aes Create(byte[] key, byte[] iv) {
            Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            return aes;
        }

        public static Aes CreateRegularAes() {
            return Create(AppConfig.EncryptionKey, AppConfig.EncryptionIV);
        }
    }
}
