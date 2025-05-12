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

        private async Task OnImageSelected(InputFileChangeEventArgs e) {
            if (e.FileCount == 0) {
                return;
            }

            var file = e.File;
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(maxAllowedSize: 120 * 1024 * 1024); // 120MB limit
            content.Add(new StreamContent(stream), "file", file.Name);

            var response = await HttpClient.PostAsync("api/icon/upload", content);
            if (response.IsSuccessStatusCode) {
                var imageName = await response.Content.ReadAsStringAsync();
                ImageUrl = ImageResolver.GetImageUrl(imageName);
                StateHasChanged();
            } else {
                ShowError("Failed to upload image.");
            }
        }

        private void OnWishDescriptionChanged(ChangeEventArgs e) {
            WishItem.ItemDescription = e.Value?.ToString() ?? string.Empty;
        }

        private void OnVendorSelected(ChangeEventArgs e) {
            string? vendorName = e.Value?.ToString() ?? string.Empty;
            if (vendorName.Equals("Select Vendor", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "new") {
                NavigationManager.NavigateTo("/vendor");
            } else {
                WishItem.VendorId = Database.GetVendorByName(vendorName)?.VendorId ?? Models.Vendor.CreateUnspecified().VendorId;
            }
        }

        private void OnCategorySelected(ChangeEventArgs e) {
            string? categoryName = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryName) || e.Value?.ToString() == "new") {
                NavigationManager.NavigateTo("/category");
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

        protected override void OnInitialized() {
            base.OnInitialized();
            if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out
            User?
            user) || user == null) {
                NavigationManager.NavigateTo("/login");
                return;
            }

            NavigationManager.GetUriContext<WishPageContext>(out _, out WishPageContext? context, out Dictionary<string, string>?
            queryParameters);
            if (context == null) {
                WishItem = Models.WishItem.CreateDefault(user.UserId);
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
                        WishItem = Models.WishItem.CreateDefault(user.UserId);
                    }
                }
            } else if (Context.Action == PageContextAction.Add) {
                WishItem = Models.WishItem.CreateDefault(user.UserId);
            }

            StateHasChanged();
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
            NavigationManager.NavigateTo(url);
        }

        public async Task SaveOrUpdate() {
            await SetLoading(true);
            try {
                WishCategory? category = Database.GetCategoryByName(WishItem.ItemCategory.CategoryName);
                if (category == null) {
                    ShowError("Category not found.");
                    return;
                }

                Models.Vendor? vendor = Database.GetVendorById(WishItem.VendorId);
                if (vendor == null) {
                    ShowError("Vendor not found.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(WishItem.ItemName) || string.IsNullOrWhiteSpace(WishItem.ProductLink) || string.IsNullOrWhiteSpace(WishItem.ItemCategory.CategoryName)) {
                    ShowError("Please fill all required fields.");
                    return;
                }

                if (Action == PageContextAction.Add) {
                    var response = await HttpClient.PostAsJsonAsync("api/wishes/add", WishItem);
                    if (response.IsSuccessStatusCode) {
                        NavigationManager.NavigateTo("/wishlist");
                    } else {
                        ShowError("Failed to save wish.");
                    }
                } else if (Action == PageContextAction.Update) {
                    var response = await HttpClient.PutAsJsonAsync($"api/wishes/update", WishItem);
                    if (response.IsSuccessStatusCode) {
                        NavigateBack();
                    } else {
                        ShowError("Failed to update wish.");
                    }
                } else {
                    throw new InvalidOperationException("Invalid action type.");
                }
            } finally {
                await SetLoading(false);
            }
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