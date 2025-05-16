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

            if (_CategoryConfig.Categories.Count == 0) {
                _CategoryConfig.Categories.Add(WishCategory.GetUncategorized());
            }

            if (_VendorConfig.Vendors.Count == 0) {
                _VendorConfig.Vendors.Add(Vendor.GetUnspecified());
            }


            if (_UserConfig.Users.Count == 0) {
                _UserConfig.Users.Add(new User(IdGenerator.GetNumericalId(), "Default", "default@default.com", "replace-me", CurrencyType.EUR));
            }

            // Ensure configs exist
            _CategoryConfig.Save();
            _UserConfig.Save();
            _WishItemConfig.Save();
            _VendorConfig.Save();
        }

        private static bool _Initialized = false;
        private static readonly object _InitSync = new();
        public void Initialize() {
            lock (_InitSync) {
                if (!_Initialized) {
                    if (_CategoryConfig.Categories.Count == 0) {
                        _CategoryConfig.Categories.Add(WishCategory.GetUncategorized());
                    } else {
                        _CategoryConfig.TryCleanConfig();
                    }

                    if (_VendorConfig.Vendors.Count == 0) {
                        _VendorConfig.Vendors.Add(Vendor.GetUnspecified());
                    } else {
                        _VendorConfig.TryCleanConfig();
                    }


                    if (_UserConfig.Users.Count == 0) {
                        _UserConfig.Users.Add(new User(IdGenerator.GetNumericalId(), "Default", "default@default.com", "replace-me", CurrencyType.EUR));
                    } else {
                        _UserConfig.TryCleanConfig();
                    }

                    var referencedImages = _WishItemConfig.Wishes.Select(w => w.ImageName).Distinct().Select(i => ImageResolver.GetImagePath(i));
                    var existingImages = Directory.GetFiles(ImageResolver.ImageDirectory).Select(Path.GetFileName);
                    foreach (var image in existingImages) {
                        if (!referencedImages.Contains(image)) {
                            if (File.Exists(image)) {
                                File.Delete(image);
                            }
                        }
                    }

                    _Initialized = true;
                }
            }
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

                return category;
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

        public Vendor UpdateVendor(int vendorId, Vendor vendor) {
            lock (_Sync) {
                var index = _VendorConfig.Vendors.FindIndex(i => i.VendorId == vendorId);
                if (index == -1) {
                    throw new InvalidOperationException("Vendor not found");
                }

                _VendorConfig.Vendors[index] = vendor;
                _VendorConfig.Save();

                return vendor;
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