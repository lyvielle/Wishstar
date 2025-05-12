namespace Wishstar.Models {
    public class WishCategory(string categoryName, string categoryDescription = "") {
        public string CategoryName { get; set; } = categoryName;
        public string CategoryDescription { get; set; } = categoryDescription;

        public static WishCategory CreateDefault() {
            return new WishCategory(string.Empty, string.Empty);
        }

        public static WishCategory CreateUncategorized() {
            return new WishCategory("Uncategorized", "No category description available.");
        }
    }
}