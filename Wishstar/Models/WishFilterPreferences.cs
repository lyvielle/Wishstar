using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wishstar.Models {
    public class WishFilterPreferences : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        private Vendor? _Vendor = null;
        public Vendor? VendorFilter {
            get { return _Vendor; }
            set {
                SetProperty(ref _Vendor, value);
            }
        }
        
        private User? _User = null;
        public User? UserFilter {
            get { return _User; }
            set {
                SetProperty(ref _User, value);
            }
        }

        private WishCategory? _Category = null;
        public WishCategory? CategoryFilter {
            get { return _Category; }
            set {
                SetProperty(ref _Category, value);
            }
        }

        private string? _SearchText = null;
        public string? SearchText {
            get { return _SearchText; }
            set {
                SetProperty(ref _SearchText, value);
            }
        }

        public bool SetProperty<T>(ref T property, T value, [CallerMemberName] string? propertyName = null) {
            if(EqualityComparer<T>.Default.Equals(property, value)) {
                return false;
            }

            property = value;
            NotifyPropertyChanged(propertyName);

            return true;
        }

        public void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
