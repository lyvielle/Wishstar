using System.Globalization;
using System.Text;
using Wishstar.Configs;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar {
    public class WishDatabase {
        private static WishDatabase? _Instance = default;
        private static readonly object _Sync = new();

        private readonly CategoryConfig _CategoryConfig;
        private readonly UserConfig _UserConfig;
        private readonly WishItemConfig _WishItemConfig;
        private readonly VendorConfig _VendorConfig;

        /// <summary>Raised after any wish, vendor or category change, so open pages can refresh.</summary>
        public event Action? Changed;

        private WishDatabase() {
            _CategoryConfig = CategoryConfig.Load();
            _UserConfig = UserConfig.Load();
            _WishItemConfig = WishItemConfig.Load();
            _VendorConfig = VendorConfig.Load();
        }

        private static bool _Initialized = false;
        private static readonly object _InitSync = new();
        public void Initialize(ILogger? logger = null) {
            lock (_InitSync) {
                if (_Initialized) {
                    return;
                }

                lock (_Sync) {
                    if (_CategoryConfig.Categories.Count == 0) {
                        _CategoryConfig.Categories.Add(WishCategory.GetUncategorized());
                        _CategoryConfig.Save();
                    }

                    if (_VendorConfig.Vendors.Count == 0) {
                        _VendorConfig.Vendors.Add(Vendor.GetUnspecified());
                        _VendorConfig.Save();
                    }

                    if (_UserConfig.Users.Count == 0) {
                        // Placeholder account that cannot log in until a real password hash is set
                        // (see README: `dotnet Wishstar.dll hash-password`).
                        _UserConfig.Users.Add(new User(IdGenerator.GetNumericalId(), "Default", "default@default.com", "replace-me", CurrencyType.EUR));
                        _UserConfig.Save();
                    }

                    int migrated = MigrateRatingsFromNames();
                    if (migrated > 0) {
                        _WishItemConfig.Save();
                        logger?.LogInformation("Migrated star ratings out of {Count} wish names", migrated);
                    }

                    // Only clean up when wishes were actually loaded; never delete images based on an empty list.
                    if (_WishItemConfig.Wishes.Count > 0) {
                        var referenced = _WishItemConfig.Wishes.Select(w => w.ImageName)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToHashSet(StringComparer.Ordinal);
                        int deleted = ImageStore.DeleteOrphans(referenced, TimeSpan.FromDays(7));
                        if (deleted > 0) {
                            logger?.LogInformation("Deleted {Count} unreferenced images", deleted);
                        }
                    }
                }

                _Initialized = true;
            }
        }

        /// <summary>
        /// Ratings used to be expressed by adding star emoji to the wish name (e.g. "⭐⭐⭐ Headphones").
        /// Moves them into <see cref="WishItem.Rating"/> and strips them from the name.
        /// </summary>
        private int MigrateRatingsFromNames() {
            int migrated = 0;
            foreach (var wish in _WishItemConfig.Wishes) {
                if (wish.Rating != 0 || string.IsNullOrEmpty(wish.ItemName)) {
                    continue;
                }

                int stars = 0;
                bool hasStarGlyph = false;
                var cleaned = new StringBuilder(wish.ItemName.Length);
                var enumerator = StringInfo.GetTextElementEnumerator(wish.ItemName);
                while (enumerator.MoveNext()) {
                    string element = enumerator.GetTextElement();
                    string bare = element.Replace("️", string.Empty);
                    if (bare is "⭐" or "★" or "🌟") {
                        stars++;
                        hasStarGlyph = true;
                    } else if (bare is "☆") {
                        hasStarGlyph = true;
                    } else {
                        cleaned.Append(element);
                    }
                }

                if (!hasStarGlyph) {
                    continue;
                }

                string newName = string.Join(' ', cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Trim(' ', '-', '|', ':', '–', '—', '(', ')', '[', ']');
                if (string.IsNullOrWhiteSpace(newName)) {
                    continue;
                }

                wish.ItemName = newName;
                wish.Rating = Math.Clamp(stars, 0, WishItem.MaxRating);
                migrated++;
            }

            return migrated;
        }

        public static WishDatabase Load() {
            lock (_Sync) {
                return _Instance ??= new();
            }
        }

        private void NotifyChanged() {
            try {
                Changed?.Invoke();
            } catch {
                // A failing subscriber must never break a save.
            }
        }

        public WishItem[] GetWishes() {
            lock (_Sync) {
                return [.. _WishItemConfig.Wishes];
            }
        }

        public WishItem? GetWishById(int wishId) {
            lock (_Sync) {
                return _WishItemConfig.Wishes.FirstOrDefault(w => w.WishId == wishId);
            }
        }

        /// <summary>True when the image belongs to at least one wish that anonymous visitors may see.</summary>
        public bool IsImagePublic(string imageName) {
            lock (_Sync) {
                return _WishItemConfig.Wishes.Any(w => !w.PrivateItem && w.ImageName == imageName);
            }
        }

        public bool IsImageReferenced(string imageName) {
            lock (_Sync) {
                return _WishItemConfig.Wishes.Any(w => w.ImageName == imageName);
            }
        }

        public User[] GetUsers() {
            lock (_Sync) {
                return [.. _UserConfig.Users];
            }
        }

        public User? GetUserById(int userId) {
            lock (_Sync) {
                return _UserConfig.Users.FirstOrDefault(u => u.UserId == userId);
            }
        }

        public User? GetUserByUsername(string username) {
            lock (_Sync) {
                return _UserConfig.Users.FirstOrDefault(u => u.Username == username);
            }
        }

        public User? GetUserByEmail(string email) {
            lock (_Sync) {
                return _UserConfig.Users.FirstOrDefault(u => string.Equals(u.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        public User UpdateUser(User oldUser, User newUser) {
            lock (_Sync) {
                var index = _UserConfig.Users.IndexOf(oldUser);
                if (index == -1) {
                    throw new InvalidOperationException("User not found");
                }

                _UserConfig.Users[index] = newUser;
                _UserConfig.Save();

                return newUser;
            }
        }

        public void SetPreferredCurrency(int userId, CurrencyType currency) {
            lock (_Sync) {
                var user = _UserConfig.Users.FirstOrDefault(u => u.UserId == userId);
                if (user != null && user.PreferredCurrency != currency) {
                    user.PreferredCurrency = currency;
                    _UserConfig.Save();
                }
            }
        }

        public void SetPasswordHash(int userId, string passwordHash) {
            lock (_Sync) {
                var user = _UserConfig.Users.FirstOrDefault(u => u.UserId == userId)
                    ?? throw new InvalidOperationException("User not found");
                user.Token = passwordHash;
                _UserConfig.Save();
            }
        }

        public WishCategory[] GetCategories() {
            lock (_Sync) {
                return [.. _CategoryConfig.Categories];
            }
        }

        public WishCategory? GetCategoryByName(string categoryName) {
            lock (_Sync) {
                return _CategoryConfig.Categories.FirstOrDefault(c => c.CategoryName == categoryName);
            }
        }

        public WishCategory? GetCategoryById(int categoryId) {
            lock (_Sync) {
                return _CategoryConfig.Categories.FirstOrDefault(c => c.CategoryId == categoryId);
            }
        }

        public WishCategory UpdateCategory(int categoryId, WishCategory category) {
            lock (_Sync) {
                var index = _CategoryConfig.Categories.FindIndex(c => c.CategoryId == categoryId);
                if (index == -1) {
                    throw new InvalidOperationException("Category not found");
                }

                _CategoryConfig.Categories[index] = category;
                _CategoryConfig.Save();
            }

            NotifyChanged();
            return category;
        }

        public Vendor[] GetVendors() {
            lock (_Sync) {
                return [.. _VendorConfig.Vendors];
            }
        }

        public Vendor? GetVendorByName(string vendorName) {
            lock (_Sync) {
                return _VendorConfig.Vendors.FirstOrDefault(v => v.VendorName == vendorName);
            }
        }

        public Vendor? GetVendorById(int vendorId) {
            lock (_Sync) {
                return _VendorConfig.Vendors.FirstOrDefault(v => v.VendorId == vendorId);
            }
        }

        public Vendor UpdateVendor(int vendorId, Vendor vendor) {
            lock (_Sync) {
                var index = _VendorConfig.Vendors.FindIndex(i => i.VendorId == vendorId);
                if (index == -1) {
                    throw new InvalidOperationException("Vendor not found");
                }

                _VendorConfig.Vendors[index] = vendor;
                _VendorConfig.Save();
            }

            NotifyChanged();
            return vendor;
        }

        public void AddWish(WishItem wish) {
            lock (_Sync) {
                _WishItemConfig.Wishes.Add(wish);
                _WishItemConfig.Save();
            }

            NotifyChanged();
        }

        public WishItem UpdateWish(int wishId, WishItem newWish) {
            lock (_Sync) {
                var index = _WishItemConfig.Wishes.FindIndex(w => w.WishId == wishId);
                if (index == -1) {
                    throw new InvalidOperationException("Wish not found");
                }

                _WishItemConfig.Wishes[index] = newWish;
                _WishItemConfig.Save();
            }

            NotifyChanged();
            return newWish;
        }

        public void DeleteWish(WishItem wish) {
            lock (_Sync) {
                _WishItemConfig.Wishes.RemoveAll(w => w.WishId == wish.WishId);
                _WishItemConfig.Save();
            }

            NotifyChanged();
        }

        public void AddUser(User user) {
            lock (_Sync) {
                _UserConfig.Users.Add(user);
                _UserConfig.Save();
            }
        }

        public void DeleteUser(User user) {
            lock (_Sync) {
                _UserConfig.Users.Remove(user);
                _UserConfig.Save();
            }
        }

        public void AddCategory(WishCategory category) {
            lock (_Sync) {
                _CategoryConfig.Categories.Add(category);
                _CategoryConfig.Save();
            }

            NotifyChanged();
        }

        /// <summary>Deletes a category and moves its wishes to "Uncategorized" in a single save.</summary>
        public void DeleteCategory(WishCategory category) {
            lock (_Sync) {
                _CategoryConfig.Categories.RemoveAll(c => c.CategoryId == category.CategoryId);
                _CategoryConfig.Save();

                int fallbackId = WishCategory.GetUncategorized().CategoryId;
                bool changed = false;
                foreach (var wish in _WishItemConfig.Wishes.Where(w => w.CategoryId == category.CategoryId)) {
                    wish.CategoryId = fallbackId;
                    changed = true;
                }

                if (changed) {
                    _WishItemConfig.Save();
                }
            }

            NotifyChanged();
        }

        public void AddVendor(Vendor vendor) {
            lock (_Sync) {
                _VendorConfig.Vendors.Add(vendor);
                _VendorConfig.Save();
            }

            NotifyChanged();
        }

        /// <summary>Deletes a vendor and moves its wishes to "Unspecified" in a single save.</summary>
        public void DeleteVendor(Vendor vendor) {
            lock (_Sync) {
                _VendorConfig.Vendors.RemoveAll(v => v.VendorId == vendor.VendorId);
                _VendorConfig.Save();

                int fallbackId = Vendor.GetUnspecified().VendorId;
                bool changed = false;
                foreach (var wish in _WishItemConfig.Wishes.Where(w => w.VendorId == vendor.VendorId)) {
                    wish.VendorId = fallbackId;
                    changed = true;
                }

                if (changed) {
                    _WishItemConfig.Save();
                }
            }

            NotifyChanged();
        }
    }
}
