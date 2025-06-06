﻿using Wishstar.Models;

namespace Wishstar.Components.Pages.Context {
    public class WishPageContext : IPageContext {
        public string Page { get; set; } = "/wish";

        [PageContextItem]
        public int WishId { get; set; }

        [PageContextItem]
        public string ItemName { get; set; } = string.Empty;

        [PageContextItem]
        public string ItemDescription { get; set; } = string.Empty;

        [PageContextItem]
        public int VendorId { get; set; } = Models.Vendor.GetUnspecified().VendorId;

        [PageContextItem]
        public string ProductLink { get; set; } = string.Empty;

        [PageContextItem]
        public string ImageName { get; set; } = string.Empty;

        [PageContextItem]
        public bool PrivateItem { get; set; } = true;

        [PageContextItem]
        public double PriceInEUR { get; set; } = 0.0D;

        [PageContextItem]
        public int CategoryId { get; set; } = WishCategory.GetUncategorized().CategoryId;

        [PageContextItem]
        public IPageContext? ParentContext { get; set; } = null;

        [PageContextItem]
        public CurrencyType DisplayCurrency { get; set; } = CurrencyType.EUR;

        [PageContextItem]
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        public string Serialize() {
            return IPageContextSerializer.Serialize(this, GetType());
        }

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

        public static WishPageContext FromWish(WishItem wish, CurrencyType displayCurrency = CurrencyType.EUR, IPageContext? parentContext = null) {
            return new WishPageContext() {
                WishId = wish.WishId,
                ItemName = wish.ItemName,
                ItemDescription = wish.ItemDescription,
                VendorId = wish.VendorId,
                ProductLink = wish.ProductLink,
                ImageName = wish.ImageName,
                PriceInEUR = wish.ItemPrice.EUR,
                CategoryId = wish.CategoryId,
                ParentContext = parentContext,
                DisplayCurrency = displayCurrency,
                Action = PageContextAction.Update
            };
        }
    }
}