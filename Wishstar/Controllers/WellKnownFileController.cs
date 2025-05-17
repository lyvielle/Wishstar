using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route(".well-known")]
    public class WellKnownFileController(ILogger<WellKnownFileController> logger) : ControllerBase {
        private readonly ILogger<WellKnownFileController> _Logger = logger;

        [Route("discord")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult GetDiscord() {
            string fullBasePath = Path.GetFullPath(AppConfig.WellKnownBasePath);
            string discordFilePath = Path.Combine(fullBasePath, "discord");
            string indexFilePath = Path.Combine(fullBasePath, ".virtual");

            string logId = Random.Shared.Next(0, 100000).ToString("D5");

            _Logger.LogInformation("[{logID}] Requested /.well-known/discord for domain {domainName}", logId, Request.GetDomain());

            if (System.IO.File.Exists(indexFilePath)) {
                foreach (DomainIndex index in DomainIndexFile.Load(indexFilePath).Indices) {
                    if (index.Domain.Equals(Request.GetDomain(), StringComparison.InvariantCultureIgnoreCase) && index.VirtualFileName == "discord") {
                        discordFilePath = Path.Combine(fullBasePath, index.PhysicalFileName);
                        _Logger.LogInformation("[{logID}] Redirecting to {filePath}", logId, discordFilePath);
                        break;
                    }
                }
            }

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "-1";

            return PhysicalFile(discordFilePath, "application/octet-stream");
        }
    }
}