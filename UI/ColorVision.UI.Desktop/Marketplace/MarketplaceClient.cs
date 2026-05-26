#pragma warning disable CA1872
using ColorVision.UI.Marketplace;
using ColorVision.Themes;
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

        private static MarketplaceClient? _instance;
        private static readonly object _locker = new();
        public static MarketplaceClient GetInstance()
        {
            lock (_locker) { return _instance ??= new MarketplaceClient(); }
        }

        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

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
                using var response = await _httpClient.GetAsync($"{baseUrl}/api/plugins/categories", cancellationToken);
                return response.IsSuccessStatusCode;
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
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonConvert.DeserializeObject<MarketplaceSearchResult>(json) ?? new MarketplaceSearchResult();
        }

        public async Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return null;

            try
            {
                using var response = await _httpClient.GetAsync($"{baseUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}", cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonConvert.DeserializeObject<MarketplacePluginDetail>(json);
            }
            catch (Exception ex)
            {
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
                using var response = await _httpClient.GetAsync($"{baseUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}/latest-version", cancellationToken);
                response.EnsureSuccessStatusCode();
                string version = await response.Content.ReadAsStringAsync(cancellationToken);
                return version?.Trim();
            }
            catch (Exception ex)
            {
                log.Debug($"GetLatestVersionAsync failed for {pluginId}: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, string?>> BatchVersionCheckAsync(IEnumerable<string> pluginIds, CancellationToken cancellationToken = default)
        {
            string baseUrl = BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
                return new Dictionary<string, string?>();

            try
            {
                var body = new { PluginIds = pluginIds.ToList() };
                var content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{baseUrl}/api/plugins/batch-version-check", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var items = JsonConvert.DeserializeObject<List<BatchVersionItem>>(json) ?? new List<BatchVersionItem>();

                var result = new Dictionary<string, string?>();
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.PluginId))
                        result[item.PluginId] = item.LatestVersion;
                }
                return result;
            }
            catch (Exception ex)
            {
                log.Debug($"BatchVersionCheckAsync failed: {ex.Message}");
                return new Dictionary<string, string?>();
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
                using var response = await _httpClient.GetAsync($"{baseUrl}/api/plugins/categories", cancellationToken);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
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
                using var response = await _httpClient.GetAsync(iconUrl).ConfigureAwait(false);
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
            try
            {
                return !string.IsNullOrWhiteSpace(filePath)
                    && File.Exists(filePath)
                    && !File.Exists(filePath + ".aria2")
                    && new FileInfo(filePath).Length > 0;
            }
            catch
            {
                return false;
            }
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

        private sealed class BatchVersionItem
        {
            [JsonProperty("pluginId")]
            public string PluginId { get; set; } = string.Empty;
            [JsonProperty("latestVersion")]
            public string? LatestVersion { get; set; }
        }
    }
}
