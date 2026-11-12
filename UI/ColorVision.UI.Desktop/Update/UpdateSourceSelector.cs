using ColorVision.UI;
using ColorVision.UI.Marketplace;
using log4net;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;

namespace ColorVision.Update
{
    public readonly record struct UpdateSourceSelection(string Url, Version? LatestVersion);

    /// <summary>
    /// Selects the first source that serves valid update metadata. Concurrent callers share the probe.
    /// </summary>
    public static class UpdateSourceSelector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(UpdateSourceSelector));
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan SelectionLifetime = TimeSpan.FromMinutes(5);
        private static readonly object SyncRoot = new();
        private static Task<Selection>? _selection;
        private static IReadOnlyList<string>? _probedSources;
        private static bool _probedDirectMode;
        private static bool _probedForRefresh;

        private sealed record Selection(UpdateSourceSelection Source, DateTime SelectedAtUtc);

        public static async Task<string> GetUpdateSourceUrlAsync(CancellationToken cancellationToken = default, bool forceRefresh = false)
        {
            return (await GetUpdateSourceAsync(cancellationToken, forceRefresh).ConfigureAwait(false)).Url;
        }

        public static async Task<UpdateSourceSelection> GetUpdateSourceAsync(CancellationToken cancellationToken = default, bool forceRefresh = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<Selection> selection;
            bool useCachedAddress;
            lock (SyncRoot)
            {
                IReadOnlyList<string> urls = MarketplaceConfig.ServiceBaseUrls;
                if (urls.Count == 1)
                    return new UpdateSourceSelection(urls[0], null);

                bool directMode = UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates;
                bool reusable = ReferenceEquals(urls, _probedSources) && directMode == _probedDirectMode && _selection != null
                    && (!_selection.IsCompleted
                        ? !forceRefresh || _probedForRefresh
                        : !forceRefresh && _selection.IsCompletedSuccessfully && _selection.Result.Source.LatestVersion != null
                            && DateTime.UtcNow - _selection.Result.SelectedAtUtc < SelectionLifetime);
                useCachedAddress = reusable && _selection!.IsCompleted;
                if (!reusable)
                {
                    _probedSources = urls;
                    _probedDirectMode = directMode;
                    _probedForRefresh = forceRefresh;
                    _selection = Task.Run(() => SelectAsync(urls));
                }
                selection = _selection!;
            }

            Selection result = await selection.WaitAsync(cancellationToken).ConfigureAwait(false);
            lock (SyncRoot)
            {
                if (ReferenceEquals(_selection, selection) && ReferenceEquals(_probedSources, MarketplaceConfig.ServiceBaseUrls))
                    MarketplaceConfig.SetActiveServiceBaseUrl(result.Source.Url);
            }
            return useCachedAddress ? result.Source with { LatestVersion = null } : result.Source;
        }

        public static void Invalidate()
        {
            lock (SyncRoot)
                _selection = null;
        }

        private static async Task<Selection> SelectAsync(IReadOnlyList<string> urls)
        {
            foreach (string url in urls)
            {
                Version? version = await ProbeLatestVersionAsync(url).ConfigureAwait(false);
                if (version != null)
                {
                    log.Info($"Using update service {url}.");
                    return new Selection(new UpdateSourceSelection(url, version), DateTime.UtcNow);
                }
            }

            log.Warn($"None of the configured sources returned valid update metadata; using {urls[0]}.");
            return new Selection(new UpdateSourceSelection(urls[0], null), DateTime.UtcNow);
        }

        private static async Task<Version?> ProbeLatestVersionAsync(string baseUrl)
        {
            using CancellationTokenSource timeout = new(ProbeTimeout);
            try
            {
                using HttpResponseMessage response = await UpdateHttpClientProvider.GetClient()
                    .GetAsync($"{baseUrl}/api/app/latest-version", HttpCompletionOption.ResponseContentRead, timeout.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode
                    && TryParseLatestVersion(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false), out Version? version))
                    return version;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                log.Info($"Update service {baseUrl} is not reachable: {ex.GetBaseException().Message}");
            }
            return null;
        }

        public static bool TryParseLatestVersion(string payload, [NotNullWhen(true)] out Version? version)
        {
            string value = payload.Trim();
            if (value.StartsWith('{'))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(value);
                    value = document.RootElement.TryGetProperty("version", out JsonElement element)
                        && element.ValueKind == JsonValueKind.String ? element.GetString()!.Trim() : string.Empty;
                }
                catch (JsonException)
                {
                    version = null;
                    return false;
                }
            }
            return Version.TryParse(value, out version) && version > new Version();
        }
    }

    /// <summary>Applies the saved update source settings during startup.</summary>
    public sealed class UpdateSourceInitializer : InitializerBase
    {
        public override int Order => 1;
        public override string Name => nameof(UpdateSourceInitializer);

        public override Task InitializeAsync()
        {
            MarketplaceConfig.ConfigureUpdateSources(UpdateNetworkConfig.Instance.UpdateServerUrls);
            return Task.CompletedTask;
        }
    }
}
