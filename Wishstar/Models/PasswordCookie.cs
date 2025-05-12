using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wishstar.Extensions;
using Wishstar.Factories;

namespace Wishstar.Models {
    public static class PasswordCookie {
        private record class PasswordCookieContent(int UserId, string UserAgent, string PasswordHash);

        public static string CreateCookie(int userId, string userAgent, string passwordHash) {
            PasswordCookieContent content = new(userId, userAgent, passwordHash);
            byte[] cookieBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content));
            
            using Aes aes = AesFactory.CreateRegularAes();
            cookieBytes = aes.EncryptCbc(cookieBytes);
            
            return cookieBytes.ByteArrayToString();            
        }

        public static bool Validate(string cookieContent, string userId, string userAgent, out User? user) {
            user = null;

            if(string.IsNullOrWhiteSpace(cookieContent)) {
                return false;
            }

            try {
                byte[] cookieBytes = Encoding.UTF8.GetBytes(cookieContent);

                using Aes aes = AesFactory.CreateRegularAes();
                cookieBytes = aes.DecryptCbc(cookieBytes);

                string json = Encoding.UTF8.GetString(cookieBytes);
                PasswordCookieContent? content = JsonSerializer.Deserialize<PasswordCookieContent>(json);
                if(content == null) {
                    return false;
                }

                if(content.UserAgent != userAgent) {
                    return false;
                }

                if(!int.TryParse(userId, out int numericalId) || numericalId != content.UserId) {
                    return false;
                }

                user = WishDatabase.Load().GetUserById(content.UserId);
                return user != null;
            } catch(Exception) {
                return false;
            }
        }
    }
}