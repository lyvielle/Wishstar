using Microsoft.AspNetCore.Mvc;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/icon")]
    public class ImageResolveController(ILogger<ImageResolveController> logger) : ControllerBase {
        private readonly ILogger<ImageResolveController> _Logger = logger;

        [HttpGet]
        public IActionResult Get([FromQuery(Name = "file")] string fileName) {
            try {
                string? physicalFilePath = ImageResolver.GetImagePath(fileName);
                if (string.IsNullOrWhiteSpace(physicalFilePath)) {
                    _Logger.LogWarning("Image not found: {FileName}", fileName);
                    return NotFound();
                }

                string fullBasePath = Path.GetFullPath(AppConfig.ImageBasePath);
                physicalFilePath = Path.GetFullPath(physicalFilePath);

                if (!physicalFilePath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase)) {
                    _Logger.LogWarning("Image path is outside of base path: {FileName}", fileName);
                    return NotFound();
                }

                return PhysicalFile(physicalFilePath, MimeMapper.GetMimeType(Path.GetExtension(physicalFilePath)), fileDownloadName: fileName);
            } catch (Exception) {
                _Logger.LogError("Error resolving image: {FileName}", fileName);
                return NotFound();
            }
        }
    }
}