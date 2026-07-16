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

        public string Description => "Fetch up to three web resources per call, including readable page text and bounded same-origin structured resources, and report omitted inputs and partial failures explicitly.";

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

            var resolvedUrls = ResolveUrls(request, toolInput);
            var urls = resolvedUrls.Take(MaxResourcesPerRequest).ToArray();

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

            var requestedOutcomes = await FetchBatchAsync(urls, cancellationToken).ConfigureAwait(false);
            var remainingSlots = MaxResourcesPerRequest - requestedOutcomes.Length;
            var discoveredUrls = requestedOutcomes
                .Where(outcome => outcome.Page != null)
                .SelectMany(outcome => outcome.Page!.Value.DiscoveredResourceUrls)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Where(url => !urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(remainingSlots)
                .ToArray();
            var discoveredOutcomes = await FetchBatchAsync(discoveredUrls, cancellationToken).ConfigureAwait(false);
            var outcomes = requestedOutcomes.Concat(discoveredOutcomes).ToArray();
            var successCount = outcomes.Count(outcome => outcome.Page != null);
            var attemptedCount = outcomes.Length;
            var omittedInputCount = Math.Max(0, resolvedUrls.Count - urls.Length);
            var errors = outcomes.Where(outcome => outcome.Page == null).Select(outcome => $"{outcome.Url}: {outcome.Error}").ToArray();
            var failureKinds = outcomes.Where(outcome => outcome.Page == null).Select(outcome => outcome.FailureKind).ToArray();

            var builder = new StringBuilder();
            builder.AppendLine("[Web Fetch Scope]");
            builder.AppendLine($"input_urls_total: {resolvedUrls.Count}");
            builder.AppendLine($"input_urls_attempted: {urls.Length}");
            builder.AppendLine($"input_urls_omitted: {omittedInputCount}");
            builder.AppendLine($"input_set_complete: {(omittedInputCount == 0).ToString().ToLowerInvariant()}");
            builder.AppendLine($"discovered_urls_attempted: {discoveredUrls.Length}");
            builder.AppendLine($"all_attempts_succeeded: {(successCount == attemptedCount).ToString().ToLowerInvariant()}");
            builder.AppendLine();
            foreach (var outcome in outcomes)
            {
                builder.AppendLine(outcome.Page != null
                    ? CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(outcome.Page.Value)
                    : CopilotWebPageToolSupport.BuildFailedWebPageContextBlock(outcome.Url, outcome.Error));
                builder.AppendLine();
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"Fetched {successCount}/{attemptedCount} web resources ({urls.Length}/{resolvedUrls.Count} input URLs attempted, {discoveredUrls.Length} discovered)."
                    : $"Failed to fetch any web resources from {attemptedCount} URLs.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Length == 0 ? string.Empty : string.Join("; ", errors),
                FailureKind = successCount == 0 && failureKinds.Length > 0 && failureKinds.All(kind => kind == CopilotToolFailureKind.Transient)
                    ? CopilotToolFailureKind.Transient
                    : successCount == 0 ? CopilotToolFailureKind.Unspecified : CopilotToolFailureKind.None,
            };
        }

        private async Task<FetchOutcome[]> FetchBatchAsync(
            string[] urls,
            CancellationToken cancellationToken)
        {
            if (urls.Length == 0)
                return Array.Empty<FetchOutcome>();

            return await Task.WhenAll(urls.Select(url => FetchOneAsync(url, cancellationToken))).ConfigureAwait(false);
        }

        private async Task<FetchOutcome> FetchOneAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var page = await _pageLoader(url, cancellationToken).ConfigureAwait(false);
                return new FetchOutcome(url, page, string.Empty, CopilotToolFailureKind.None);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var error = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
                return new FetchOutcome(url, null, error, CopilotToolFailureClassifier.Classify(ex));
            }
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

        private readonly record struct FetchOutcome(
            string Url,
            CopilotFetchedWebPageContent? Page,
            string Error,
            CopilotToolFailureKind FailureKind);
    }
}
