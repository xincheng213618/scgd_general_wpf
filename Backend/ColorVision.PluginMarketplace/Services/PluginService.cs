using ColorVision.PluginMarketplace.Data;
using ColorVision.PluginMarketplace.DTOs;
using ColorVision.PluginMarketplace.Models;
using Microsoft.EntityFrameworkCore;

namespace ColorVision.PluginMarketplace.Services
{
    public class PluginService
    {
        private readonly MarketplaceDbContext _db;
        private readonly StorageService _storage;

        public PluginService(MarketplaceDbContext db, StorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        /// <summary>
        /// Search and list plugins with filtering, sorting, and pagination
        /// </summary>
        public async Task<PagedResult<PluginListItemDto>> SearchPluginsAsync(PluginSearchRequest request)
        {
            var query = _db.Plugins
                .Where(p => p.IsPublished)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(keyword) ||
                    (p.Description != null && p.Description.ToLower().Contains(keyword)) ||
                    p.PluginId.ToLower().Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(p => p.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.Author))
            {
                query = query.Where(p => p.Author == request.Author);
            }

            var totalCount = await query.CountAsync();

            query = request.SortBy?.ToLower() switch
            {
                "downloads" => request.SortOrder == "asc"
                    ? query.OrderBy(p => p.TotalDownloads)
                    : query.OrderByDescending(p => p.TotalDownloads),
                "name" => request.SortOrder == "asc"
                    ? query.OrderBy(p => p.Name)
                    : query.OrderByDescending(p => p.Name),
                "created" => request.SortOrder == "asc"
                    ? query.OrderBy(p => p.CreatedAt)
                    : query.OrderByDescending(p => p.CreatedAt),
                _ => request.SortOrder == "asc"
                    ? query.OrderBy(p => p.UpdatedAt)
                    : query.OrderByDescending(p => p.UpdatedAt),
            };

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var plugins = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Versions)
                .ToListAsync();

            var items = plugins.Select(p => new PluginListItemDto
            {
                PluginId = p.PluginId,
                Name = p.Name,
                Description = p.Description,
                Author = p.Author,
                Category = p.Category,
                IconUrl = p.IconPath != null ? _storage.GetFileUrl(p.IconPath) : null,
                LatestVersion = p.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.Version,
                TotalDownloads = p.TotalDownloads,
                UpdatedAt = p.UpdatedAt,
            }).ToList();

            return new PagedResult<PluginListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            };
        }

        /// <summary>
        /// Get detailed plugin information including all versions
        /// </summary>
        public async Task<PluginDetailDto?> GetPluginDetailAsync(string pluginId)
        {
            var plugin = await _db.Plugins
                .Include(p => p.Versions.OrderByDescending(v => v.CreatedAt))
                .FirstOrDefaultAsync(p => p.PluginId == pluginId && p.IsPublished);

            if (plugin == null)
                return null;

            return new PluginDetailDto
            {
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Description = plugin.Description,
                Author = plugin.Author,
                Url = plugin.Url,
                Category = plugin.Category,
                IconUrl = plugin.IconPath != null ? _storage.GetFileUrl(plugin.IconPath) : null,
                Readme = plugin.Readme,
                TotalDownloads = plugin.TotalDownloads,
                CreatedAt = plugin.CreatedAt,
                UpdatedAt = plugin.UpdatedAt,
                Versions = plugin.Versions.Select(v => new PluginVersionDto
                {
                    Version = v.Version,
                    RequiresVersion = v.RequiresVersion,
                    ChangeLog = v.ChangeLog,
                    FileSize = v.FileSize,
                    FileHash = v.FileHash,
                    DownloadCount = v.DownloadCount,
                    CreatedAt = v.CreatedAt,
                }).ToList(),
            };
        }

        /// <summary>
        /// Get the latest version string for a plugin (backward-compatible with LATEST_RELEASE)
        /// </summary>
        public async Task<string?> GetLatestVersionAsync(string pluginId)
        {
            var latestVersion = await _db.PluginVersions
                .Where(v => v.Plugin.PluginId == pluginId && v.Plugin.IsPublished)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => v.Version)
                .FirstOrDefaultAsync();

            return latestVersion;
        }

        /// <summary>
        /// Batch version check for multiple plugins
        /// </summary>
        public async Task<List<VersionCheckDto>> BatchVersionCheckAsync(List<string> pluginIds)
        {
            var results = await _db.Plugins
                .Where(p => pluginIds.Contains(p.PluginId) && p.IsPublished)
                .Select(p => new VersionCheckDto
                {
                    PluginId = p.PluginId,
                    LatestVersion = p.Versions
                        .OrderByDescending(v => v.CreatedAt)
                        .Select(v => v.Version)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return results;
        }

        /// <summary>
        /// Publish a new plugin or add a new version to an existing plugin
        /// </summary>
        public async Task<PluginVersion> PublishPluginAsync(PluginPublishRequest request, Stream packageStream, string? iconRelativePath = null)
        {
            var plugin = await _db.Plugins.FirstOrDefaultAsync(p => p.PluginId == request.PluginId);

            if (plugin == null)
            {
                plugin = new Plugin
                {
                    PluginId = request.PluginId,
                    Name = request.Name,
                    Description = request.Description,
                    Author = request.Author,
                    Url = request.Url,
                    Category = request.Category,
                };
                _db.Plugins.Add(plugin);
                await _db.SaveChangesAsync();
            }
            else
            {
                plugin.Name = request.Name;
                plugin.Description = request.Description ?? plugin.Description;
                plugin.Author = request.Author ?? plugin.Author;
                plugin.Url = request.Url ?? plugin.Url;
                plugin.Category = request.Category ?? plugin.Category;
                plugin.UpdatedAt = DateTime.UtcNow;
            }

            // Save the package file
            var packagePath = _storage.GetPluginPackagePath(request.PluginId, request.Version);
            var fileHash = await _storage.SaveFileAsync(packageStream, packagePath);
            var fileSize = new FileInfo(_storage.GetAbsolutePath(packagePath)).Length;

            // Check for existing version
            var existingVersion = await _db.PluginVersions
                .FirstOrDefaultAsync(v => v.PluginId == plugin.Id && v.Version == request.Version);

            if (existingVersion != null)
            {
                existingVersion.RequiresVersion = request.RequiresVersion;
                existingVersion.ChangeLog = request.ChangeLog;
                existingVersion.PackagePath = packagePath;
                existingVersion.FileSize = fileSize;
                existingVersion.FileHash = fileHash;
                existingVersion.CreatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return existingVersion;
            }

            var version = new PluginVersion
            {
                PluginId = plugin.Id,
                Version = request.Version,
                RequiresVersion = request.RequiresVersion,
                ChangeLog = request.ChangeLog,
                PackagePath = packagePath,
                FileSize = fileSize,
                FileHash = fileHash,
            };

            _db.PluginVersions.Add(version);
            await _db.SaveChangesAsync();

            return version;
        }

        /// <summary>
        /// Record a download and return the file path
        /// </summary>
        public async Task<(string? FilePath, string? FileName)> GetDownloadPathAsync(
            string pluginId, string version, string? clientIpHash = null, string? clientVersion = null)
        {
            var pluginVersion = await _db.PluginVersions
                .Include(v => v.Plugin)
                .FirstOrDefaultAsync(v =>
                    v.Plugin.PluginId == pluginId &&
                    v.Version == version &&
                    v.Plugin.IsPublished);

            if (pluginVersion?.PackagePath == null)
                return (null, null);

            // Record download
            _db.DownloadRecords.Add(new DownloadRecord
            {
                PluginVersionId = pluginVersion.Id,
                ClientIpHash = clientIpHash,
                ClientVersion = clientVersion,
            });

            pluginVersion.DownloadCount++;
            pluginVersion.Plugin.TotalDownloads++;
            await _db.SaveChangesAsync();

            var absolutePath = _storage.GetAbsolutePath(pluginVersion.PackagePath);
            var fileName = $"{pluginId}-{version}.cvxp";
            return (absolutePath, fileName);
        }

        /// <summary>
        /// Get all distinct categories
        /// </summary>
        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _db.Plugins
                .Where(p => p.IsPublished && p.Category != null)
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
