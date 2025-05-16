namespace Wishstar.Models {
    public class User(int userId, string username, string email, string token, CurrencyType preferredCurrency) {
        public int UserId { get; set; } = userId;
        public string Username { get; set; } = username;
        public string Email { get; set; } = email;
        public string Token { get; set; } = token;
        public CurrencyType PreferredCurrency { get; set; } = preferredCurrency;

        public static User CreateDefault() {
            return new User(IdGenerator.GetNumericalId(), string.Empty, string.Empty, string.Empty, CurrencyType.EUR);
        }
    }
}