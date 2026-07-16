using System;
using System.Net;
using System.Net.Http;

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

        public static HttpClient CreateClient()
        {
            return new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
        }
    }
}
