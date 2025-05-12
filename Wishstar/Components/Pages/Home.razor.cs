using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Home {
        public WishDatabase Database { get; } = WishDatabase.Load();
        public CurrencyType DisplayCurrencyType { get; set; } = CurrencyType.EUR;
        public bool IsLoggedIn { get; set; } = false;

        public ObservableCollection<WishItem> WishItems { get; set; } = [];
        public WishFilterPreferences FilterPreferences { get; set; } = new();

        public User? User { get; set; } = null;

        protected override void OnInitialized() {
            base.OnInitialized();
            WishItems = [.. Database.GetWishes()];
            if (HttpContextAccessor.HttpContext != null) {
                if (HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) && user != null) {
                    IsLoggedIn = true;
                    User = user;
                }
            }

            FilterPreferences.PropertyChanged += OnFilterPreferencesChanged;

            StateHasChanged();
        }

        #region Events
        private void OnCurrencyChange(ChangeEventArgs e) {
            var selectedCurrency = (CurrencyType)Enum.Parse(typeof(CurrencyType), e.Value!.ToString()!);
            SetPreferredCurrency(selectedCurrency);
        }

        private void OnSearchTextChange(ChangeEventArgs e) {
            string searchText = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(searchText)) {
                FilterPreferences.SearchText = null;
            } else {
                FilterPreferences.SearchText = searchText;
            }
        }

        private void OnUserFilterChange(ChangeEventArgs e) {
            string username = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username) || username == "All Users") {
                FilterPreferences.UserFilter = null;
            } else {
                FilterPreferences.UserFilter = Database.GetUserByUsername(username);
            }
        }

        private void OnVendorFilterChange(ChangeEventArgs e) {
            string vendorName = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "All Vendors") {
                FilterPreferences.VendorFilter = null;
            } else {
                FilterPreferences.VendorFilter = Database.GetVendorByName(vendorName);
            }
        }

        private void OnCategoryFilterChange(ChangeEventArgs e) {
            string categoryName = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName == "All Categories") {
                FilterPreferences.CategoryFilter = null;
            } else {
                FilterPreferences.CategoryFilter = Database.GetCategoryByName(categoryName);
            }
        }

        private void OnFilterPreferencesChanged(object? sender, PropertyChangedEventArgs e) {
            List<WishItem> items = [.. Database.GetWishes()];

            if (!string.IsNullOrWhiteSpace(FilterPreferences.SearchText)) {
                items = items.Where(w => w.ItemName.Contains(FilterPreferences.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    w.ItemDescription.Contains(FilterPreferences.SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (FilterPreferences.VendorFilter != null) {
                items = items.Where(w => w.VendorName == FilterPreferences.VendorFilter.VendorName).ToList();
            }

            if (FilterPreferences.UserFilter != null) {
                items = items.Where(w => w.UserId == FilterPreferences.UserFilter.UserId).ToList();
            }

            if (FilterPreferences.CategoryFilter != null) {
                items = items.Where(w => w.ItemCategory.CategoryName == FilterPreferences.CategoryFilter.CategoryName).ToList();
            }

            WishItems = [.. items];
            StateHasChanged();
        }
        #endregion

        public void ReferToLogin() {
            NavigationManager.NavigateTo("/login");
        }

        public void SetPreferredCurrency(CurrencyType currency) {
            DisplayCurrencyType = currency;
            if (IsLoggedIn && User != null) {
                User.PreferredCurrency = currency;
                Database.UpdateUser(Database.GetUserById(User.UserId)!, User);
            }

            StateHasChanged();
        }

        public void AddWishItem() {
            NavigationManager.NavigateToWithContext("/wish", PageContextAction.Add, context: WishPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditWishItem(WishItem wishItem) {
            NavigationManager.NavigateToWithContext("/wish", PageContextAction.Update,
                context: WishPageContext.FromWish(wishItem, GetPageContext()),
                queryParameters: new() {
                { "id", wishItem.WishId.ToString() } });
        }

        public void DeleteWishItem(WishItem wishItem) {
            Database.DeleteWish(wishItem);
            WishItems.Remove(wishItem);
            StateHasChanged();
        }

        public void AddVendor(Models.Vendor vendor) {
            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Add, VendorPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditVendor(Models.Vendor vendor) {
            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Update,
                context: VendorPageContext.FromVendor(vendor, GetPageContext()),
                queryParameters: new() {
                { "id", vendor.VendorName } });
        }

        public void AddCategory() {
            NavigationManager.NavigateToWithContext("/category", PageContextAction.Add, CategoryPageContext.CreateDefaultWith(GetPageContext()));
        }

        public async Task DeleteCategory(WishCategory category) {
            using var response = await HttpClient.PostAsJsonAsync("/api/categories/delete", new { Name = category.CategoryName });
            if (response.IsSuccessStatusCode) {
                WishItems = [.. Database.GetWishes()];
                StateHasChanged();
            }
        }

        private IPageContext GetPageContext() {
            return ParentPageContext.Create(NavigationManager.Uri);
        }
    }
}