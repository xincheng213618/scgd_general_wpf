using ColorVision.PluginMarketplace.DTOs;
using ColorVision.PluginMarketplace.Services;
using Microsoft.AspNetCore.Mvc;

namespace ColorVision.PluginMarketplace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PackagesController : ControllerBase
    {
        private readonly PluginService _pluginService;
        private readonly ILogger<PackagesController> _logger;

        public PackagesController(PluginService pluginService, ILogger<PackagesController> logger)
        {
            _pluginService = pluginService;
            _logger = logger;
        }

        /// <summary>
        /// Download a specific plugin version package (.cvxp)
        /// </summary>
        [HttpGet("{pluginId}/{version}")]
        public async Task<IActionResult> Download(string pluginId, string version)
        {
            var clientIpHash = GetClientIpHash();
            var clientVersion = Request.Headers["X-Client-Version"].FirstOrDefault();

            var (filePath, fileName) = await _pluginService.GetDownloadPathAsync(
                pluginId, version, clientIpHash, clientVersion);

            if (filePath == null || !System.IO.File.Exists(filePath))
                return NotFound();

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, "application/octet-stream", fileName);
        }

        /// <summary>
        /// Publish a new plugin version (upload .cvxp package)
        /// </summary>
        [HttpPost("publish")]
        public async Task<IActionResult> Publish(
            [FromForm] PluginPublishRequest request,
            [FromForm] IFormFile package,
            [FromForm] IFormFile? icon = null)
        {
            if (package == null || package.Length == 0)
                return BadRequest("Package file is required.");

            if (string.IsNullOrWhiteSpace(request.PluginId))
                return BadRequest("PluginId is required.");

            if (string.IsNullOrWhiteSpace(request.Version))
                return BadRequest("Version is required.");

            _logger.LogInformation("Publishing plugin {PluginId} version {Version}", request.PluginId, request.Version);

            string? iconRelPath = null;
            if (icon != null && icon.Length > 0)
            {
                var storage = HttpContext.RequestServices.GetRequiredService<StorageService>();
                iconRelPath = storage.GetPluginIconPath(request.PluginId);
                using var iconStream = icon.OpenReadStream();
                await storage.SaveFileAsync(iconStream, iconRelPath);
            }

            using var packageStream = package.OpenReadStream();
            var pluginVersion = await _pluginService.PublishPluginAsync(request, packageStream, iconRelPath);

            return CreatedAtAction(nameof(Download),
                new { pluginId = request.PluginId, version = pluginVersion.Version },
                new { pluginId = request.PluginId, version = pluginVersion.Version });
        }

        private string? GetClientIpHash()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (ip == null)
                return null;

            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(ip));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }
    }
}
