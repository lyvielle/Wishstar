using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Wishstar.Components.Shared;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Components.Pages {
    public enum WishSort {
        Added,
        Rating,
        Price,
        Name
    }

    public partial class Home {
        private sealed record WishView(WishItem Wish, string VendorName, string? CategoryName, string UserName, string? Link) {
            public bool HasImage => !string.IsNullOrWhiteSpace(Wish.ImageName);
        }

        /// <summary>Filter state that survives navigation and reloads (stored in sessionStorage).</summary>
        private sealed record FilterState(string? Search, int? User, int? Vendor, int? Category, WishSort Sort,
            SortDirection Direction, CurrencyType? Currency);

        private static readonly JsonSerializerOptions _JsonOptions = new(JsonSerializerDefaults.Web);

        private WishDatabase Database { get; } = WishDatabase.Load();

        private User[] _users = [];
        private Models.Vendor[] _vendors = [];
        private WishCategory[] _categories = [];
        private List<WishView> _visible = [];

        private ConfirmDialog _confirm = default!;
        private bool _filtersOpen;
        private bool _interactive;
        private bool _restoreScrollPending;
        private bool _disposed;

        public CurrencyType DisplayCurrency { get; set; } = CurrencyType.EUR;
        public string? SearchText { get; set; }
        public int? UserFilter { get; set; }
        public int? VendorFilter { get; set; }
        public int? CategoryFilter { get; set; }
        public WishSort SortBy { get; set; } = WishSort.Added;
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

        private bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchText) || ActiveFilterCount > 0;
        private int ActiveFilterCount => (UserFilter.HasValue ? 1 : 0) + (VendorFilter.HasValue ? 1 : 0) + (CategoryFilter.HasValue ? 1 : 0);
        private bool CanEditVendor => VendorFilter is int id && id != Models.Vendor.GetUnspecified().VendorId;
        private bool CanEditCategory => CategoryFilter is int id && id != WishCategory.GetUncategorized().CategoryId;

        private string SortDirectionLabel => (SortBy, SortDirection) switch {
            (WishSort.Added, SortDirection.Ascending) => "Oldest first",
            (WishSort.Added, _) => "Newest first",
            (WishSort.Rating, SortDirection.Ascending) => "Least wanted first",
            (WishSort.Rating, _) => "Most wanted first",
            (WishSort.Price, SortDirection.Ascending) => "Cheapest first",
            (WishSort.Price, _) => "Most expensive first",
            (_, SortDirection.Ascending) => "A → Z",
            _ => "Z → A"
        };

        protected override void OnInitialized() {
            if (Session.User is { } user) {
                DisplayCurrency = user.PreferredCurrency;
            }

            Database.Changed += OnDatabaseChanged;
            ApplyFilters();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if (firstRender) {
                _interactive = true;
                await RestoreFiltersAsync();
                _restoreScrollPending = true;
                StateHasChanged();
                return;
            }

            if (_restoreScrollPending) {
                _restoreScrollPending = false;
                await JS.InvokeVoidAsync("wishstar.scroll.restore");
            }
        }

        #region Filtering

        private void ApplyFilters() {
            _users = Database.GetUsers();
            _vendors = [.. Database.GetVendors().OrderBy(v => v.VendorId != Models.Vendor.GetUnspecified().VendorId).ThenBy(v => v.VendorName, StringComparer.CurrentCultureIgnoreCase)];
            _categories = [.. Database.GetCategories().OrderBy(c => c.CategoryId != WishCategory.GetUncategorized().CategoryId).ThenBy(c => c.CategoryName, StringComparer.CurrentCultureIgnoreCase)];

            // Drop filters that point at deleted entries.
            if (VendorFilter is int vendorId && !_vendors.Any(v => v.VendorId == vendorId)) VendorFilter = null;
            if (CategoryFilter is int categoryId && !_categories.Any(c => c.CategoryId == categoryId)) CategoryFilter = null;
            if (UserFilter is int userId && !_users.Any(u => u.UserId == userId)) UserFilter = null;

            var vendorNames = _vendors.ToDictionary(v => v.VendorId, v => v.VendorName);
            var categoryNames = _categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);
            var userNames = _users.ToDictionary(u => u.UserId, u => u.Username);
            int uncategorized = WishCategory.GetUncategorized().CategoryId;
            string unspecified = Models.Vendor.GetUnspecified().VendorName;

            IEnumerable<(WishItem Wish, int Index)> wishes = Database.GetWishes().Select((wish, index) => (wish, index));

            if (!Session.IsLoggedIn) {
                wishes = wishes.Where(w => !w.Wish.PrivateItem);
            }

            string? search = SearchText?.Trim();
            if (!string.IsNullOrEmpty(search)) {
                wishes = wishes.Where(w => w.Wish.ItemName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || w.Wish.ItemDescription.Contains(search, StringComparison.CurrentCultureIgnoreCase));
            }

            if (UserFilter is int u) wishes = wishes.Where(w => w.Wish.UserId == u);
            if (VendorFilter is int v) wishes = wishes.Where(w => w.Wish.VendorId == v);
            if (CategoryFilter is int c) wishes = wishes.Where(w => w.Wish.CategoryId == c);

            bool descending = SortDirection == SortDirection.Descending;
            wishes = SortBy switch {
                WishSort.Rating => descending
                    ? wishes.OrderByDescending(w => w.Wish.Rating).ThenBy(w => w.Index)
                    : wishes.OrderBy(w => w.Wish.Rating == 0 ? int.MaxValue : w.Wish.Rating).ThenBy(w => w.Index),
                WishSort.Price => descending
                    ? wishes.OrderByDescending(w => w.Wish.ItemPrice.EUR).ThenBy(w => w.Index)
                    : wishes.OrderBy(w => w.Wish.ItemPrice.EUR).ThenBy(w => w.Index),
                WishSort.Name => descending
                    ? wishes.OrderByDescending(w => w.Wish.ItemName, StringComparer.CurrentCultureIgnoreCase)
                    : wishes.OrderBy(w => w.Wish.ItemName, StringComparer.CurrentCultureIgnoreCase),
                _ => descending ? wishes.OrderByDescending(w => w.Index) : wishes.OrderBy(w => w.Index)
            };

            _visible = [.. wishes.Select(w => new WishView(
                w.Wish,
                vendorNames.GetValueOrDefault(w.Wish.VendorId, unspecified),
                w.Wish.CategoryId == uncategorized ? null : categoryNames.GetValueOrDefault(w.Wish.CategoryId),
                userNames.GetValueOrDefault(w.Wish.UserId, "Unknown"),
                WishService.IsSafeHttpUrl(w.Wish.ProductLink) ? w.Wish.ProductLink : null))];
        }

        private async Task ApplyFiltersAndSaveAsync() {
            ApplyFilters();
            await SaveFiltersAsync();
        }

        private async Task OnSortChangedAsync(ChangeEventArgs e) {
            if (Enum.TryParse(e.Value?.ToString(), out WishSort sort) && sort != SortBy) {
                SortBy = sort;
                // Pick the direction people usually want for each kind of sort.
                SortDirection = sort is WishSort.Rating ? SortDirection.Descending : SortDirection.Ascending;
                await ApplyFiltersAndSaveAsync();
            }
        }

        private async Task ToggleSortDirectionAsync() {
            SortDirection = SortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            await ApplyFiltersAndSaveAsync();
        }

        private async Task ClearFiltersAsync() {
            SearchText = null;
            UserFilter = VendorFilter = CategoryFilter = null;
            await ApplyFiltersAndSaveAsync();
        }

        private async Task OnCurrencyChangedAsync(ChangeEventArgs e) {
            if (!Enum.TryParse(e.Value?.ToString(), out CurrencyType currency)) {
                return;
            }

            DisplayCurrency = currency;
            if (Session.User is { } user) {
                Database.SetPreferredCurrency(user.UserId, currency);
            }

            await SaveFiltersAsync();
        }

        private async Task RestoreFiltersAsync() {
            try {
                string? json = await JS.InvokeAsync<string?>("wishstar.storage.get", "wishlist-filters");
                if (string.IsNullOrEmpty(json)) {
                    return;
                }

                var state = JsonSerializer.Deserialize<FilterState>(json, _JsonOptions);
                if (state == null) {
                    return;
                }

                SearchText = state.Search;
                UserFilter = state.User;
                VendorFilter = state.Vendor;
                CategoryFilter = state.Category;
                SortBy = Enum.IsDefined(state.Sort) ? state.Sort : WishSort.Added;
                SortDirection = Enum.IsDefined(state.Direction) ? state.Direction : SortDirection.Ascending;
                if (!Session.IsLoggedIn && state.Currency is CurrencyType currency && Enum.IsDefined(currency)) {
                    DisplayCurrency = currency;
                }

                _filtersOpen = ActiveFilterCount > 0;
                ApplyFilters();
            } catch (JsonException) {
                // Ignore stale or malformed state.
            }
        }

        private async Task SaveFiltersAsync() {
            if (!_interactive) {
                return;
            }

            var state = new FilterState(SearchText, UserFilter, VendorFilter, CategoryFilter, SortBy, SortDirection, DisplayCurrency);
            await JS.InvokeVoidAsync("wishstar.storage.set", "wishlist-filters", JsonSerializer.Serialize(state, _JsonOptions));
        }

        private void OnDatabaseChanged() {
            // Another user (or tab) changed something: refresh this view live.
            _ = InvokeAsync(() => {
                if (_disposed) {
                    return;
                }

                ApplyFilters();
                StateHasChanged();
            });
        }

        #endregion

        #region Navigation & actions

        /// <summary>
        /// Navigates via js/app.js, which first remembers the scroll position so returning to the list
        /// (back button, save or cancel) lands at exactly the same spot.
        /// </summary>
        private async Task NavigateAwayAsync(string url) {
            bool handled = false;
            try {
                handled = await JS.InvokeAsync<bool>("wishstar.nav.go", url);
            } catch (JSException) {
                // Fall back to plain navigation below.
            }

            if (!handled) {
                NavigationManager.NavigateTo(url);
            }
        }

        private Task AddWish() => NavigateAwayAsync("/wish");

        private Task EditWish(WishItem wish) => NavigateAwayAsync($"/wish/{wish.WishId}");

        private Task AddVendor() => NavigateAwayAsync($"/vendor?returnUrl={Uri.EscapeDataString("/wishlist")}");

        private Task EditVendor() => VendorFilter is int id ? NavigateAwayAsync($"/vendor/{id}?returnUrl={Uri.EscapeDataString("/wishlist")}") : Task.CompletedTask;

        private Task AddCategory() => NavigateAwayAsync($"/category?returnUrl={Uri.EscapeDataString("/wishlist")}");

        private Task EditCategory() => CategoryFilter is int id ? NavigateAwayAsync($"/category/{id}?returnUrl={Uri.EscapeDataString("/wishlist")}") : Task.CompletedTask;

        private void ConfirmDelete(WishItem wish) {
            _confirm.Show($"\"{wish.ItemName}\" will be removed from the wishlist. This cannot be undone.", () => {
                if (Session.IsLoggedIn) {
                    WishService.DeleteWish(wish.WishId);
                    ApplyFilters();
                }
            });
        }

        private async Task LogoutAsync() {
            await JS.InvokeVoidAsync("wishstar.auth.logout");
        }

        #endregion

        public ValueTask DisposeAsync() {
            _disposed = true;
            Database.Changed -= OnDatabaseChanged;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
