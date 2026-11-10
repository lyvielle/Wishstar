using System.Text.Json.Serialization;
using Wishstar.Converters;

namespace Wishstar.Models {
    public class WishItem(int wishId, string itemName, string itemDescription,
                      Price itemPrice, string productLink, bool privateItem, string imageName, int vendorId, int userId, int categoryId) : ICloneable {
        public const int MinRating = 1;
        public const int MaxRating = 5;

        public int WishId { get; set; } = wishId;

        public string ItemName { get; set; } = itemName;
        public string ItemDescription { get; set; } = itemDescription;

        public string ProductLink { get; set; } = productLink;
        public string ImageName { get; set; } = imageName;
        public bool PrivateItem { get; set; } = privateItem;

        [JsonConverter(typeof(PriceConverter))]
        public Price ItemPrice { get; set; } = itemPrice;

        public int UserId { get; set; } = userId;
        public int VendorId { get; set; } = vendorId;
        public int CategoryId { get; set; } = categoryId;

        /// <summary>
        /// How much the item is desired, 1 (nice to have) to 5 (want it most). 0 means not rated yet.
        /// </summary>
        public int Rating { get; set; } = 0;

        public static WishItem CreateDefault(int userId) {
            return new(IdGenerator.GetNumericalId(),
                itemName: string.Empty, itemDescription: string.Empty, itemPrice: Price.CreateDefault(),
                productLink: string.Empty, privateItem: true, imageName: string.Empty,
                vendorId: Vendor.GetUnspecified().VendorId,
                userId: userId,
                categoryId: WishCategory.GetUncategorized().CategoryId) {
                Rating = 3
            };
        }

        public object Clone() {
            // Price is immutable, so a shallow copy is a full copy.
            return MemberwiseClone();
        }
    }
}
