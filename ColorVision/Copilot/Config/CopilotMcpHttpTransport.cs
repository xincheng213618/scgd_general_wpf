using System;
using System.Net;
using System.Net.Http;

namespace ColorVision.Copilot
{
    internal static class CopilotMcpHttpTransport
    {
        // MCP clients are short-lived per Agent run. Share the connection pool while
        // allowing each SDK transport to own and dispose its HttpClient wrapper.
        private static readonly HttpMessageHandler SharedHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        public static HttpClient CreateClient(TimeSpan timeout)
        {
            return new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = timeout,
            };
        }
    }
}
