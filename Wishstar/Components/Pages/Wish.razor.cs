using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Wishstar.Components.Shared;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Components.Pages {
    public partial class Wish {
        private sealed record UploadResult(bool Ok, string? FileName, string? Error);

        [Parameter] public int? WishId { get; set; }

        private WishDatabase Database { get; } = WishDatabase.Load();

        private WishItem? _wish;
        private Models.Vendor[] _vendors = [];
        private WishCategory[] _categories = [];
        private ElementReference _fileInput;
        private ConfirmDialog? _confirm;
        private readonly HashSet<string> _uploadedImages = [];

        private string? _error;
        private bool _notFound;
        private bool _saving;
        private bool _uploading;

        private bool IsEdit => WishId.HasValue;
        private bool HasImage => !string.IsNullOrWhiteSpace(_wish?.ImageName);
        private string DraftKey => WishId?.ToString() ?? "new";

        private CurrencyType Currency { get; set; } = CurrencyType.EUR;

        private double _priceAmount;
        private bool _priceDirty;
        private double PriceAmount {
            get => _priceAmount;
            set {
                _priceAmount = value;
                _priceDirty = true;
            }
        }

        protected override void OnInitialized() {
            if (Session.User is not { } user) {
                NavigationManager.RedirectToLogin();
                return;
            }

            LoadLists();
            Currency = user.PreferredCurrency;

            if (Drafts.TryTake(DraftKey, out WishItem draft, out double price, out CurrencyType currency, out int? vendorId, out int? categoryId)) {
                // Coming back from creating a vendor/category: restore what was typed and select the new entry.
                _wish = draft;
                Currency = currency;
                _priceAmount = price;
                _priceDirty = true;
                if (vendorId is int v) _wish.VendorId = v;
                if (categoryId is int c) _wish.CategoryId = c;
                return;
            }

            if (WishId is int id) {
                var existing = Database.GetWishById(id);
                if (existing == null) {
                    _notFound = true;
                    return;
                }

                _wish = (WishItem)existing.Clone();
                _priceAmount = Math.Round(_wish.ItemPrice.GetPrice(Currency), 2);
            } else {
                _wish = WishItem.CreateDefault(user.UserId);
            }
        }

        private void LoadLists() {
            _vendors = [.. Database.GetVendors().OrderBy(v => v.VendorId != Models.Vendor.GetUnspecified().VendorId).ThenBy(v => v.VendorName, StringComparer.CurrentCultureIgnoreCase)];
            _categories = [.. Database.GetCategories().OrderBy(c => c.CategoryId != WishCategory.GetUncategorized().CategoryId).ThenBy(c => c.CategoryName, StringComparer.CurrentCultureIgnoreCase)];
        }

        private void OnCurrencyChanged(ChangeEventArgs e) {
            if (Enum.TryParse(e.Value?.ToString(), out CurrencyType currency)) {
                // The entered amount is kept and interpreted in the newly selected currency.
                Currency = currency;
                _priceDirty = true;
            }
        }

        private async Task OnFileSelectedAsync() {
            if (_wish == null) {
                return;
            }

            _uploading = true;
            _error = null;
            StateHasChanged();

            try {
                var result = await JS.InvokeAsync<UploadResult>("wishstar.upload.fromInput", TimeSpan.FromMinutes(3), _fileInput);
                if (result.Ok && !string.IsNullOrEmpty(result.FileName)) {
                    // Replacing a photo uploaded a moment ago: that one is not needed anymore.
                    if (_uploadedImages.Contains(_wish.ImageName) && !Database.IsImageReferenced(_wish.ImageName)) {
                        ImageStore.Delete(_wish.ImageName);
                    }

                    _wish.ImageName = result.FileName;
                    _uploadedImages.Add(result.FileName);
                } else if (result.Error != null) {
                    _error = result.Error;
                }
            } catch (Exception ex) when (ex is JSException or TaskCanceledException) {
                _error = "The photo could not be uploaded. Please try again.";
            } finally {
                _uploading = false;
            }
        }

        private async Task SaveAsync() {
            if (_wish == null || _saving || _uploading || Session.User is not { } user) {
                return;
            }

            _saving = true;
            _error = null;

            if (_priceDirty) {
                _wish.ItemPrice = new Price(Math.Max(0, PriceAmount), Currency);
            }

            var result = IsEdit ? WishService.UpdateWish(_wish) : WishService.AddWish(_wish, user);
            if (!result.Success) {
                _error = result.Error;
                _saving = false;
                await ScrollToTopAsync();
                return;
            }

            Drafts.Clear();
            await GoBackAsync();
        }

        private void ConfirmDelete() {
            if (_wish == null || _confirm == null) {
                return;
            }

            _confirm.Show($"\"{_wish.ItemName}\" will be removed from the wishlist. This cannot be undone.", async () => {
                var result = WishService.DeleteWish(_wish.WishId);
                if (!result.Success) {
                    _error = result.Error;
                    return;
                }

                Drafts.Clear();
                await GoBackAsync();
            });
        }

        private Task AddVendorAsync() => LeaveForAsync("/vendor");

        private Task AddCategoryAsync() => LeaveForAsync("/category");

        /// <summary>Keeps everything typed so far and opens the vendor/category form.</summary>
        private async Task LeaveForAsync(string page) {
            if (_wish == null) {
                return;
            }

            Drafts.Save(DraftKey, _wish, PriceAmount, Currency);
            string returnUrl = IsEdit ? $"/wish/{WishId}" : "/wish";
            string url = $"{page}?returnUrl={Uri.EscapeDataString(returnUrl)}&from=wish";
            if (!await JS.InvokeAsync<bool>("wishstar.nav.go", url)) {
                NavigationManager.NavigateTo(url);
            }
        }

        private async Task GoBackAsync() {
            // Prefer a real "history back" so the wishlist is restored exactly where the user left it.
            if (!await JS.InvokeAsync<bool>("wishstar.nav.back")) {
                NavigationManager.NavigateTo(NavigationHelper.Home, replace: true);
            }
        }

        private async Task ScrollToTopAsync() {
            try {
                await JS.InvokeVoidAsync("scrollTo", new { top = 0, behavior = "smooth" });
            } catch (JSException) {
                // Not critical.
            }
        }
    }
}
