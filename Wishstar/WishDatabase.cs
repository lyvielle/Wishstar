using Wishstar.Configs;
using Wishstar.Models;

namespace Wishstar {
    public class WishDatabase {
        private static WishDatabase? _Instance = default;
        private static readonly object _Sync = new();

        private readonly CategoryConfig _CategoryConfig;
        private readonly UserConfig _UserConfig;
        private readonly WishItemConfig _WishItemConfig;
        private readonly VendorConfig _VendorConfig;

        private WishDatabase() {
            _CategoryConfig = CategoryConfig.Load();
            _UserConfig = UserConfig.Load();
            _WishItemConfig = WishItemConfig.Load();
            _VendorConfig = VendorConfig.Load();

            if (!_CategoryConfig.Categories.Any()) {
                _CategoryConfig.Categories.Add(WishCategory.CreateUncategorized());
            }

            if (!_VendorConfig.Vendors.Any()) {
                _VendorConfig.Vendors.Add(Vendor.CreateUnspecified());
            }

            // Ensure configs exist
            _CategoryConfig.Save();
            _UserConfig.Save();
            _WishItemConfig.Save();
            _VendorConfig.Save();
        }

        public static WishDatabase Load() {
            lock (_Sync) {
                return _Instance ??= new();
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

        public WishCategory UpdateCategory(WishCategory oldCategory, WishCategory newCategory) {
            lock (_Sync) {
                var index = _CategoryConfig.Categories.IndexOf(oldCategory);
                if (index == -1) {
                    throw new InvalidOperationException("Category not found");
                }

                _CategoryConfig.Categories[index] = newCategory;
                _CategoryConfig.Save();

                return newCategory;
            }
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

        public Vendor UpdateVendor(Vendor oldVendor, Vendor newVendor) {
            lock (_Sync) {
                var index = _VendorConfig.Vendors.IndexOf(oldVendor);
                if (index == -1) {
                    throw new InvalidOperationException("Vendor not found");
                }

                _VendorConfig.Vendors[index] = newVendor;
                _VendorConfig.Save();

                return newVendor;
            }
        }

        public void AddWish(WishItem wish) {
            lock (_Sync) {
                _WishItemConfig.Wishes.Add(wish);
                _WishItemConfig.Save();
            }
        }

        public WishItem UpdateWish(int wishId, WishItem newWish) {
            lock (_Sync) {
                var index = _WishItemConfig.Wishes.FindIndex(w => w.WishId == wishId);
                if (index == -1) {
                    throw new InvalidOperationException("Wish not found");
                }

                _WishItemConfig.Wishes[index] = newWish;
                _WishItemConfig.Save();

                return newWish;
            }
        }

        public void DeleteWish(WishItem wish) {
            lock (_Sync) {
                _WishItemConfig.Wishes.Remove(wish);
                _WishItemConfig.Save();
            }
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
        }

        public void DeleteCategory(WishCategory category) {
            lock (_Sync) {
                _CategoryConfig.Categories.Remove(category);
                _CategoryConfig.Save();
            }
        }

        public void AddVendor(Vendor vendor) {
            lock (_Sync) {
                _VendorConfig.Vendors.Add(vendor);
                _VendorConfig.Save();
            }
        }

        public void DeleteVendor(Vendor vendor) {
            lock (_Sync) {
                _VendorConfig.Vendors.Remove(vendor);
                _VendorConfig.Save();
            }
        }
    }
}