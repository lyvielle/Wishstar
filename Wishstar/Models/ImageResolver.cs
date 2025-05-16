namespace Wishstar.Models {
    public static class ImageResolver {
        public static string ImageDirectory {
            get {
                return Path.GetFullPath(AppConfig.ImageBasePath);
            }
        }

        public static string GetImagePath(string imageName) {
            if (string.IsNullOrWhiteSpace(imageName)) {
                throw new ArgumentNullException(nameof(imageName), "Image name cannot be null or empty.");
            }

            string imagePath = Path.Combine(ImageDirectory, imageName);
            if (!imagePath.StartsWith(ImageDirectory)) {
                throw new InvalidOperationException("Invalid image path.");
            }

            return imagePath;
        }

        public static string GetRelativeImageUrl(string imageName) {
            return $"/icon?file={Uri.EscapeDataString(imageName)}";
        }
    }
}