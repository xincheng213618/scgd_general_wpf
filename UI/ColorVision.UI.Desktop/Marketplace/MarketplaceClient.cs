#pragma warning disable CA1872
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using ColorVision.Themes;
using ColorVision.Update;
using log4net;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Desktop.Marketplace
{
    /// <summary>
    /// HTTP client implementation of IMarketplaceService for the Python Flask marketplace backend.
    /// Falls back to legacy file-server when the marketplace API is unavailable.
    /// </summary>
    public class MarketplaceClient : IMarketplaceService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceClient));
        private static readonly ConcurrentDictionary<string, Lazy<Task<ImageSource?>>> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, CachedPluginDetail> _pluginDetailCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan UpdateMetadataCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan UpdateRequestTimeout = TimeSpan.FromSeconds(6);

        private static MarketplaceClient? _instance;
        private static readonly object _locker = new();
        public static MarketplaceClient GetInstance()
        {
            lock (_locker) { return _instance ??= new MarketplaceClient(); }
        }

        private readonly SemaphoreSlim _batchVersionSemaphore = new(1, 1);
        private string? _batchVersionCacheKey;
        private Dictionary<string, string?>? _batchVersionCache;
        private DateTimeOffset _batchVersionCachedAt = DateTimeOffset.MinValue;

        public MarketplaceClient()
        {
        }

        private static string BaseUrl => MarketplaceConfig.ServiceBaseUrl;

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return false;
            try
            {
                using var response = await UpdateHttpClientProvider.GetClient().GetAsync($"{baseUrl}/api/plugins/categories", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public async Task<MarketplaceSearchResult> SearchPluginsAsync(MarketplaceSearchRequest request, CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Marketplace service base URL is not configured.");

            string url = $"{baseUrl}/api/plugins?Keyword={Uri.EscapeDataString(request.Keyword ?? "")}&Category={Uri.EscapeDataString(request.Category ?? "")}&Author={Uri.EscapeDataString(request.Author ?? "")}&SortBy={Uri.EscapeDataString(request.SortBy ?? "updated")}&SortOrder={Uri.EscapeDataString(request.SortOrder ?? "desc")}&Page={request.Page}&PageSize={request.PageSize}";
            using var response = await UpdateHttpClientProvider.GetClient().GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonConvert.DeserializeObject<MarketplaceSearchResult>(json) ?? new MarketplaceSearchResult();
        }

        public Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            return GetPluginDetailAsync(pluginId, forceRefresh: false, cancellationToken);
        }

        public async Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return null;

            if (!forceRefresh && TryGetCachedPluginDetail(pluginId, requireFresh: true, out MarketplacePluginDetail? cachedDetail))
                return cachedDetail;

            try
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(UpdateRequestTimeout);
                using var response = await UpdateHttpClientProvider.GetClient().GetAsync($"{baseUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}", timeoutSource.Token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                MarketplacePluginDetail? detail = JsonConvert.DeserializeObject<MarketplacePluginDetail>(json);
                if (detail != null)
                    _pluginDetailCache[pluginId] = new CachedPluginDetail(detail, DateTimeOffset.UtcNow);
                return detail;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                if (TryGetCachedPluginDetail(pluginId, requireFresh: false, out MarketplacePluginDetail? staleDetail))
                {
                    log.Warn($"Plugin detail request timed out for {pluginId}; using the last successful response.");
                    return staleDetail;
                }

                log.Warn($"Plugin detail request timed out for {pluginId}: {ex.GetBaseException().Message}");
                return null;
            }
            catch (Exception ex)
            {
                if (TryGetCachedPluginDetail(pluginId, requireFresh: false, out MarketplacePluginDetail? staleDetail))
                {
                    log.Warn($"GetPluginDetailAsync failed for {pluginId}; using the last successful response: {ex.Message}");
                    return staleDetail;
                }

                log.Debug($"GetPluginDetailAsync failed for {pluginId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return null;

            try
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(UpdateRequestTimeout);
                using var response = await UpdateHttpClientProvider.GetClient().GetAsync($"{baseUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}/latest-version", timeoutSource.Token);
                response.EnsureSuccessStatusCode();
                string version = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                return version?.Trim();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Debug($"GetLatestVersionAsync failed for {pluginId}: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, string?>> BatchVersionCheckAsync(
            IEnumerable<string> pluginIds,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Marketplace service address is not configured.");

            List<string> normalizedPluginIds = pluginIds
                .Where(pluginId => !string.IsNullOrWhiteSpace(pluginId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pluginId => pluginId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedPluginIds.Count == 0)
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            string cacheKey = string.Join("\n", normalizedPluginIds);
            await _batchVersionSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && TryGetBatchVersionCache(cacheKey, requireFresh: true, out Dictionary<string, string?>? cachedVersions))
                    return cachedVersions;

                string requestJson = JsonConvert.SerializeObject(new { PluginIds = normalizedPluginIds });
                try
                {
                    using HttpResponseMessage response = await UpdateHttpClientProvider.SendWithTransientRetryAsync(
                        () => new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/plugins/batch-version-check")
                        {
                            Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json"),
                        },
                        UpdateRequestTimeout,
                        cancellationToken);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync(cancellationToken);
                    List<BatchVersionItem> items = JsonConvert.DeserializeObject<List<BatchVersionItem>>(json) ?? new List<BatchVersionItem>();
                    Dictionary<string, string?> versions = new(StringComparer.OrdinalIgnoreCase);
                    foreach (BatchVersionItem item in items)
                    {
                        if (!string.IsNullOrWhiteSpace(item.PluginId))
                            versions[item.PluginId] = item.LatestVersion;
                    }

                    _batchVersionCacheKey = cacheKey;
                    _batchVersionCache = versions;
                    _batchVersionCachedAt = DateTimeOffset.UtcNow;
                    return CloneVersions(versions);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    if (TryGetBatchVersionCache(cacheKey, requireFresh: false, out Dictionary<string, string?>? staleVersions))
                    {
                        log.Warn("Plugin batch-version request timed out; using the last successful response.");
                        return staleVersions;
                    }

                    throw new TimeoutException("Timed out fetching plugin update metadata.", ex);
                }
                catch (Exception ex)
                {
                    if (TryGetBatchVersionCache(cacheKey, requireFresh: false, out Dictionary<string, string?>? staleVersions))
                    {
                        log.Warn($"Plugin batch-version request failed; using the last successful response: {ex.Message}");
                        return staleVersions;
                    }

                    throw;
                }
            }
            finally
            {
                _batchVersionSemaphore.Release();
            }
        }

        public string GetDownloadUrl(string pluginId, string version)
        {
            return $"{BaseUrl}/api/packages/{Uri.EscapeDataString(pluginId)}/{Uri.EscapeDataString(version)}";
        }

        public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return new List<string>();

            try
            {
                using var response = await UpdateHttpClientProvider.GetClient().GetAsync($"{baseUrl}/api/plugins/categories", cancellationToken);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Debug($"GetCategoriesAsync failed: {ex.Message}");
                return new List<string>();
            }
        }

        public static ImageSource GetDefaultPluginIcon()
        {
            var iconName = ThemeManager.Current.CurrentUITheme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico";
            return new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{iconName}"));
        }

        public static async Task<ImageSource?> GetPluginIconAsync(string? iconUrl)
        {
            if (string.IsNullOrWhiteSpace(iconUrl))
                return GetDefaultPluginIcon();

            var lazyTask = _iconCache.GetOrAdd(iconUrl, url => new Lazy<Task<ImageSource?>>(() => LoadPluginIconCoreAsync(url)));

            try
            {
                return await lazyTask.Value.ConfigureAwait(false) ?? GetDefaultPluginIcon();
            }
            catch (Exception ex)
            {
                log.Debug($"GetPluginIconAsync failed for {iconUrl}: {ex.Message}");
                _iconCache.TryRemove(iconUrl, out _);
                return GetDefaultPluginIcon();
            }
        }

        private static async Task<ImageSource?> LoadPluginIconCoreAsync(string iconUrl)
        {
            try
            {
                using var response = await UpdateHttpClientProvider.GetClient().GetAsync(iconUrl).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                byte[] imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (imageBytes.Length == 0)
                    return GetDefaultPluginIcon();

                using var stream = new MemoryStream(imageBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                log.Debug($"LoadPluginIconCoreAsync failed for {iconUrl}: {ex.Message}");
                return GetDefaultPluginIcon();
            }
        }

        // --- Hash verification helpers ---

        /// <summary>
        /// Compute SHA256 hash of a local file.
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Verify a downloaded file's SHA256 hash matches the expected hash.
        /// Returns true if hash matches or if expectedHash is null/empty (skip verification).
        /// </summary>
        public static bool VerifyFileHash(string filePath, string? expectedHash)
        {
            if (!IsDownloadedFileUsable(filePath))
                return false;

            if (string.IsNullOrEmpty(expectedHash)) return true;
            string actualHash = ComputeFileHash(filePath);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDownloadedFileUsable(string? filePath)
        {
            return PluginUpdater.IsPluginPackageFileReady(filePath);
        }

        /// <summary>
        /// Check if a file already exists at the expected download path and has the correct hash.
        /// Returns the path if file exists and matches, null otherwise.
        /// </summary>
        public static string? GetExistingFileIfValid(string downloadDir, string pluginId, string version, string? expectedHash)
        {
            string expectedFileName = $"{pluginId}-{version}.cvxp";
            string filePath = Path.Combine(downloadDir, expectedFileName);
            if (!IsDownloadedFileUsable(filePath)) return null;
            if (string.IsNullOrEmpty(expectedHash)) return filePath; // No hash to check, file exists
            return VerifyFileHash(filePath, expectedHash) ? filePath : null;
        }

        private bool TryGetBatchVersionCache(string cacheKey, bool requireFresh, out Dictionary<string, string?> versions)
        {
            if (_batchVersionCache != null
                && string.Equals(_batchVersionCacheKey, cacheKey, StringComparison.OrdinalIgnoreCase)
                && (!requireFresh || DateTimeOffset.UtcNow - _batchVersionCachedAt <= UpdateMetadataCacheDuration))
            {
                versions = CloneVersions(_batchVersionCache);
                return true;
            }

            versions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        private static Dictionary<string, string?> CloneVersions(Dictionary<string, string?> versions)
        {
            return new Dictionary<string, string?>(versions, StringComparer.OrdinalIgnoreCase);
        }

        private static bool TryGetCachedPluginDetail(string pluginId, bool requireFresh, out MarketplacePluginDetail? detail)
        {
            if (_pluginDetailCache.TryGetValue(pluginId, out CachedPluginDetail? cached)
                && (!requireFresh || DateTimeOffset.UtcNow - cached.CachedAt <= UpdateMetadataCacheDuration))
            {
                detail = cached.Detail;
                return true;
            }

            detail = null;
            return false;
        }

        private sealed record CachedPluginDetail(MarketplacePluginDetail Detail, DateTimeOffset CachedAt);

        private sealed class BatchVersionItem
        {
            [JsonProperty("pluginId")]
            public string PluginId { get; set; } = string.Empty;

            [JsonProperty("latestVersion")]
            public string? LatestVersion { get; set; }
        }

    }
}
