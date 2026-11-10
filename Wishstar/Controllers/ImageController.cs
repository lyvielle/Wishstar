using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Wishstar.Extensions;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    public class ImageController(ILogger<ImageController> logger) : ControllerBase {
        private readonly ILogger<ImageController> _Logger = logger;

        /// <summary>Serves wish images. Sizes: s = 480px, m = 960px, o = original (max. 2048px).</summary>
        [HttpGet("/img/{size}/{fileName}")]
        public Task<IActionResult> Get(string size, string fileName) {
            ImageVariant variant = size switch {
                "s" => ImageVariant.Small,
                "m" => ImageVariant.Medium,
                "o" => ImageVariant.Original,
                _ => (ImageVariant)(-1)
            };

            if (!Enum.IsDefined(variant)) {
                return Task.FromResult<IActionResult>(NotFound());
            }

            return ServeAsync(fileName, variant);
        }

        /// <summary>Legacy URL format used by older versions of the app.</summary>
        [HttpGet("/icon")]
        public Task<IActionResult> GetLegacy([FromQuery(Name = "file")] string? fileName) {
            return ServeAsync(fileName, ImageVariant.Original);
        }

        private async Task<IActionResult> ServeAsync(string? fileName, ImageVariant variant) {
            if (!ImageStore.IsValidName(fileName) || !ImageStore.Exists(fileName)) {
                return NotFound();
            }

            // Images of private wishes (and freshly uploaded, not yet saved ones) are only visible when logged in.
            if (!WishDatabase.Load().IsImagePublic(fileName!) && Request.GetCurrentUser() == null) {
                return NotFound();
            }

            string? path;
            try {
                path = await ImageStore.GetVariantPathAsync(fileName!, variant)
                    ?? await ImageStore.GetVariantPathAsync(fileName!, ImageVariant.Original);
            } catch (Exception ex) {
                _Logger.LogWarning(ex, "Unable to generate {Variant} variant of {FileName}", variant, fileName);
                path = await ImageStore.GetVariantPathAsync(fileName!, ImageVariant.Original);
            }

            if (path == null) {
                return NotFound();
            }

            string contentType = ImageStore.GetContentType(path);
            if (!contentType.StartsWith("image/", StringComparison.Ordinal)) {
                // Never serve legacy non-image uploads (e.g. HTML) from our origin.
                return NotFound();
            }

            // Image names are unique and never reused, so the response can be cached forever.
            // "private" keeps shared proxies from caching images of private wishes.
            Response.Headers.CacheControl = "private, max-age=31536000, immutable";

            var fileInfo = new FileInfo(path);
            return PhysicalFile(path, contentType,
                lastModified: fileInfo.LastWriteTimeUtc,
                entityTag: new EntityTagHeaderValue($"\"{fileInfo.Length:x}-{fileInfo.LastWriteTimeUtc.Ticks:x}\""));
        }
    }
}
