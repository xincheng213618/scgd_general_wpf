using ColorVision.UI.Marketplace;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Update
{
    /// <summary>
    /// Serves update requests from an offline update folder produced by <c>Scripts/export_offline_updates.py</c>, so a
    /// local, USB or UNC folder can be listed as an update source next to HTTP services. The update service API is mapped
    /// onto the folder layout below; every file must be listed with its SHA-256 in <see cref="IndexFileName"/>.
    /// <code>
    /// colorvision-updates.json                        index: app/plugin versions and file hashes
    /// app/latest-version.json                         GET api/app/latest-version
    /// app/updates/ColorVision-Update-[v].cvx          GET api/app/updates/{v}/download (names match AutoUpdater's cache names)
    /// app/releases/ColorVision-v.exe                  GET api/app/releases/{v}/download
    /// plugins/{id}/update.json                        GET api/plugins/{id}[?view=update]
    /// plugins/{id}/{id}-{v}.cvxp                      GET api/packages/{id}/{v}
    /// </code>
    /// Plugin latest versions, batch version checks, categories and search are answered from the index.
    /// </summary>
    public static class OfflineUpdateFolder
    {
        public const string IndexFileName = "colorvision-updates.json";
        private const long MaxMetadataBytes = 16 * 1024 * 1024;
        private static readonly ILog log = LogManager.GetLogger(typeof(OfflineUpdateFolder));

        public sealed class FolderIndex
        {
            public int Format { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? AppLatestVersion { get; set; }
            public Dictionary<string, string> Plugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        internal enum RouteKind { File, PluginLatestVersion, BatchVersionCheck, Categories, Search }

        internal readonly record struct Route(RouteKind Kind, string? RelativeFile = null, string? PluginId = null);

        /// <summary>Maps an update API path (relative to the source, without query) onto the folder layout.</summary>
        internal static Route? MapRoute(HttpMethod method, string apiPath)
        {
            string[] s = apiPath.Trim('/').Split('/');
            if (s.Any(segment => !IsSafeSegment(segment)))
                return null;

            if (method == HttpMethod.Post)
                return s is ["api", "plugins", "batch-version-check"] ? new Route(RouteKind.BatchVersionCheck) : null;
            if (method != HttpMethod.Get && method != HttpMethod.Head)
                return null;

            return s switch
            {
                ["api", "app", "latest-version"] => new Route(RouteKind.File, "app/latest-version.json"),
                ["api", "app", "updates", var v, "download"] => new Route(RouteKind.File, $"app/updates/ColorVision-Update-[{Version.Parse(v)}].cvx"),
                ["api", "app", "releases", var v, "download"] => new Route(RouteKind.File, $"app/releases/ColorVision-{Version.Parse(v)}.exe"),
                ["api", "plugins"] => new Route(RouteKind.Search),
                ["api", "plugins", "categories"] => new Route(RouteKind.Categories),
                ["api", "plugins", var id, "latest-version"] => new Route(RouteKind.PluginLatestVersion, PluginId: id),
                ["api", "plugins", var id] => new Route(RouteKind.File, $"plugins/{id}/update.json", id),
                ["api", "packages", var id, var v] => new Route(RouteKind.File, $"plugins/{id}/{id}-{v}.cvxp", id),
                _ => null,
            };
        }

        private static bool IsSafeSegment(string segment)
        {
            return segment.Length > 0 && segment != "." && segment != ".." && segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary>
        /// Splits a request URL into the configured folder source it belongs to and the API path below it.
        /// Returns false when the URL is not a <c>file:</c> URL of a configured folder source.
        /// </summary>
        internal static bool TryLocate(string url, out string rootDirectory, out string apiPath)
        {
            rootDirectory = string.Empty;
            apiPath = string.Empty;
            if (!MarketplaceConfig.IsFolderSource(url))
                return false;

            int query = url.IndexOf('?');
            string withoutQuery = query >= 0 ? url[..query] : url;
            foreach (string source in MarketplaceConfig.ServiceBaseUrls.Where(MarketplaceConfig.IsFolderSource))
            {
                if (!withoutQuery.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase))
                    continue;
                rootDirectory = new Uri(source).LocalPath;
                apiPath = Uri.UnescapeDataString(withoutQuery[(source.Length + 1)..]);
                return true;
            }

            return false;
        }

        internal static FolderIndex? ReadIndex(string rootDirectory)
        {
            string path = Path.Combine(rootDirectory, IndexFileName);
            if (!File.Exists(path))
                return null;
            FolderIndex? index = JsonConvert.DeserializeObject<FolderIndex>(File.ReadAllText(path, Encoding.UTF8));
            return index?.Format == 1 ? index : null;
        }

        /// <summary>
        /// Resolves a package download URL to the file to copy and its expected SHA-256. Returns false for non-folder URLs
        /// (the caller downloads normally); returns true with a null path and an error when the folder cannot provide it.
        /// </summary>
        public static bool TryResolveDownload(string url, out string? sourcePath, out string? sha256, out string? error)
        {
            sourcePath = null;
            sha256 = null;
            error = null;
            if (!MarketplaceConfig.IsFolderSource(url))
                return false;

            try
            {
                if (!TryLocate(url, out string root, out string apiPath))
                    error = "The download address is not inside a configured offline update folder.";
                else if (ReadIndex(root) is not { } index)
                    error = $"The offline update folder has no valid {IndexFileName}: {root}";
                else if (MapRoute(HttpMethod.Get, apiPath) is not { Kind: RouteKind.File } route)
                    error = $"The offline update folder does not provide {apiPath}.";
                else if (!TryGetListedFile(root, index, route.RelativeFile!, out sourcePath, out sha256))
                    error = $"The offline update folder does not contain {route.RelativeFile}.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or ArgumentException)
            {
                sourcePath = null;
                error = $"Unable to read the offline update folder: {ex.Message}";
            }

            return true;
        }

        private static bool TryGetListedFile(string root, FolderIndex index, string relativeFile, out string? path, out string? sha256)
        {
            path = null;
            if (!index.Files.TryGetValue(relativeFile, out sha256) || string.IsNullOrWhiteSpace(sha256))
                return false;
            string candidate = Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidate))
                return false;
            path = candidate;
            return true;
        }

        /// <summary>Answers an update API request from the folder. Missing or invalid indexes answer 503 so source probing skips the folder.</summary>
        internal static async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.OriginalString;
            if (!TryLocate(url, out string root, out string apiPath))
                return Text(HttpStatusCode.NotFound, "Not a configured offline update folder.", request);

            try
            {
                FolderIndex? index = ReadIndex(root);
                if (index == null)
                    return Text(HttpStatusCode.ServiceUnavailable, $"Offline update folder index {IndexFileName} is missing or unsupported.", request);

                Route? route = MapRoute(request.Method, apiPath);
                switch (route?.Kind)
                {
                    case RouteKind.File:
                        return ServeFile(root, index, route.Value.RelativeFile!, request);
                    case RouteKind.PluginLatestVersion:
                        return index.Plugins.TryGetValue(route.Value.PluginId!, out string? latest)
                            ? Text(HttpStatusCode.OK, latest, request)
                            : Text(HttpStatusCode.NotFound, "Plugin not found", request);
                    case RouteKind.BatchVersionCheck:
                        string body = request.Content == null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        JObject payload = JObject.Parse(body);
                        JToken? ids = payload["PluginIds"] ?? payload["pluginIds"];
                        var items = (ids as JArray ?? new JArray()).Select(token => token.ToString()).Select(id => index.Plugins.TryGetValue(id, out string? version)
                            ? new { pluginId = id, latestVersion = (string?)version, status = "ok" }
                            : new { pluginId = id, latestVersion = (string?)null, status = "missing" });
                        return Json(JsonConvert.SerializeObject(items), request);
                    case RouteKind.Categories:
                        return Json("[]", request);
                    case RouteKind.Search:
                        return Json(JsonConvert.SerializeObject(new MarketplaceSearchResult { Page = 1, PageSize = 0 }), request);
                    default:
                        return Text(HttpStatusCode.NotFound, $"The offline update folder does not provide {apiPath}.", request);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or ArgumentException)
            {
                log.Warn($"Failed to answer {url} from the offline update folder: {ex.Message}");
                return Text(HttpStatusCode.ServiceUnavailable, ex.Message, request);
            }
        }

        private static HttpResponseMessage ServeFile(string root, FolderIndex index, string relativeFile, HttpRequestMessage request)
        {
            if (!TryGetListedFile(root, index, relativeFile, out string? path, out string? sha256))
                return Text(HttpStatusCode.NotFound, $"{relativeFile} is not in the offline update folder.", request);

            // Metadata is small and verified here; packages are verified by the download path while copying.
            if (relativeFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (new FileInfo(path!).Length > MaxMetadataBytes)
                    return Text(HttpStatusCode.BadGateway, $"{relativeFile} is too large.", request);
                byte[] bytes = File.ReadAllBytes(path!);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                {
                    log.Warn($"Offline update folder file {relativeFile} does not match its SHA-256 in {IndexFileName}.");
                    return Text(HttpStatusCode.BadGateway, $"{relativeFile} failed SHA-256 verification.", request);
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(bytes) { Headers = { { "Content-Type", "application/json; charset=utf-8" } } } };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StreamContent(File.OpenRead(path!)) };
        }

        private static HttpResponseMessage Json(string json, HttpRequestMessage request) => new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        private static HttpResponseMessage Text(HttpStatusCode status, string text, HttpRequestMessage request) => new(status)
        {
            RequestMessage = request,
            Content = new StringContent(text, Encoding.UTF8, "text/plain"),
        };
    }

    /// <summary>Routes <c>file:</c> requests of the update HTTP clients to <see cref="OfflineUpdateFolder"/>.</summary>
    internal sealed class OfflineUpdateFolderHandler : DelegatingHandler
    {
        public OfflineUpdateFolderHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return request.RequestUri != null && MarketplaceConfig.IsFolderSource(request.RequestUri.OriginalString)
                ? OfflineUpdateFolder.RespondAsync(request, cancellationToken)
                : base.SendAsync(request, cancellationToken);
        }
    }
}
