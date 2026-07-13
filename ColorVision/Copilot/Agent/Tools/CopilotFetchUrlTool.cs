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
        private const int MaxResourcesPerRequest = 3;
        private readonly Func<string, CancellationToken, Task<CopilotFetchedWebPageContent>> _pageLoader;

        public CopilotFetchUrlTool()
            : this(CopilotWebPageToolSupport.LoadWebPageContentAsync)
        {
        }

        public CopilotFetchUrlTool(Func<string, CancellationToken, Task<CopilotFetchedWebPageContent>> pageLoader)
        {
            _pageLoader = pageLoader ?? throw new ArgumentNullException(nameof(pageLoader));
        }

        public string Name => "FetchUrl";

        public string Description => "Fetch a web page's readable text, structured data resources, and bounded same-origin navigation links for evidence-driven site exploration.";

        public CopilotToolEvidenceMode EvidenceMode => CopilotToolEvidenceMode.RedactedExcerpt;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("One or more complete http/https URLs to fetch, separated by spaces.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            return CopilotToolIntentPolicy.NeedsUrlFetch(request);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var urls = ResolveUrls(request, toolInput)
                .Take(MaxResourcesPerRequest)
                .ToArray();

            if (urls.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "No fetchable web page URL was detected.",
                    ErrorMessage = "The current request has no processable web page URL; the planner can provide a complete URL in input.query.",
                    FailureKind = CopilotToolFailureKind.Validation,
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var attemptedCount = 0;
            var discoveredCount = 0;
            var errors = new List<string>();
            var failureKinds = new List<CopilotToolFailureKind>();
            var pendingUrls = new Queue<string>(urls);
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (pendingUrls.Count > 0 && attemptedCount < MaxResourcesPerRequest)
            {
                var url = pendingUrls.Dequeue();
                if (!visitedUrls.Add(url))
                    continue;

                cancellationToken.ThrowIfCancellationRequested();
                attemptedCount++;

                try
                {
                    var page = await _pageLoader(url, cancellationToken);
                    builder.AppendLine(CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page));
                    builder.AppendLine();
                    successCount++;

                    foreach (var relatedUrl in page.DiscoveredResourceUrls)
                    {
                        if (pendingUrls.Count + attemptedCount >= MaxResourcesPerRequest)
                            break;
                        if (visitedUrls.Contains(relatedUrl) || pendingUrls.Contains(relatedUrl, StringComparer.OrdinalIgnoreCase))
                            continue;
                        pendingUrls.Enqueue(relatedUrl);
                        discoveredCount++;
                    }
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
                    failureKinds.Add(CopilotToolFailureClassifier.Classify(ex));
                }
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"Fetched {successCount}/{attemptedCount} web resources"
                        + (discoveredCount > 0 ? $" ({urls.Length} requested, {discoveredCount} discovered)." : ".")
                    : $"Failed to fetch any web resources from {attemptedCount} URLs.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
                FailureKind = successCount == 0 && failureKinds.Count > 0 && failureKinds.All(kind => kind == CopilotToolFailureKind.Transient)
                    ? CopilotToolFailureKind.Transient
                    : successCount == 0 ? CopilotToolFailureKind.Unspecified : CopilotToolFailureKind.None,
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
