using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotWebSearchTool : ICopilotTool
    {
        private readonly Func<string, CancellationToken, Task<CopilotWebSearchResult>> _search;
        private readonly Func<CopilotAgentRequest, string, CancellationToken, Task<CopilotToolResult>> _fetch;

        public CopilotWebSearchTool()
            : this(CopilotWebSearchCapability.SearchAsync, FetchSelectedResultAsync)
        {
        }

        public CopilotWebSearchTool(
            Func<string, CancellationToken, Task<CopilotWebSearchResult>> search,
            Func<CopilotAgentRequest, string, CancellationToken, Task<CopilotToolResult>> fetch)
        {
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        }

        public string Name => "WebSearch";

        public string Description => "Search the public web, select the result most relevant to the requested site, and deep-read that page with bounded same-origin structured resources. Returns search leads even when the selected page cannot be read.";

        public CopilotToolEvidenceMode EvidenceMode => CopilotToolEvidenceMode.RedactedExcerpt;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Focused public web search query.", required: true);

        public TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(60);

        public bool CanHandle(CopilotAgentRequest request)
        {
            return CopilotToolIntentPolicy.NeedsPublicWebSearch(request);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;
            var result = await _search(query, cancellationToken);
            var searchOnlyResult = result.ToCapabilityResult().ToToolResult(Name);
            if (!result.Success || result.Hits.Count == 0)
                return searchOnlyResult;

            var selectedHit = SelectDeepReadHit(request, query, result.Hits);
            if (selectedHit == null)
                return searchOnlyResult;

            CopilotToolResult fetchResult;
            try
            {
                fetchResult = await _fetch(request, selectedHit.Url, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return WithDeepReadUnavailable(searchOnlyResult, selectedHit.Url);
            }

            if (!fetchResult.Success || string.IsNullOrWhiteSpace(fetchResult.Content))
                return WithDeepReadUnavailable(searchOnlyResult, selectedHit.Url);

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"{result.Summary} Deep-read selected result {selectedHit.Url}: {fetchResult.Summary}",
                Content = string.Join(Environment.NewLine + Environment.NewLine, new[]
                {
                    searchOnlyResult.Content,
                    $"[Selected Search Result Deep Read] {selectedHit.Url}",
                    fetchResult.Content,
                }),
                FailureKind = CopilotToolFailureKind.None,
            };
        }

        private static async Task<CopilotToolResult> FetchSelectedResultAsync(
            CopilotAgentRequest request,
            string url,
            CancellationToken cancellationToken)
        {
            return await new CopilotFetchUrlTool().ExecuteAsync(
                request,
                new CopilotAgentToolInput { Query = url },
                cancellationToken);
        }

        private static CopilotToolResult WithDeepReadUnavailable(CopilotToolResult searchResult, string selectedUrl)
        {
            return new CopilotToolResult
            {
                ToolName = "WebSearch",
                Success = true,
                Summary = searchResult.Summary + " The selected result could not be deep-read; the search leads remain available.",
                Content = searchResult.Content + Environment.NewLine + Environment.NewLine
                    + $"[Selected Search Result Deep Read Unavailable] {selectedUrl}"
                    + Environment.NewLine
                    + "Use another returned result with FetchUrl only when full page evidence is still required.",
                FailureKind = CopilotToolFailureKind.None,
            };
        }

        private static CopilotWebSearchHit? SelectDeepReadHit(
            CopilotAgentRequest request,
            string query,
            IReadOnlyList<CopilotWebSearchHit> hits)
        {
            var eligibleHits = hits.Where(hit => IsEligiblePublicResult(hit.Url)).ToArray();
            if (eligibleHits.Length == 0)
                return null;

            var preferredHosts = ExtractPreferredHosts((request.UserText ?? string.Empty) + " " + query);
            return eligibleHits.FirstOrDefault(hit => Uri.TryCreate(hit.Url, UriKind.Absolute, out var uri)
                    && preferredHosts.Any(host => HostMatches(uri.Host, host)))
                ?? eligibleHits[0];
        }

        private static HashSet<string> ExtractPreferredHosts(string text)
        {
            var hosts = CopilotWebPageToolSupport.ExtractHttpUrls(text)
                .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty)
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(text ?? string.Empty, @"(?i)(?:^|\s)site:(?<host>[a-z0-9.-]+)"))
            {
                var host = match.Groups["host"].Value.Trim().Trim('.');
                if (!string.IsNullOrWhiteSpace(host))
                    hosts.Add(host);
            }
            return hosts;
        }

        private static bool IsEligiblePublicResult(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                && !uri.IsLoopback
                && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(uri.UserInfo);
        }

        private static bool HostMatches(string candidate, string preferred)
        {
            return string.Equals(candidate, preferred, StringComparison.OrdinalIgnoreCase)
                || candidate.EndsWith("." + preferred, StringComparison.OrdinalIgnoreCase);
        }
    }
}
