namespace Wishstar.Models {
    public class WishItem(int wishId, string itemName, string itemDescription, int vendorId,
                      string productLink, string imageName, int userId,
                      Price itemPrice, WishCategory itemCategory) {
        public int WishId { get; set; } = wishId;
        public string ItemName { get; set; } = itemName;
        public string ItemDescription { get; set; } = itemDescription;
        public int VendorId { get; set; } = vendorId;
        public string ProductLink { get; set; } = productLink;
        public string ImageName { get; set; } = imageName;
        public int UserId { get; set; } = userId;
        public Price ItemPrice { get; set; } = itemPrice;
        public WishCategory ItemCategory { get; set; } = itemCategory;

        public static WishItem CreateDefault(int userId) {
            return new(IdGenerator.GetNumericalId(), string.Empty, string.Empty, Vendor.CreateUnspecified().VendorId, string.Empty, string.Empty, userId, Price.CreateDefault(), WishCategory.CreateUncategorized());
        }
    }

}