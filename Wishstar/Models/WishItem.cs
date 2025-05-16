using System.Text.Json.Serialization;
using Wishstar.Converters;

namespace Wishstar.Models {
    public class WishItem(int wishId, string itemName, string itemDescription,
                      Price itemPrice, string productLink, bool privateItem, string imageName, int vendorId, int userId, int categoryId) {
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

        public static WishItem CreateDefault(int userId) {
            return new(IdGenerator.GetNumericalId(),
                itemName: string.Empty, itemDescription: string.Empty, itemPrice: Price.CreateDefault(),
                productLink: string.Empty, privateItem: true, imageName: string.Empty,
                vendorId: Vendor.GetUnspecified().VendorId,
                userId: 0,
                categoryId: 0);
        }
    }

}