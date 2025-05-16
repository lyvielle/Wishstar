using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Home : IDisposable {
        public WishDatabase Database { get; } = WishDatabase.Load();
        public CurrencyType DisplayCurrencyType { get; set; } = CurrencyType.EUR;
        public bool IsLoggedIn { get; set; } = false;

        public ObservableCollection<WishItem> WishItems { get; set; } = [];
        public WishFilterPreferences FilterPreferences { get; set; } = new();

        public User? User { get; set; } = null;

        protected string _SelectedVendor = "All Vendors";
        public string SelectedVendor {
            get { return _SelectedVendor; }
            set {
                _SelectedVendor = value;
                OnVendorFilterChange(value);
            }
        }

        protected string _SelectedCategory = "All Categories";
        public string SelectedCategory {
            get { return _SelectedCategory; }
            set {
                _SelectedCategory = value;
                OnCategoryFilterChange(value);
            }
        }

        protected DotNetObjectReference<Home> ObjectReference { get; set; } = null!;

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

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if (firstRender) {
                await JSRuntime.InvokeVoidAsync("registerHomeComponent", ObjectReference = DotNetObjectReference.Create(this));
            }
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

        private void OnVendorFilterChange(string vendorName) {
            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "All Vendors") {
                FilterPreferences.VendorFilter = null;
            } else if (vendorName == "+ Add Vendor") {
                AddVendor();
            } else {
                FilterPreferences.VendorFilter = Database.GetVendorByName(vendorName);
            }
        }

        private void OnCategoryFilterChange(string categoryName) {
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName == "All Categories") {
                FilterPreferences.CategoryFilter = null;
            } else if (categoryName == "+ Add Category") {
                AddCategory();
            } else {
                FilterPreferences.CategoryFilter = Database.GetCategoryByName(categoryName);
            }
        }

        private void OnFilterPreferencesChanged(object? sender, PropertyChangedEventArgs e) {
            List<WishItem> items = [.. Database.GetWishes()];

            if (!string.IsNullOrWhiteSpace(FilterPreferences.SearchText)) {
                items = [.. items.Where(w => w.ItemName.Contains(FilterPreferences.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    w.ItemDescription.Contains(FilterPreferences.SearchText, StringComparison.OrdinalIgnoreCase))];
            }

            if (FilterPreferences.VendorFilter != null) {
                items = [.. items.Where(w => w.VendorId == FilterPreferences.VendorFilter.VendorId)];
            }

            if (FilterPreferences.UserFilter != null) {
                items = [.. items.Where(w => w.UserId == FilterPreferences.UserFilter.UserId)];
            }

            if (FilterPreferences.CategoryFilter != null) {
                items = [.. items.Where(w => w.CategoryId == FilterPreferences.CategoryFilter.CategoryId)];
            }

            WishItems = [.. items];
            StateHasChanged();
        }
        #endregion

        public void ReferToLogin() {
            NavigationManager.NavigateTo("/login", true);
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

            if (File.Exists(ImageResolver.GetImagePath(wishItem.ImageName))) {
                File.Delete(ImageResolver.GetImagePath(wishItem.ImageName));
            }

            StateHasChanged();
        }

        public void AddVendor() {
            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Add, VendorPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditVendor() {
            Models.Vendor? vendor = Database.GetVendorByName(SelectedVendor);
            if (vendor == null) {
                return;
            }

            NavigationManager.NavigateToWithContext("/vendor", PageContextAction.Update,
                context: VendorPageContext.FromVendor(vendor, GetPageContext()),
                queryParameters: new() {
                { "id", vendor.VendorId.ToString() } });
        }

        public void AddCategory() {
            NavigationManager.NavigateToWithContext("/category", PageContextAction.Add, CategoryPageContext.CreateDefaultWith(GetPageContext()));
        }

        public void EditCategory() {
            WishCategory? category = Database.GetCategoryByName(SelectedCategory);
            if (category == null) {
                return;
            }

            NavigationManager.NavigateToWithContext("/category", PageContextAction.Update, CategoryPageContext.FromCategory(category, GetPageContext()),
                queryParameters: new() {
                { "id", category.CategoryId.ToString() } });
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

        public void Dispose() {
            FilterPreferences.PropertyChanged -= OnFilterPreferencesChanged;
            ObjectReference?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}