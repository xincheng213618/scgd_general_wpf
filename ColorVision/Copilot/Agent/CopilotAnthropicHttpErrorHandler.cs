using Anthropic.Exceptions;
using ColorVision.Copilot.Mcp;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAnthropicHttpErrorHandler(string apiKey) : DelegatingHandler
    {
        private const int MaximumErrorResponseBytes = 256 * 1024;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return response;

            using (response)
            {
                string body;
                try
                {
                    body = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                        response.Content, MaximumErrorResponseBytes, "Provider error response", cancellationToken).ConfigureAwait(false);
                }
                catch (CopilotHttpContentSizeLimitException exception)
                {
                    body = exception.Message;
                }
                catch (IOException)
                {
                    body = "The provider error response could not be read.";
                }
                catch (HttpRequestException)
                {
                    body = "The provider error response could not be read.";
                }

                if (!string.IsNullOrEmpty(apiKey))
                    body = body.Replace(apiKey, "<redacted>", StringComparison.Ordinal);
                var providerException = AnthropicExceptionFactory.CreateApiException(
                    response.StatusCode, CopilotMcpAuditLogger.RedactText(body));
                CopilotProviderRetryChatClient.PreserveRetryAfter(response, providerException, includeMilliseconds: true);
                CopilotProviderRequestId.Preserve(providerException, CopilotProviderRequestId.Redact(
                    CopilotProviderRequestId.Extract(response), apiKey));
                throw providerException;
            }
        }
    }
}
