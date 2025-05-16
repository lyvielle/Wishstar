namespace Wishstar.Models {
    public class WishCategory(int categoryId, string categoryName, string categoryDescription = "") : ICloneable {
        public int CategoryId { get; set; } = categoryId;
        public string CategoryName { get; set; } = categoryName;
        public string CategoryDescription { get; set; } = categoryDescription;

        public static WishCategory CreateDefault() {
            return new WishCategory(IdGenerator.GetNumericalId(), string.Empty, string.Empty);
        }

        public static WishCategory GetUncategorized() {
            return new WishCategory(0, "Uncategorized", "No category description available.");
        }

        public object Clone() {
            return MemberwiseClone();
        }
    }
}