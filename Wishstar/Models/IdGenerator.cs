namespace Wishstar.Models {
    public static class IdGenerator {
        public static int GetNumericalId() {
            return Math.Abs(Guid.NewGuid().GetHashCode());
        }
    }
}