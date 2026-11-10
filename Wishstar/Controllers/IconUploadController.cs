using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/icon")]
    public class IconUploadController(ILogger<IconUploadController> logger) : ControllerBase {
        private readonly ILogger<IconUploadController> _Logger = logger;

        [HttpPost("upload")]
        [RequestSizeLimit(ImageStore.MaxUploadBytes + 64 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = ImageStore.MaxUploadBytes + 64 * 1024)]
        [EnableRateLimiting(RateLimitPolicies.Upload)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken cancellationToken) {
            if (!Request.TryValidateLogin(out _, out User? user) || user == null) {
                return Unauthorized("You must be logged in to upload an image.");
            }

            if (file == null || file.Length == 0) {
                return BadRequest("No file uploaded.");
            }

            if (file.Length > ImageStore.MaxUploadBytes) {
                return BadRequest("The image is too large (max. 25 MB).");
            }

            try {
                await using var stream = file.OpenReadStream();
                string imageName = await ImageStore.SaveUploadAsync(stream, cancellationToken);
                _Logger.LogInformation("{UserName} uploaded image {ImageName} ({Bytes} bytes)", user.Username, imageName, file.Length);

                return Ok(new { fileName = imageName, url = ImageStore.GetUrl(imageName, ImageVariant.Medium) });
            } catch (InvalidDataException ex) {
                return BadRequest(ex.Message);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                _Logger.LogError(ex, "Unable to process uploaded image");
                return StatusCode(StatusCodes.Status500InternalServerError, "The image could not be processed.");
            }
        }

        [HttpPost("delete/{fileName}")]
        public IActionResult Delete(string fileName) {
            if (!Request.TryValidateLogin(out _, out User? user) || user == null) {
                return Unauthorized("You must be logged in to delete an image.");
            }

            if (!ImageStore.Exists(fileName)) {
                return NotFound("File not found.");
            }

            // Images that still belong to a wish are removed together with the wish instead.
            if (WishDatabase.Load().IsImageReferenced(fileName)) {
                return Conflict("The image is still used by a wish.");
            }

            ImageStore.Delete(fileName);
            _Logger.LogInformation("{UserName} deleted image {FileName}", user.Username, fileName);
            return Ok(new { message = "Image deleted successfully." });
        }
    }
}
