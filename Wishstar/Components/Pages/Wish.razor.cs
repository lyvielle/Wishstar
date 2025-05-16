using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Wish : IDisposable {
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

        private string _SelectedVendor = Models.Vendor.GetUnspecified().VendorName;
        public string SelectedVendor {
            get { return _SelectedVendor; }
            set {
                _SelectedVendor = value;
                OnVendorSelected(value);
            }
        }

        private string _SelectedCategory = WishCategory.GetUncategorized().CategoryName;
        public string SelectedCategory {
            get { return _SelectedCategory; }
            set {
                _SelectedCategory = value;
                OnCategorySelected(value);
            }
        }

        #region Events

        private void OnVendorSelected(string vendorName) {
            if (vendorName == "new") {
                AddVendor();
            } else if (vendorName == Models.Vendor.GetUnspecified().VendorName) {
                WishItem.VendorId = Models.Vendor.GetUnspecified().VendorId;
            } else {
                WishItem.VendorId = Database.GetVendorByName(vendorName)?.VendorId ?? Models.Vendor.GetUnspecified().VendorId;
            }
        }

        private void OnCategorySelected(string categoryName) {
            if (categoryName == "new") {
                AddCategory();
            } else if (categoryName == WishCategory.GetUncategorized().CategoryName) {
                WishItem.CategoryId = WishCategory.GetUncategorized().CategoryId;
            } else {
                WishItem.CategoryId = Database.GetCategoryByName(categoryName)?.CategoryId ?? WishCategory.GetUncategorized().CategoryId;
            }
        }

        private void OnCurrencySelected(ChangeEventArgs e) {
            string? currencyName = e.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currencyName)) {
                Currency = Enum.TryParse(currencyName, out CurrencyType currency) ? currency : CurrencyType.EUR;
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
        protected DotNetObjectReference<Wish> ObjectReference = null!;

        protected override void OnInitialized() {
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

            PriceAmount = WishItem.ItemPrice.GetPrice(Currency);

            StateHasChanged();
        }

        override protected async Task OnAfterRenderAsync(bool firstRender) {
            if (firstRender) {
                await JSRuntime.InvokeVoidAsync("registerHomeComponent", ObjectReference = DotNetObjectReference.Create(this));
                if (!string.IsNullOrWhiteSpace(WishItem.ImageName)) {
                    await JSRuntime.InvokeVoidAsync("setImage", ImageResolver.GetRelativeImageUrl(WishItem.ImageName));
                }
            }
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
        public void SetImage(string imageUrl) {
            if (imageUrl.Contains('?')) {
                imageUrl = imageUrl[(imageUrl.LastIndexOf('=') + 1)..];
            }

            WishItem.ImageName = imageUrl;
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
                CategoryId = WishItem.CategoryId
            };
        }

        public void Dispose() {
            ObjectReference?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}