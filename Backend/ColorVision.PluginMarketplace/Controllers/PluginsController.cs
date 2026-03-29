using ColorVision.PluginMarketplace.DTOs;
using ColorVision.PluginMarketplace.Services;
using Microsoft.AspNetCore.Mvc;

namespace ColorVision.PluginMarketplace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PluginsController : ControllerBase
    {
        private readonly PluginService _pluginService;

        public PluginsController(PluginService pluginService)
        {
            _pluginService = pluginService;
        }

        /// <summary>
        /// Search and list plugins in the marketplace
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<PluginListItemDto>>> Search([FromQuery] PluginSearchRequest request)
        {
            var result = await _pluginService.SearchPluginsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Get detailed information about a specific plugin
        /// </summary>
        [HttpGet("{pluginId}")]
        public async Task<ActionResult<PluginDetailDto>> GetDetail(string pluginId)
        {
            var detail = await _pluginService.GetPluginDetailAsync(pluginId);
            if (detail == null)
                return NotFound();
            return Ok(detail);
        }

        /// <summary>
        /// Get the latest version string for a plugin (backward-compatible with LATEST_RELEASE)
        /// Returns plain text version string
        /// </summary>
        [HttpGet("{pluginId}/latest-version")]
        public async Task<ActionResult<string>> GetLatestVersion(string pluginId)
        {
            var version = await _pluginService.GetLatestVersionAsync(pluginId);
            if (version == null)
                return NotFound();
            return Content(version, "text/plain");
        }

        /// <summary>
        /// Batch version check for multiple plugins at once
        /// Reduces N+1 HTTP calls to a single request
        /// </summary>
        [HttpPost("batch-version-check")]
        public async Task<ActionResult<List<VersionCheckDto>>> BatchVersionCheck([FromBody] BatchVersionCheckRequest request)
        {
            var results = await _pluginService.BatchVersionCheckAsync(request.PluginIds);
            return Ok(results);
        }

        /// <summary>
        /// Get all available plugin categories
        /// </summary>
        [HttpGet("categories")]
        public async Task<ActionResult<List<string>>> GetCategories()
        {
            var categories = await _pluginService.GetCategoriesAsync();
            return Ok(categories);
        }
    }
}
