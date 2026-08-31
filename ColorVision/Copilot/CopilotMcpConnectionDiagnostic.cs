using ColorVision.Copilot.Mcp;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotMcpConnectionDiagnostic
    {
        internal const int MaximumResponseBytes = 512 * 1024;

        public static async Task TestAsync(HttpClient client, Uri endpoint, string bearerToken, CancellationToken cancellationToken)
        {
            // ResponseHeadersRead does not apply HttpClient.Timeout to body reads.
            // Keep the entire handshake and status read within one diagnostic budget.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var initializeRequest = CreateRequest(endpoint, bearerToken, new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "initialize",
                    @params = new
                    {
                        protocolVersion = CopilotMcpRequestHandler.SupportedProtocolVersion,
                        capabilities = new { },
                        clientInfo = new { name = "colorvision-connection-test", version = "1.0.0" },
                    },
                });
                using var initializeResponse = await client.SendAsync(initializeRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                var initialized = await ReadResultAsync(initializeResponse, "initialize", 1, timeout.Token).ConfigureAwait(false);
                if (!initialized.TryGetProperty("protocolVersion", out var protocolVersion)
                    || protocolVersion.ValueKind != JsonValueKind.String
                    || protocolVersion.GetString() != CopilotMcpRequestHandler.SupportedProtocolVersion)
                {
                    throw new InvalidOperationException("initialize returned an unsupported MCP protocol version.");
                }

                var sessionIds = initializeResponse.Headers.TryGetValues(CopilotMcpRequestHandler.SessionHeaderName, out var values)
                    ? values.ToArray()
                    : Array.Empty<string>();
                if (sessionIds.Length != 1 || string.IsNullOrEmpty(sessionIds[0])
                    || sessionIds[0].Any(character => character < 0x21 || character > 0x7e))
                {
                    throw new InvalidOperationException("initialize did not return a valid MCP session header.");
                }
                var sessionId = sessionIds[0];

                using var notificationRequest = CreateRequest(endpoint, bearerToken, new
                {
                    jsonrpc = "2.0",
                    method = "notifications/initialized",
                }, sessionId);
                using var notificationResponse = await client.SendAsync(notificationRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                EnsureHttpSuccess(notificationResponse, "notifications/initialized");
                if (notificationResponse.StatusCode != HttpStatusCode.Accepted)
                    throw new InvalidOperationException("notifications/initialized did not receive the expected HTTP 202 acknowledgement.");

                using var statusRequest = CreateRequest(endpoint, bearerToken, new
                {
                    jsonrpc = "2.0",
                    id = 2,
                    method = "tools/call",
                    @params = new { name = "get_server_status", arguments = new { } },
                }, sessionId);
                using var statusResponse = await client.SendAsync(statusRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                var status = await ReadResultAsync(statusResponse, "get_server_status", 2, timeout.Token).ConfigureAwait(false);
                if (status.TryGetProperty("isError", out var isError)
                    && isError.ValueKind != JsonValueKind.False)
                {
                    throw new InvalidOperationException("get_server_status returned an MCP error or invalid error flag.");
                }
                if (!status.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array
                    || !content.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "text"
                        && item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString())))
                {
                    throw new InvalidOperationException("get_server_status returned no status text.");
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("MCP connection test timed out.", exception);
            }
        }

        private static HttpRequestMessage CreateRequest(Uri endpoint, string bearerToken, object payload, string? sessionId = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (sessionId != null)
            {
                request.Headers.Add(CopilotMcpRequestHandler.SessionHeaderName, sessionId);
                request.Headers.Add(CopilotMcpRequestHandler.ProtocolVersionHeaderName, CopilotMcpRequestHandler.SupportedProtocolVersion);
            }
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static async Task<JsonElement> ReadResultAsync(HttpResponseMessage response, string operation, int requestId, CancellationToken cancellationToken)
        {
            EnsureHttpSuccess(response, operation);
            var body = await CopilotBoundedHttpContentReader.ReadAsStringAsync(response.Content, MaximumResponseBytes, "MCP " + operation + " response", cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0"
                || !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var responseId) || responseId != requestId)
            {
                throw new InvalidOperationException(operation + " returned an invalid JSON-RPC response.");
            }
            // Do not surface remote error text: it can echo the bearer token or session ID.
            if (root.TryGetProperty("error", out _))
                throw new InvalidOperationException(operation + " returned a JSON-RPC error.");
            if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(operation + " returned no JSON-RPC result.");
            return result.Clone();
        }

        private static void EnsureHttpSuccess(HttpResponseMessage response, string operation)
        {
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{operation} failed: HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }
    }
}
