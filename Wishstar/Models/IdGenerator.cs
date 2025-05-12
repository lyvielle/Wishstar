namespace Wishstar.Models {
    public static class IdGenerator {
        public static int GetNumericalId() {
            return DateTime.UtcNow.Ticks.GetHashCode();
        }
    }
}