using System.Security.Cryptography;

namespace Wishstar.Extensions {
    public static class AesExtensions {
        public static byte[] EncryptCbc(this Aes aes, byte[] input) {
            return aes.EncryptCbc(input, aes.IV);
        }

        public static byte[] DecryptCbc(this Aes aes, byte[] input) {
            return aes.DecryptCbc(input, aes.IV);
        }
    }
}
