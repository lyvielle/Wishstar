using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Wish {
        public WishPageContext? Context { get; set; } = null;
        public WishItem WishItem { get; set; } = null!;
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        public WishDatabase Database { get; set; } = WishDatabase.Load();

        public string ImageUrl { get; set; } = string.Empty;
        public bool HasCustomImage => !string.IsNullOrWhiteSpace(ImageUrl);

        public CurrencyType Currency { get; set; } = CurrencyType.EUR;

        private double _PriceAmount = 0D;
        public double PriceAmount {
            get { return _PriceAmount; }
            set {
                _PriceAmount = value;
                OnPriceChanged(value);
            }
        }

        #region Events
        private void OnWishDescriptionChanged(ChangeEventArgs e) {
            WishItem.ItemDescription = e.Value?.ToString() ?? string.Empty;
        }

        private void OnVendorSelected(ChangeEventArgs e) {
            string? vendorName = e.Value?.ToString() ?? string.Empty;
            if (vendorName.Equals("Select Vendor", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "new") {
                NavigationManager.NavigateTo("/vendor", true);
            } else {
                WishItem.VendorId = Database.GetVendorByName(vendorName)?.VendorId ?? Models.Vendor.CreateUnspecified().VendorId;
            }
        }

        private void OnCategorySelected(ChangeEventArgs e) {
            string? categoryName = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryName) || e.Value?.ToString() == "new") {
                NavigationManager.NavigateTo("/category", true);
            } else {
                WishItem.ItemCategory = Database.GetCategoryByName(categoryName) ?? WishItem.ItemCategory;
            }
        }

        private void OnCurrencySelected(ChangeEventArgs e) {
            string? currencyName = e.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currencyName)) {
                Currency = Enum.TryParse<CurrencyType>(currencyName, out CurrencyType currency) ? currency : CurrencyType.EUR;
                if (WishItem.ItemPrice.EUR != 0) {
                    PriceAmount = WishItem.ItemPrice.GetPrice(Currency);
                    StateHasChanged();
                }
            }
        }

        private void OnPriceChanged(double price) {
            WishItem.ItemPrice = new Price(price, Currency);
        }

        #endregion

        protected string NavigateUrl = string.Empty;

        protected override void OnInitialized() {
            base.OnInitialized();

            if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) || user == null) {
                NavigateUrl = "/login";
                return;
            }

            NavigationManager.GetUriContext(out _, out WishPageContext? context, out Dictionary<string, string>?
            queryParameters);
            if (context == null) {
                WishItem = WishItem.CreateDefault(user.UserId);
                Action = PageContextAction.Add;
                return;
            }

            Context = context;
            Action = Context.Action;
            if (Context.Action == PageContextAction.Update) {
                if (queryParameters != null && queryParameters.TryGetValue("id", out string? wishId) && int.TryParse(wishId, out int numWishId)) {
                    var wish = WishDatabase.Load().GetWishById(numWishId);
                    if (wish != null) {
                        WishItem = wish;
                    } else {
                        WishItem = WishItem.CreateDefault(user.UserId);
                    }
                }
            } else if (Context.Action == PageContextAction.Add) {
                WishItem = WishItem.CreateDefault(user.UserId);
            }

            StateHasChanged();
        }

        protected override void OnAfterRender(bool firstRender) {
            if (!string.IsNullOrWhiteSpace(NavigateUrl)) {
                NavigationManager.NavigateTo(NavigateUrl, true);
            }
        }

        public void AddCategory() {
            NavigationManager.NavigateToWithContext("/category", PageContextAction.Add, CategoryPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditCategory(WishCategory category) {
            NavigationManager.NavigateToWithContext("/category", PageContextAction.Update, CategoryPageContext.FromCategory(category, GetPageContext()),
                queryParameters: new() {
                    { "id", category.CategoryName }
                });
        }

        public void AddVendor() {
            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Add, VendorPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditVendor(Models.Vendor vendor) {
            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Update, VendorPageContext.FromVendor(vendor, GetPageContext()),
                queryParameters: new() {
                    { "id", vendor.VendorName }
                });
        }

        [JSInvokable]
        public void NavigateBack() {
            string url = Context?.ParentContext?.GetFullUrl() ?? "/";
            NavigationManager.NavigateTo(url, true);
        }

        public async void ShowError(string message) {
            await JSRuntime.InvokeVoidAsync("showError", message);
        }

        public async Task SetLoading(bool isLoading) {
            await JSRuntime.InvokeVoidAsync("setLoading", isLoading);
        }

        public IPageContext GetPageContext() {
            return new WishPageContext {
                Action = Action,
                ParentContext = Context?.ParentContext,
                WishId = WishItem.WishId,
                ItemName = WishItem.ItemName,
                ItemDescription = WishItem.ItemDescription,
                VendorId = WishItem.VendorId,
                ProductLink = WishItem.ProductLink,
                ImageName = WishItem.ImageName,
                PriceInEUR = WishItem.ItemPrice.EUR,
                CategoryName = WishItem.ItemCategory.CategoryName
            };
        }
    }
}