namespace ColorVision.UI.Marketplace
{
    /// <summary>
    /// Plugin marketplace service interface for querying and downloading plugins from the backend API.
    /// Implementations should handle HTTP communication with the marketplace backend.
    /// </summary>
    public interface IMarketplaceService
    {
        /// <summary>
        /// Search for plugins in the marketplace with optional filtering and pagination.
        /// </summary>
        Task<MarketplaceSearchResult> SearchPluginsAsync(MarketplaceSearchRequest request);

        /// <summary>
        /// Get detailed information about a specific plugin.
        /// </summary>
        Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId);

        /// <summary>
        /// Get the latest version string for a specific plugin.
        /// This is the marketplace equivalent of checking LATEST_RELEASE.
        /// </summary>
        Task<string?> GetLatestVersionAsync(string pluginId);

        /// <summary>
        /// Batch check versions for multiple plugins at once.
        /// Significantly reduces network overhead compared to checking one at a time.
        /// </summary>
        Task<Dictionary<string, string?>> BatchVersionCheckAsync(IEnumerable<string> pluginIds);

        /// <summary>
        /// Get the download URL for a specific plugin version.
        /// </summary>
        string GetDownloadUrl(string pluginId, string version);

        /// <summary>
        /// Get available plugin categories.
        /// </summary>
        Task<List<string>> GetCategoriesAsync();

        /// <summary>
        /// Whether the marketplace service is available (backend is reachable).
        /// </summary>
        Task<bool> IsAvailableAsync();
    }

    /// <summary>
    /// Search request parameters for the marketplace
    /// </summary>
    public class MarketplaceSearchRequest
    {
        public string? Keyword { get; set; }
        public string? Category { get; set; }
        public string? Author { get; set; }
        public string? SortBy { get; set; } = "updated";
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Paged search result from the marketplace
    /// </summary>
    public class MarketplaceSearchResult
    {
        public List<MarketplacePluginSummary> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Summary information for a plugin in marketplace listings
    /// </summary>
    public class MarketplacePluginSummary
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? IconUrl { get; set; }
        public string? LatestVersion { get; set; }
        public long TotalDownloads { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Detailed plugin information from the marketplace
    /// </summary>
    public class MarketplacePluginDetail
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Url { get; set; }
        public string? Category { get; set; }
        public string? IconUrl { get; set; }
        public string? Readme { get; set; }
        public long TotalDownloads { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<MarketplacePluginVersionInfo> Versions { get; set; } = new();
    }

    /// <summary>
    /// Version information for a plugin in the marketplace
    /// </summary>
    public class MarketplacePluginVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string? RequiresVersion { get; set; }
        public string? ChangeLog { get; set; }
        public long FileSize { get; set; }
        public string? FileHash { get; set; }
        public long DownloadCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
