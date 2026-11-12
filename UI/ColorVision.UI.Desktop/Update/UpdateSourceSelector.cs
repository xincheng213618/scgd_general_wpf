using ColorVision.UI;
using ColorVision.UI.Marketplace;
using log4net;
using System.Net.Http;

namespace ColorVision.Update
{
    /// <summary>
    /// Picks the first reachable service address from <see cref="MarketplaceConfig.ServiceBaseUrls"/> so update checks,
    /// downloads, the marketplace and other service features use one source. With a single address nothing is probed.
    /// Concurrent callers share one probe, and a selection is reused briefly so one update check does not switch sources midway.
    /// </summary>
    public static class UpdateSourceSelector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(UpdateSourceSelector));
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan SelectionLifetime = TimeSpan.FromMinutes(5);
        private static readonly object SyncRoot = new();
        private static Task<string>? _selection;
        private static DateTime _selectedAtUtc;

        public static async Task<string> GetUpdateSourceUrlAsync(CancellationToken cancellationToken = default)
        {
            await EnsureAsync(cancellationToken).ConfigureAwait(false);
            return MarketplaceConfig.UpdateSourceUrl;
        }

        public static Task EnsureAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> urls = MarketplaceConfig.ServiceBaseUrls;
            if (urls.Count <= 1)
                return Task.CompletedTask;

            Task<string> selection;
            lock (SyncRoot)
            {
                bool reusable = _selection != null
                    && (!_selection.IsCompleted || DateTime.UtcNow - _selectedAtUtc < SelectionLifetime);
                if (!reusable)
                    _selection = SelectAsync(urls.ToArray());
                selection = _selection!;
            }

            return selection.WaitAsync(cancellationToken);
        }

        public static void Invalidate()
        {
            lock (SyncRoot)
                _selection = null;
        }

        private static async Task<string> SelectAsync(string[] urls)
        {
            try
            {
                foreach (string url in urls)
                {
                    if (await IsReachableAsync(url).ConfigureAwait(false))
                    {
                        MarketplaceConfig.SetActiveServiceBaseUrl(url);
                        log.Info($"Using update service {url}.");
                        return url;
                    }
                }

                log.Warn($"None of the configured update services responded; using {urls[0]}.");
                MarketplaceConfig.SetActiveServiceBaseUrl(urls[0]);
                return urls[0];
            }
            finally
            {
                lock (SyncRoot)
                    _selectedAtUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Any HTTP answer below 500 counts as reachable; a relay whose upstream is down closes the connection or answers 5xx.
        /// </summary>
        private static async Task<bool> IsReachableAsync(string baseUrl)
        {
            using CancellationTokenSource timeout = new(ProbeTimeout);
            try
            {
                using HttpResponseMessage response = await UpdateHttpClientProvider.GetClient()
                    .GetAsync($"{baseUrl}/api/app/latest-version", HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);
                return (int)response.StatusCode < 500;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                log.Info($"Update service {baseUrl} is not reachable: {ex.GetBaseException().Message}");
                return false;
            }
        }
    }

    /// <summary>Applies the saved service address list before other features read <see cref="MarketplaceConfig.ServiceBaseUrl"/>.</summary>
    public sealed class UpdateSourceInitializer : InitializerBase
    {
        public override int Order => 1;
        public override string Name => nameof(UpdateSourceInitializer);

        public override Task InitializeAsync()
        {
            MarketplaceConfig.SetServiceBaseUrls(MarketplaceConfig.ParseServiceBaseUrls(UpdateNetworkConfig.Instance.UpdateServerUrls));
            return Task.CompletedTask;
        }
    }
}
