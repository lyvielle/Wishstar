using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SkiaSharp;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;
using Wishstar.Services.Import;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/import")]
    public class ImportController(ILogger<ImportController> logger) : ControllerBase {
        private const int MaxScreenshotSize = 2560;
        private readonly ILogger<ImportController> _Logger = logger;

        public record class ScreenshotImportResponse(string? Name, string? FullTitle, double? Price, string? Currency, string? FileName);

        /// <summary>
        /// Reads a shop screenshot and suggests name, price and photo for a new wish. Nothing is saved as a wish:
        /// the form is only pre-filled, and the user reviews and completes it.
        /// </summary>
        [HttpPost("screenshot")]
        [RequestSizeLimit(ImageStore.MaxUploadBytes + 64 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = ImageStore.MaxUploadBytes + 64 * 1024)]
        [EnableRateLimiting(RateLimitPolicies.Upload)]
        public async Task<IActionResult> Screenshot([FromForm] IFormFile file, CancellationToken cancellationToken) {
            if (!Request.TryValidateLogin(out _, out User? user) || user == null) {
                return Unauthorized("You must be logged in to import a wish.");
            }

            if (file == null || file.Length == 0) {
                return BadRequest("No screenshot uploaded.");
            }

            if (file.Length > ImageStore.MaxUploadBytes) {
                return BadRequest("The screenshot is too large (max. 25 MB).");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));

            try {
                var stopwatch = Stopwatch.StartNew();
                await using var stream = file.OpenReadStream();
                using SKBitmap screenshot = await ImageStore.DecodeAsync(stream, MaxScreenshotSize, timeout.Token);

                ScreenshotAnalysis analysis = await ScreenshotAnalyzer.AnalyzeAsync(screenshot, timeout.Token);

                string? fileName = null;
                if (analysis.PhotoRect is SKRectI rect) {
                    using SKBitmap photo = PhotoLocator.Crop(screenshot, rect);
                    fileName = ImageStore.Save(photo);
                }

                _Logger.LogInformation("{UserName} imported a screenshot in {Elapsed} ms (name: {HasName}, price: {HasPrice}, photo: {HasPhoto})",
                    user.Username, stopwatch.ElapsedMilliseconds, analysis.Name != null, analysis.Price != null, fileName != null);

                return Ok(new ScreenshotImportResponse(analysis.Name, analysis.FullTitle, analysis.Price, analysis.Currency?.ToString(), fileName));
            } catch (InvalidDataException ex) {
                return BadRequest(ex.Message);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Reading the screenshot took too long. Please try again.");
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                _Logger.LogError(ex, "Screenshot import failed");
                return StatusCode(StatusCodes.Status500InternalServerError, "The screenshot could not be read.");
            }
        }
    }
}
