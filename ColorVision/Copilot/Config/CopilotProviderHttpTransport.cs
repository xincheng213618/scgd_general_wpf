using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderHttpTransport
    {
        // Agent SDK clients can be disposed after each run. Share only the handler so
        // their HttpClient wrappers retain a common connection pool without owning it.
        private static readonly HttpMessageHandler SharedHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        public static HttpClient CreateClient(string? profileId = null)
        {
            var hasProfile = !string.IsNullOrWhiteSpace(profileId);
            var handler = hasProfile
                ? new CopilotProviderRateLimitTrackingHandler(profileId!, SharedHandler)
                : SharedHandler;
            return new HttpClient(handler, disposeHandler: hasProfile)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
        }
    }

    internal sealed class CopilotProviderRateLimitTrackingHandler : HttpMessageHandler
    {
        private readonly string _profileId;
        private readonly HttpMessageInvoker _invoker;

        public CopilotProviderRateLimitTrackingHandler(
            string profileId,
            HttpMessageHandler innerHandler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
            ArgumentNullException.ThrowIfNull(innerHandler);
            _profileId = profileId.Trim();
            _invoker = new HttpMessageInvoker(innerHandler, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await _invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
            CopilotProviderRateLimitTracker.Capture(_profileId, response);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _invoker.Dispose();
            base.Dispose(disposing);
        }
    }
}
