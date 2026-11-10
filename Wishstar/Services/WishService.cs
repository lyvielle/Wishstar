using Wishstar.Models;

namespace Wishstar.Services {
    public readonly record struct ServiceResult(bool Success, string? Error) {
        public static ServiceResult Ok() => new(true, null);
        public static ServiceResult Fail(string error) => new(false, error);
    }

    /// <summary>
    /// All validation and mutations for wishes, vendors and categories. Used by both the Blazor pages
    /// and the HTTP API so the rules are enforced in one place.
    /// </summary>
    public static class WishService {
        public const int MaxNameLength = 200;
        public const int MaxDescriptionLength = 4000;
        public const int MaxUrlLength = 2048;
        private const double MaxPrice = 100_000_000;

        private static WishDatabase Db => WishDatabase.Load();

        /// <summary>Only absolute http(s) URLs are allowed (blocks javascript:, data: and similar links).</summary>
        public static bool IsSafeHttpUrl(string? url) {
            return !string.IsNullOrWhiteSpace(url)
                && url.Length <= MaxUrlLength
                && Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        /// <summary>Adds https:// to links typed without a scheme, e.g. "amazon.de/item".</summary>
        public static string NormalizeUrl(string? url) {
            string trimmed = (url ?? string.Empty).Trim();
            if (trimmed.Length > 0 && !trimmed.Contains("://", StringComparison.Ordinal) && trimmed.Contains('.')) {
                trimmed = "https://" + trimmed;
            }

            return trimmed;
        }

        #region Wishes

        public static ServiceResult AddWish(WishItem input, User actor) {
            var wish = (WishItem)input.Clone();
            if (wish.WishId <= 0 || Db.GetWishById(wish.WishId) != null) {
                wish.WishId = IdGenerator.GetNumericalId();
            }

            // The owner is always the logged-in user, never whatever the client sent.
            wish.UserId = actor.UserId;

            var validation = Normalize(wish);
            if (!validation.Success) {
                return validation;
            }

            Db.AddWish(wish);
            input.WishId = wish.WishId;
            return ServiceResult.Ok();
        }

        public static ServiceResult UpdateWish(WishItem input) {
            var existing = Db.GetWishById(input.WishId);
            if (existing == null) {
                return ServiceResult.Fail("This wish no longer exists.");
            }

            var wish = (WishItem)input.Clone();
            wish.UserId = existing.UserId;

            var validation = Normalize(wish);
            if (!validation.Success) {
                return validation;
            }

            string previousImage = existing.ImageName;
            Db.UpdateWish(existing.WishId, wish);

            if (!string.IsNullOrEmpty(previousImage) && previousImage != wish.ImageName && !Db.IsImageReferenced(previousImage)) {
                ImageStore.Delete(previousImage);
            }

            return ServiceResult.Ok();
        }

        public static ServiceResult DeleteWish(int wishId) {
            var wish = Db.GetWishById(wishId);
            if (wish == null) {
                return ServiceResult.Fail("This wish no longer exists.");
            }

            Db.DeleteWish(wish);
            if (!string.IsNullOrEmpty(wish.ImageName) && !Db.IsImageReferenced(wish.ImageName)) {
                ImageStore.Delete(wish.ImageName);
            }

            return ServiceResult.Ok();
        }

        private static ServiceResult Normalize(WishItem wish) {
            wish.ItemName = (wish.ItemName ?? string.Empty).Trim();
            wish.ItemDescription = (wish.ItemDescription ?? string.Empty).Trim();
            wish.ProductLink = NormalizeUrl(wish.ProductLink);
            wish.ImageName = (wish.ImageName ?? string.Empty).Trim();

            if (wish.ItemName.Length == 0) {
                return ServiceResult.Fail("Please enter a name for the wish.");
            }

            if (wish.ItemName.Length > MaxNameLength) {
                return ServiceResult.Fail($"The name can be at most {MaxNameLength} characters long.");
            }

            if (wish.ItemDescription.Length > MaxDescriptionLength) {
                return ServiceResult.Fail($"The description can be at most {MaxDescriptionLength} characters long.");
            }

            if (!IsSafeHttpUrl(wish.ProductLink)) {
                return ServiceResult.Fail("Please enter a valid product link (starting with https://).");
            }

            if (wish.ItemPrice == null || double.IsNaN(wish.ItemPrice.EUR) || double.IsInfinity(wish.ItemPrice.EUR)
                || wish.ItemPrice.EUR < 0 || wish.ItemPrice.EUR > MaxPrice) {
                return ServiceResult.Fail("Please enter a valid price.");
            }

            if (wish.Rating < 0 || wish.Rating > WishItem.MaxRating) {
                return ServiceResult.Fail($"The rating must be between {WishItem.MinRating} and {WishItem.MaxRating} stars.");
            }

            if (wish.ImageName.Length > 0 && !ImageStore.Exists(wish.ImageName)) {
                return ServiceResult.Fail("The image could not be found. Please upload it again.");
            }

            if (Db.GetVendorById(wish.VendorId) == null) {
                wish.VendorId = Vendor.GetUnspecified().VendorId;
            }

            if (Db.GetCategoryById(wish.CategoryId) == null) {
                wish.CategoryId = WishCategory.GetUncategorized().CategoryId;
            }

            return ServiceResult.Ok();
        }

        #endregion

        #region Vendors

        public static ServiceResult AddVendor(Vendor input) {
            var vendor = (Vendor)input.Clone();
            if (vendor.VendorId <= 0 || Db.GetVendorById(vendor.VendorId) != null) {
                vendor.VendorId = IdGenerator.GetNumericalId();
            }

            var validation = Normalize(vendor);
            if (!validation.Success) {
                return validation;
            }

            Db.AddVendor(vendor);
            input.VendorId = vendor.VendorId;
            return ServiceResult.Ok();
        }

        public static ServiceResult UpdateVendor(Vendor input) {
            if (input.VendorId == Vendor.GetUnspecified().VendorId) {
                return ServiceResult.Fail("The built-in vendor cannot be changed.");
            }

            if (Db.GetVendorById(input.VendorId) == null) {
                return ServiceResult.Fail("This vendor no longer exists.");
            }

            var vendor = (Vendor)input.Clone();
            var validation = Normalize(vendor);
            if (!validation.Success) {
                return validation;
            }

            Db.UpdateVendor(vendor.VendorId, vendor);
            return ServiceResult.Ok();
        }

        public static ServiceResult DeleteVendor(int vendorId) {
            if (vendorId == Vendor.GetUnspecified().VendorId) {
                return ServiceResult.Fail("The built-in vendor cannot be deleted.");
            }

            var vendor = Db.GetVendorById(vendorId);
            if (vendor == null) {
                return ServiceResult.Fail("This vendor no longer exists.");
            }

            Db.DeleteVendor(vendor);
            return ServiceResult.Ok();
        }

        private static ServiceResult Normalize(Vendor vendor) {
            vendor.VendorName = (vendor.VendorName ?? string.Empty).Trim();
            vendor.Website = NormalizeUrl(vendor.Website);

            if (vendor.VendorName.Length == 0) {
                return ServiceResult.Fail("Please enter a vendor name.");
            }

            if (vendor.VendorName.Length > MaxNameLength) {
                return ServiceResult.Fail($"The vendor name can be at most {MaxNameLength} characters long.");
            }

            if (!IsSafeHttpUrl(vendor.Website)) {
                return ServiceResult.Fail("Please enter a valid website (starting with https://).");
            }

            var sameName = Db.GetVendors().FirstOrDefault(v => v.VendorId != vendor.VendorId
                && string.Equals(v.VendorName, vendor.VendorName, StringComparison.OrdinalIgnoreCase));
            if (sameName != null) {
                return ServiceResult.Fail("A vendor with this name already exists.");
            }

            return ServiceResult.Ok();
        }

        #endregion

        #region Categories

        public static ServiceResult AddCategory(WishCategory input) {
            var category = (WishCategory)input.Clone();
            if (category.CategoryId <= 0 || Db.GetCategoryById(category.CategoryId) != null) {
                category.CategoryId = IdGenerator.GetNumericalId();
            }

            var validation = Normalize(category);
            if (!validation.Success) {
                return validation;
            }

            Db.AddCategory(category);
            input.CategoryId = category.CategoryId;
            return ServiceResult.Ok();
        }

        public static ServiceResult UpdateCategory(WishCategory input) {
            if (input.CategoryId == WishCategory.GetUncategorized().CategoryId) {
                return ServiceResult.Fail("The built-in category cannot be changed.");
            }

            if (Db.GetCategoryById(input.CategoryId) == null) {
                return ServiceResult.Fail("This category no longer exists.");
            }

            var category = (WishCategory)input.Clone();
            var validation = Normalize(category);
            if (!validation.Success) {
                return validation;
            }

            Db.UpdateCategory(category.CategoryId, category);
            return ServiceResult.Ok();
        }

        public static ServiceResult DeleteCategory(int categoryId) {
            if (categoryId == WishCategory.GetUncategorized().CategoryId) {
                return ServiceResult.Fail("The built-in category cannot be deleted.");
            }

            var category = Db.GetCategoryById(categoryId);
            if (category == null) {
                return ServiceResult.Fail("This category no longer exists.");
            }

            Db.DeleteCategory(category);
            return ServiceResult.Ok();
        }

        private static ServiceResult Normalize(WishCategory category) {
            category.CategoryName = (category.CategoryName ?? string.Empty).Trim();
            category.CategoryDescription = (category.CategoryDescription ?? string.Empty).Trim();

            if (category.CategoryName.Length == 0) {
                return ServiceResult.Fail("Please enter a category name.");
            }

            if (category.CategoryName.Length > MaxNameLength) {
                return ServiceResult.Fail($"The category name can be at most {MaxNameLength} characters long.");
            }

            if (category.CategoryDescription.Length > MaxDescriptionLength) {
                return ServiceResult.Fail($"The description can be at most {MaxDescriptionLength} characters long.");
            }

            var sameName = Db.GetCategories().FirstOrDefault(c => c.CategoryId != category.CategoryId
                && string.Equals(c.CategoryName, category.CategoryName, StringComparison.OrdinalIgnoreCase));
            if (sameName != null) {
                return ServiceResult.Fail("A category with this name already exists.");
            }

            return ServiceResult.Ok();
        }

        #endregion
    }
}
