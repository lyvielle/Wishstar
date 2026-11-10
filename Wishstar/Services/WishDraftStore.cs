using Wishstar.Models;

namespace Wishstar.Services {
    /// <summary>
    /// Keeps an unsaved wish while the user briefly leaves the wish form to create a vendor or category,
    /// so nothing typed so far is lost. Lives as long as the circuit (one browser tab).
    /// </summary>
    public class WishDraftStore {
        public string? DraftKey { get; private set; }
        public WishItem? Draft { get; private set; }
        public double PriceAmount { get; private set; }
        public CurrencyType Currency { get; private set; }

        /// <summary>Set by the vendor/category pages so the returning wish form can preselect the new entry.</summary>
        public int? CreatedVendorId { get; set; }
        public int? CreatedCategoryId { get; set; }

        public void Save(string key, WishItem draft, double priceAmount, CurrencyType currency) {
            DraftKey = key;
            Draft = (WishItem)draft.Clone();
            PriceAmount = priceAmount;
            Currency = currency;
            CreatedVendorId = null;
            CreatedCategoryId = null;
        }

        public bool TryTake(string key, out WishItem draft, out double priceAmount, out CurrencyType currency, out int? vendorId, out int? categoryId) {
            draft = null!;
            priceAmount = 0;
            currency = CurrencyType.EUR;
            vendorId = null;
            categoryId = null;

            if (Draft == null || DraftKey != key) {
                return false;
            }

            draft = Draft;
            priceAmount = PriceAmount;
            currency = Currency;
            vendorId = CreatedVendorId;
            categoryId = CreatedCategoryId;
            Clear();
            return true;
        }

        public void Clear() {
            DraftKey = null;
            Draft = null;
            CreatedVendorId = null;
            CreatedCategoryId = null;
        }
    }
}
