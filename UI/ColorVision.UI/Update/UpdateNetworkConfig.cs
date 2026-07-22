using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace ColorVision.Update
{
    public sealed class UpdateNetworkConfig : ViewModelBase, IConfig
    {
        public static UpdateNetworkConfig Instance => ConfigService.Instance.GetRequiredService<UpdateNetworkConfig>();

        [ConfigSetting(Order = 520, Section = ConfigSettingConstants.SectionBasic, Description = "DisableSystemProxyForUpdatesDescription")]
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
    }

    public static class UpdateHttpClientProvider
    {
        private const int MetadataRequestAttemptCount = 2;
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
                }
                catch (HttpRequestException) when (attempt < MetadataRequestAttemptCount)
                {
                }
            }

            throw new InvalidOperationException("The update metadata request retry loop completed without a response.");
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
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        }
    }
}
