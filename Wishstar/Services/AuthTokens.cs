using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Wishstar.Models;

namespace Wishstar.Services {
    /// <summary>
    /// Creates and validates the login cookie. The payload is encrypted and authenticated with ASP.NET
    /// Data Protection (keys persisted in the data volume), so it cannot be forged or tampered with.
    /// It carries a stamp derived from the stored password hash: changing a password logs out every session.
    /// </summary>
    public static class AuthTokens {
        private static ITimeLimitedDataProtector? _Protector;

        private static ITimeLimitedDataProtector Protector =>
            _Protector ?? throw new InvalidOperationException("AuthTokens has not been initialized.");

        public static void Initialize(IDataProtectionProvider provider) {
            _Protector = provider.CreateProtector("Wishstar.Auth.v2").ToTimeLimitedDataProtector();
        }

        public static string Create(User user) {
            long issued = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Protector.Protect($"{user.UserId}|{GetStamp(user)}|{issued}", AppConfig.AuthLifetime);
        }

        public static bool TryValidate(string? token, out User? user, out DateTimeOffset issuedAt) {
            user = null;
            issuedAt = default;
            if (string.IsNullOrWhiteSpace(token)) {
                return false;
            }

            string payload;
            try {
                payload = Protector.Unprotect(token, out _);
            } catch (CryptographicException) {
                return false;
            }

            string[] parts = payload.Split('|');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int userId) || !long.TryParse(parts[2], out long issued)) {
                return false;
            }

            User? candidate = WishDatabase.Load().GetUserById(userId);
            if (candidate == null) {
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(parts[1]), Encoding.ASCII.GetBytes(GetStamp(candidate)))) {
                return false;
            }

            user = candidate;
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(issued);
            return true;
        }

        public static CookieOptions CreateCookieOptions() {
            return new CookieOptions {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.Add(AppConfig.AuthLifetime)
            };
        }

        private static string GetStamp(User user) {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.Token)))[..16];
        }
    }
}
