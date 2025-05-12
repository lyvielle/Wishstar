using Wishstar.Models;

namespace Wishstar.Extensions {
    public static class WishItemExtensions {
        public static User? GetUser(this WishItem wish) {
            return WishDatabase.Load().GetUserById(wish.UserId);
        }
    }
}