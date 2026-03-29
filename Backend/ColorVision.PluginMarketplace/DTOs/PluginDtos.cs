namespace ColorVision.PluginMarketplace.DTOs
{
    /// <summary>
    /// Plugin summary for marketplace listing
    /// </summary>
    public class PluginListItemDto
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
    /// Detailed plugin information
    /// </summary>
    public class PluginDetailDto
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
        public List<PluginVersionDto> Versions { get; set; } = new();
    }

    public class PluginVersionDto
    {
        public string Version { get; set; } = string.Empty;
        public string? RequiresVersion { get; set; }
        public string? ChangeLog { get; set; }
        public long FileSize { get; set; }
        public string? FileHash { get; set; }
        public long DownloadCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Lightweight version check response (backward-compatible with LATEST_RELEASE)
    /// </summary>
    public class VersionCheckDto
    {
        public string PluginId { get; set; } = string.Empty;
        public string? LatestVersion { get; set; }
    }

    /// <summary>
    /// Batch version check request
    /// </summary>
    public class BatchVersionCheckRequest
    {
        public List<string> PluginIds { get; set; } = new();
    }

    /// <summary>
    /// Search/filter request
    /// </summary>
    public class PluginSearchRequest
    {
        public string? Keyword { get; set; }
        public string? Category { get; set; }
        public string? Author { get; set; }
        public string? SortBy { get; set; } = "updated"; // "updated", "downloads", "name"
        public string? SortOrder { get; set; } = "desc"; // "asc", "desc"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Paged result wrapper
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// Plugin publish/update request
    /// </summary>
    public class PluginPublishRequest
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Url { get; set; }
        public string? Category { get; set; }
        public string Version { get; set; } = string.Empty;
        public string? RequiresVersion { get; set; }
        public string? ChangeLog { get; set; }
    }
}
