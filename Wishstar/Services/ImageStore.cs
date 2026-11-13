using System.Collections.Concurrent;
using Microsoft.AspNetCore.StaticFiles;
using SkiaSharp;
using Wishstar.Models;

namespace Wishstar.Services {
    public enum ImageVariant {
        Original,
        /// <summary>480px, used for wish cards on most screens.</summary>
        Small,
        /// <summary>960px, used for high-DPI cards and the edit preview.</summary>
        Medium
    }

    /// <summary>
    /// Stores wish images. Uploads are decoded, auto-rotated, downscaled and re-encoded as WebP
    /// (which strips EXIF/GPS metadata and guarantees only real images are ever served).
    /// Smaller variants are generated on demand and cached on disk.
    /// </summary>
    public static class ImageStore {
        public const long MaxUploadBytes = 25 * 1024 * 1024;
        private const long MaxPixels = 80_000_000;
        private const int OriginalMaxSize = 2048;
        private const int OriginalQuality = 85;
        private const int VariantQuality = 78;

        private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _PendingVariants = new();
        private static readonly FileExtensionContentTypeProvider _ContentTypes = new();

        public static string ImageDirectory => Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppConfig.ImageBasePath));
        public static string CacheDirectory => Path.Combine(ImageDirectory, ".cache");

        public static int GetWidth(ImageVariant variant) => variant switch {
            ImageVariant.Small => 480,
            ImageVariant.Medium => 960,
            _ => OriginalMaxSize
        };

        public static string GetUrl(string imageName, ImageVariant variant = ImageVariant.Original) {
            string size = variant switch {
                ImageVariant.Small => "s",
                ImageVariant.Medium => "m",
                _ => "o"
            };

            return $"/img/{size}/{Uri.EscapeDataString(imageName)}";
        }

        /// <summary>Only plain file names created by this app are valid (no paths, no hidden files).</summary>
        public static bool IsValidName(string? imageName) {
            if (string.IsNullOrWhiteSpace(imageName) || imageName.Length > 128 || imageName.StartsWith('.')) {
                return false;
            }

            return imageName == Path.GetFileName(imageName)
                && imageName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !imageName.Contains("..", StringComparison.Ordinal);
        }

        public static string? GetOriginalPath(string? imageName) {
            if (!IsValidName(imageName)) {
                return null;
            }

            string fullPath = Path.GetFullPath(Path.Combine(ImageDirectory, imageName!));
            if (!fullPath.StartsWith(ImageDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        public static bool Exists(string? imageName) => GetOriginalPath(imageName) != null;

        public static string GetContentType(string path) {
            return _ContentTypes.TryGetContentType(path, out string? contentType) ? contentType : "application/octet-stream";
        }

        /// <summary>Decodes, normalizes and stores an uploaded image. Returns the new image name.</summary>
        public static async Task<string> SaveUploadAsync(Stream upload, CancellationToken cancellationToken) {
            using SKBitmap bitmap = await DecodeAsync(upload, OriginalMaxSize, cancellationToken);
            return Save(bitmap);
        }

        /// <summary>
        /// Decodes an uploaded image (auto-rotated per EXIF, downscaled to at most <paramref name="maxSize"/> px
        /// during decoding where the codec supports it). Throws <see cref="InvalidDataException"/> for non-images.
        /// </summary>
        public static async Task<SKBitmap> DecodeAsync(Stream upload, int maxSize, CancellationToken cancellationToken) {
            using var buffer = new MemoryStream();
            await upload.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            return DecodeOriented(buffer, maxSize)
                ?? throw new InvalidDataException("The uploaded file is not a supported image.");
        }

        /// <summary>Stores a decoded image (plus its variants) and returns the new image name.</summary>
        public static string Save(SKBitmap bitmap) {
            Directory.CreateDirectory(ImageDirectory);
            string imageName = $"{IdGenerator.GetNumericalId()}.webp";
            string path = Path.Combine(ImageDirectory, imageName);

            using (SKBitmap resized = ResizeToFit(bitmap, OriginalMaxSize)) {
                WriteWebp(resized, path, OriginalQuality);
            }

            // Pre-generate the variants so the wishlist never waits for them.
            foreach (ImageVariant variant in new[] { ImageVariant.Small, ImageVariant.Medium }) {
                using SKBitmap resized = ResizeToFit(bitmap, GetWidth(variant));
                WriteWebp(resized, GetVariantPath(imageName, variant), VariantQuality);
            }

            return imageName;
        }

        /// <summary>
        /// Returns the path of a cached variant, generating it on first request. Falls back to null if the
        /// original cannot be decoded (callers then serve the original).
        /// </summary>
        public static Task<string?> GetVariantPathAsync(string imageName, ImageVariant variant) {
            string? originalPath = GetOriginalPath(imageName);
            if (originalPath == null) {
                return Task.FromResult<string?>(null);
            }

            if (variant == ImageVariant.Original) {
                return Task.FromResult<string?>(originalPath);
            }

            string variantPath = GetVariantPath(imageName, variant);
            if (File.Exists(variantPath)) {
                return Task.FromResult<string?>(variantPath);
            }

            // Make sure concurrent requests for the same variant only generate it once.
            var pending = _PendingVariants.GetOrAdd(variantPath, key => new Lazy<Task<string?>>(() => Task.Run(() => {
                try {
                    using FileStream stream = File.OpenRead(originalPath);
                    using SKBitmap? bitmap = DecodeOriented(stream, GetWidth(variant));
                    if (bitmap == null) {
                        return null;
                    }

                    using SKBitmap resized = ResizeToFit(bitmap, GetWidth(variant));
                    WriteWebp(resized, variantPath, VariantQuality);
                    return variantPath;
                } finally {
                    _PendingVariants.TryRemove(variantPath, out _);
                }
            })));

            return pending.Value;
        }

        /// <summary>Deletes an image and all of its cached variants.</summary>
        public static void Delete(string? imageName) {
            string? originalPath = GetOriginalPath(imageName);
            if (originalPath != null) {
                File.Delete(originalPath);
            }

            if (IsValidName(imageName)) {
                foreach (ImageVariant variant in new[] { ImageVariant.Small, ImageVariant.Medium }) {
                    string variantPath = GetVariantPath(imageName!, variant);
                    if (File.Exists(variantPath)) {
                        File.Delete(variantPath);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes images no wish references any more. Only files older than <paramref name="minAge"/> are
        /// removed, so images uploaded for a wish that is still being edited are kept.
        /// </summary>
        public static int DeleteOrphans(IReadOnlySet<string> referencedNames, TimeSpan minAge) {
            if (!Directory.Exists(ImageDirectory)) {
                return 0;
            }

            int deleted = 0;
            DateTime threshold = DateTime.UtcNow - minAge;
            foreach (string path in Directory.EnumerateFiles(ImageDirectory)) {
                string name = Path.GetFileName(path);
                if (name.StartsWith('.') || referencedNames.Contains(name) || File.GetLastWriteTimeUtc(path) > threshold) {
                    continue;
                }

                Delete(name);
                deleted++;
            }

            return deleted;
        }

        private static string GetVariantPath(string imageName, ImageVariant variant) {
            Directory.CreateDirectory(CacheDirectory);
            return Path.Combine(CacheDirectory, $"{Path.GetFileNameWithoutExtension(imageName)}.{GetWidth(variant)}.webp");
        }

        private static SKBitmap? DecodeOriented(Stream stream, int targetSize) {
            using SKCodec? codec = SKCodec.Create(stream);
            if (codec == null) {
                return null;
            }

            SKImageInfo info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels) {
                throw new InvalidDataException("The image dimensions are not supported.");
            }

            // JPEG and WebP can decode directly at a reduced scale, which is much faster for large photos.
            float scale = Math.Min(1f, (float)targetSize / Math.Max(info.Width, info.Height));
            SKBitmap? bitmap = null;
            if (scale < 1f) {
                SKSizeI scaled = codec.GetScaledDimensions(scale);
                if (scaled.Width > 0 && scaled.Height > 0 && Math.Max(scaled.Width, scaled.Height) >= targetSize) {
                    bitmap = TryDecode(codec, scaled);
                }
            }

            bitmap ??= TryDecode(codec, info.Size);
            if (bitmap == null) {
                return null;
            }

            SKBitmap oriented = ApplyOrientation(bitmap, codec.EncodedOrigin);
            if (!ReferenceEquals(oriented, bitmap)) {
                bitmap.Dispose();
            }

            return oriented;
        }

        private static SKBitmap? TryDecode(SKCodec codec, SKSizeI size) {
            var decodeInfo = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            var bitmap = new SKBitmap(decodeInfo);
            SKCodecResult result = codec.GetPixels(decodeInfo, bitmap.GetPixels());
            if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput) {
                return bitmap;
            }

            bitmap.Dispose();
            return null;
        }

        private static SKBitmap ResizeToFit(SKBitmap source, int maxSize) {
            float scale = Math.Min(1f, (float)maxSize / Math.Max(source.Width, source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            if (width == source.Width && height == source.Height) {
                return source.Copy();
            }

            var info = new SKImageInfo(width, height, source.ColorType, source.AlphaType);
            return source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidOperationException("Unable to resize image.");
        }

        private static void WriteWebp(SKBitmap bitmap, string path, int quality) {
            string tempPath = path + ".tmp";
            using (SKData data = bitmap.Encode(SKEncodedImageFormat.Webp, quality)
                ?? throw new InvalidOperationException("Unable to encode image.")) {
                using FileStream stream = File.Create(tempPath);
                data.SaveTo(stream);
            }

            File.Move(tempPath, path, overwrite: true);
        }

        private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin) {
            if (origin == SKEncodedOrigin.TopLeft) {
                return source;
            }

            bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            int w = source.Width, h = source.Height;
            var rotated = new SKBitmap(swap ? h : w, swap ? w : h, source.ColorType, source.AlphaType);

            using var canvas = new SKCanvas(rotated);
            switch (origin) {
                case SKEncodedOrigin.TopRight:
                    canvas.Translate(w, 0);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.Translate(w, h);
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Translate(0, h);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(h, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(h, w);
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, w);
                    canvas.RotateDegrees(270);
                    break;
            }

            canvas.DrawBitmap(source, 0, 0);
            canvas.Flush();
            return rotated;
        }
    }
}
