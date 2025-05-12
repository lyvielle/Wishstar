using Wishstar.Models;

namespace Wishstar.Factories {
    public static class UserFactory {
        public static User Create(string userName, string email, string passwordHash) {
            int userId = Random.Shared.Next(0, int.MaxValue);
            return new User(userId, userName, email, passwordHash, CurrencyType.EUR);
        }
    }
}
