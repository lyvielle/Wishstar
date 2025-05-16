namespace Wishstar.Models {
    public class Vendor(int vendorId, string vendorName, string website) {
        public int VendorId { get; set; } = vendorId;

        public string VendorName { get; set; } = vendorName;
        public string Website { get; set; } = website;

        public static Vendor GetUnspecified() {
            return new Vendor(0, "Unspecified", string.Empty);
        }

        public static Vendor CreateDefault() {
            return new Vendor(IdGenerator.GetNumericalId(), string.Empty, string.Empty);
        }
    }
}