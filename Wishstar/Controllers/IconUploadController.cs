using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/icon")]
    public class IconUploadController(ILogger<IconUploadController> logger) : ControllerBase {
        private readonly ILogger<IconUploadController> _Logger = logger;

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file) {
            try {
                if(!Request.TryValidateLogin(out IActionResult? errorResult, out User? user) || user == null) {
                    return Unauthorized("You must be logged in to upload an icon.");
                }

                if(file == null || file.Length == 0) {
                    return BadRequest("No file uploaded.");
                }

                string fileName = Path.GetFileName(file.FileName);
                string fullBasePath = Path.GetFullPath(AppConfig.ImageBasePath);
                string imgDirectory = Path.Combine(fullBasePath, "img");
                Directory.CreateDirectory(imgDirectory);
                string physicalFilePath = Path.GetFullPath(Path.Combine(imgDirectory, fileName));

                if(!physicalFilePath.StartsWith(imgDirectory)) {
                    return BadRequest("Invalid path");
                }

                _Logger.LogInformation("{userName} uploading icon: {FileName}", user.Username, fileName);

                using var stream = new FileStream(physicalFilePath, FileMode.Create);
                await file.CopyToAsync(stream);

                return Ok(new { url = ImageResolver.GetImageUrl(fileName) });
            } catch(Exception ex) {
                _Logger.LogError(ex, "Unable to upload icon");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the icon.");
            }
        }

        [HttpPost]
        [Route("delete/{fileName}")]
        public IActionResult Delete(string fileName) {
            try {
                if(!Request.TryValidateLogin(out IActionResult? errorResult, out User? user) || user == null) {
                    return Unauthorized("You must be logged in to delete an icon.");
                }

                if(string.IsNullOrWhiteSpace(fileName)) {
                    return BadRequest("File name is required.");
                }

                string fullBasePath = Path.GetFullPath(AppConfig.ImageBasePath);
                string imgDirectory = Path.Combine(fullBasePath, "img");
                string physicalFilePath = Path.GetFullPath(Path.Combine(imgDirectory, fileName));

                if(!physicalFilePath.StartsWith(imgDirectory)) {
                    return BadRequest("Invalid path");
                }

                if(!System.IO.File.Exists(physicalFilePath)) {
                    return NotFound("File not found.");
                }

                System.IO.File.Delete(physicalFilePath);
                _Logger.LogInformation("{userName} deleted icon: {FileName}", user.Username, fileName);

                return Ok(new { message = "Icon deleted successfully." });
            } catch(Exception ex) {
                _Logger.LogError(ex, "Unable to delete icon");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the icon.");
            }
        }
    }
}