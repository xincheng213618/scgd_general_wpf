using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotFetchUrlTool : ICopilotTool
    {
        private const int MaxUrlsPerRequest = 3;

        public string Name => "FetchUrl";

        public string Description => "Fetch the title, description, and readable body text for web pages mentioned by the user.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return true;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var urls = ResolveUrls(request, toolInput)
                .Take(MaxUrlsPerRequest)
                .ToArray();

            if (urls.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "No fetchable web page URL was detected.",
                    ErrorMessage = "The current request has no processable web page URL; the planner can provide a complete URL in input.query.",
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();

            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var page = await CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);
                    builder.AppendLine(CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page));
                    builder.AppendLine();
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    builder.AppendLine(CopilotWebPageToolSupport.BuildFailedWebPageContextBlock(url, ex.Message));
                    builder.AppendLine();
                    errors.Add($"{url}: {ex.Message}");
                }
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"Fetched {successCount}/{urls.Length} web pages."
                    : $"Failed to fetch any web pages from {urls.Length} URLs.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
            };
        }

        private static IReadOnlyList<string> ResolveUrls(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var query = (toolInput?.Query ?? string.Empty).Trim();
            var urls = CopilotWebPageToolSupport.ExtractHttpUrls(query);
            if (urls.Count > 0)
                return urls;

            var singleQueryUrl = TryNormalizeSingleUrl(query);
            if (!string.IsNullOrWhiteSpace(singleQueryUrl))
                return new[] { singleQueryUrl };

            return CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText);
        }

        private static string TryNormalizeSingleUrl(string value)
        {
            var candidate = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate)
                || candidate.Contains(' ')
                || candidate.Contains('\t')
                || candidate.Contains('\r')
                || candidate.Contains('\n'))
            {
                return string.Empty;
            }

            var normalized = CopilotWebPageToolSupport.NormalizeWebPageUrl(candidate);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                return string.Empty;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(uri.Host) || !uri.Host.Contains('.')
                ? string.Empty
                : uri.ToString();
        }
    }
}
