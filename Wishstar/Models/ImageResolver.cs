namespace Wishstar.Models {
    public static class ImageResolver {
        public static string? GetImagePath(string imageName) {
            if (string.IsNullOrWhiteSpace(imageName)) {
                return null;
            }

            string imagePath = Path.Combine(Path.GetFullPath(AppConfig.ImageBasePath), imageName);
            if (!File.Exists(imagePath)) {
                return null;
            }

            return imagePath;
        }

        public static string GetImageUrl(string imageName) {
            return $"{AppConfig.FullCurrentDomain}/icon?file={Uri.EscapeDataString(imageName)}";
        }

        public static string GetRelativeImageUrl(string imageName) {
            return $"/icon?file={Uri.EscapeDataString(imageName)}";
        }
    }
}