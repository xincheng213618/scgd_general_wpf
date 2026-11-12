using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Marketplace;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace ColorVision.Update
{
    public sealed class UpdateNetworkConfig : ViewModelBase, IConfig
    {
        private static readonly string[] ProxyEnvironmentVariableNames =
        [
            "ALL_PROXY",
            "HTTP_PROXY",
            "HTTPS_PROXY",
            "FTP_PROXY",
        ];

        public static UpdateNetworkConfig Instance => ConfigService.Instance.GetRequiredService<UpdateNetworkConfig>();

        /// <summary>
        /// Backup update server or ordered source list; empty means the official service. Applied to
        /// <see cref="MarketplaceConfig"/> whenever it is set, including when the saved configuration loads.
        /// </summary>
        [ConfigSetting(Order = 510, Section = ConfigSettingConstants.SectionUpdates, Description = "UpdateServerUrlsDescription")]
        [DisplayName("UpdateServerUrls")]
        public string UpdateServerUrls
        {
            get => _updateServerUrls;
            set
            {
                _updateServerUrls = value?.Trim() ?? string.Empty;
                MarketplaceConfig.ConfigureUpdateSources(_updateServerUrls);
                UpdateSourceSelector.Invalidate();
                OnPropertyChanged();
            }
        }
        private string _updateServerUrls = string.Empty;

        [ConfigSetting(Order = 520, Section = ConfigSettingConstants.SectionUpdates, Description = "DisableSystemProxyForUpdatesDescription")]
        [DisplayName("DisableSystemProxyForUpdates")]
        public bool DisableSystemProxyForUpdates
        {
            get => _disableSystemProxyForUpdates;
            set
            {
                if (_disableSystemProxyForUpdates == value)
                    return;

                _disableSystemProxyForUpdates = value;
                OnPropertyChanged();
            }
        }
        private bool _disableSystemProxyForUpdates = true;

        public static void ConfigureChildProcessProxyEnvironment(ProcessStartInfo startInfo, bool disableSystemProxy)
        {
            ArgumentNullException.ThrowIfNull(startInfo);
            if (!disableSystemProxy)
                return;

            string[] inheritedProxyVariables = startInfo.Environment.Keys
                .Where(key => ProxyEnvironmentVariableNames.Contains(key, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            foreach (string variableName in inheritedProxyVariables)
                startInfo.Environment.Remove(variableName);
        }
    }

    public static class UpdateHttpClientProvider
    {
        private const int MetadataRequestAttemptCount = 3;
        private static readonly TimeSpan[] MetadataRequestRetryDelays =
        [
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(900),
        ];
        private static readonly HttpClient SystemProxyClient = CreateClient(useProxy: true);
        private static readonly HttpClient DirectClient = CreateClient(useProxy: false);

        public static HttpClient GetClient()
        {
            return UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates
                ? DirectClient
                : SystemProxyClient;
        }

        public static async Task<HttpResponseMessage> SendWithTransientRetryAsync(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan requestTimeout,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(requestFactory);

            for (int attempt = 1; attempt <= MetadataRequestAttemptCount; attempt++)
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(requestTimeout);
                using HttpRequestMessage request = requestFactory();
                try
                {
                    HttpResponseMessage response = await GetClient().SendAsync(
                        request,
                        HttpCompletionOption.ResponseContentRead,
                        timeoutSource.Token).ConfigureAwait(false);
                    if (attempt < MetadataRequestAttemptCount && IsTransientStatusCode(response.StatusCode))
                    {
                        response.Dispose();
                        await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return response;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (attempt < MetadataRequestAttemptCount)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt < MetadataRequestAttemptCount)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("The update metadata request retry loop completed without a response.");
        }

        private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
        {
            return Task.Delay(MetadataRequestRetryDelays[attempt - 1], cancellationToken);
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        private static HttpClient CreateClient(bool useProxy)
        {
            HttpClientHandler handler = new() { UseProxy = useProxy };
            return new HttpClient(new OfflineUpdateFolderHandler(handler)) { Timeout = TimeSpan.FromSeconds(15) };
        }
    }
}
