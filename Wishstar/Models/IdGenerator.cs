using System.Security.Cryptography;

namespace Wishstar.Models {
    public static class IdGenerator {
        /// <summary>
        /// Returns a random positive id. 0 is reserved for the built-in "Uncategorized"
        /// category and "Unspecified" vendor, so it is never returned.
        /// </summary>
        public static int GetNumericalId() {
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
    }
}
