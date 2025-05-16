using Wishstar.Models;

namespace Wishstar.Components.Pages.Context {
    public class WishPageContext : IPageContext {
        public string Page { get; set; } = $"{AppConfig.FullCurrentDomain}/wish";

        [PageContextItem]
        public int WishId { get; set; }

        [PageContextItem]
        public string ItemName { get; set; } = string.Empty;

        [PageContextItem]
        public string ItemDescription { get; set; } = string.Empty;

        [PageContextItem]
        public int VendorId { get; set; } = Models.Vendor.CreateUnspecified().VendorId;

        [PageContextItem]
        public string ProductLink { get; set; } = string.Empty;

        [PageContextItem]
        public string ImageName { get; set; } = string.Empty;

        [PageContextItem]
        public double PriceInEUR { get; set; } = 0.0D;

        [PageContextItem]
        public string CategoryName { get; set; } = string.Empty;

        [PageContextItem]
        public IPageContext? ParentContext { get; set; } = null;

        [PageContextItem]
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        public static WishPageContext FromUri(string uri) {
            int contextIndex = uri.IndexOf("context=", StringComparison.OrdinalIgnoreCase);
            if (contextIndex == -1) {
                return new WishPageContext();
            }

            string contextContent = uri[(contextIndex + 8)..];
            return IPageContextSerializer.Deconstruct<WishPageContext>(contextContent);
        }

        public static string ToUri(WishPageContext context) {
            string contextString = IPageContextSerializer.Serialize(context);
            return $"context={contextString}";
        }

        public static WishPageContext CreateDefaultWith(IPageContext parentContext) {
            return new WishPageContext() {
                ParentContext = parentContext,
            };
        }

        public static WishPageContext FromWish(WishItem wish, IPageContext? parentContext = null) {
            return new WishPageContext() {
                WishId = wish.WishId,
                ItemName = wish.ItemName,
                ItemDescription = wish.ItemDescription,
                VendorId = wish.VendorId,
                ProductLink = wish.ProductLink,
                ImageName = wish.ImageName,
                PriceInEUR = wish.ItemPrice.EUR,
                CategoryName = wish.ItemCategory.CategoryName,
                ParentContext = parentContext,
                Action = PageContextAction.Update
            };
        }
    }
}