using System.Security.Cryptography;
using System.Text;

namespace Wishstar.Services {
    /// <summary>
    /// Salted PBKDF2 password hashes. Legacy unsalted SHA-256 hex hashes are still accepted so
    /// existing accounts keep working; they are upgraded to PBKDF2 on the next successful login.
    /// </summary>
    public static class PasswordHasher {
        private const string Prefix = "pbkdf2-sha256";
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password) {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string storedHash, out bool needsRehash) {
            needsRehash = false;
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) {
                return false;
            }

            if (storedHash.StartsWith(Prefix + "$", StringComparison.Ordinal)) {
                string[] parts = storedHash.Split('$');
                if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations <= 0) {
                    return false;
                }

                try {
                    byte[] salt = Convert.FromBase64String(parts[2]);
                    byte[] expected = Convert.FromBase64String(parts[3]);
                    byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                    bool valid = CryptographicOperations.FixedTimeEquals(actual, expected);
                    needsRehash = valid && iterations < Iterations;
                    return valid;
                } catch (FormatException) {
                    return false;
                }
            }

            // Legacy format: lowercase hex SHA-256 of the password, no salt.
            if (storedHash.Length == 64 && storedHash.All(Uri.IsHexDigit)) {
                byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                byte[] expected = Convert.FromHexString(storedHash);
                bool valid = CryptographicOperations.FixedTimeEquals(actual, expected);
                needsRehash = valid;
                return valid;
            }

            return false;
        }

        /// <summary>Burns roughly the same CPU time as a real verification (prevents user enumeration by timing).</summary>
        public static void SimulateVerify() {
            Rfc2898DeriveBytes.Pbkdf2("dummy"u8, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
        }
    }
}
