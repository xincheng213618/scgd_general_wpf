using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotWebSearchHit
    {
        public int Rank { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;

        public string Snippet { get; init; } = string.Empty;
    }

    public sealed class CopilotWebSearchResult
    {
        public bool Success { get; init; }

        public string Query { get; init; } = string.Empty;

        public string Provider { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public IReadOnlyList<CopilotWebSearchHit> Hits { get; init; } = Array.Empty<CopilotWebSearchHit>();

        public CopilotCapabilityResult ToCapabilityResult()
        {
            return new CopilotCapabilityResult
            {
                Success = Success,
                Summary = Summary,
                Content = Content,
                ErrorMessage = ErrorMessage,
            };
        }
    }

    public static class CopilotWebSearchCapability
    {
        private const int MaxResults = 8;
        private const string ProviderName = "DuckDuckGo HTML";
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public static async Task<CopilotWebSearchResult> SearchAsync(string? query, CancellationToken cancellationToken)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return new CopilotWebSearchResult
                {
                    Success = false,
                    Query = string.Empty,
                    Provider = ProviderName,
                    Summary = "Missing web search query.",
                    ErrorMessage = "The web search tool requires a non-empty query.",
                };
            }

            try
            {
                var searchUrl = "https://duckduckgo.com/html/?q=" + Uri.EscapeDataString(normalizedQuery);
                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var hits = ExtractDuckDuckGoHits(html)
                    .Take(MaxResults)
                    .ToArray();

                if (hits.Length == 0)
                {
                    return new CopilotWebSearchResult
                    {
                        Success = false,
                        Query = normalizedQuery,
                        Provider = ProviderName,
                        Summary = "Web search completed but returned no usable results.",
                        ErrorMessage = "No result title and URL pairs could be extracted from the search response.",
                    };
                }

                return new CopilotWebSearchResult
                {
                    Success = true,
                    Query = normalizedQuery,
                    Provider = ProviderName,
                    Hits = hits,
                    Summary = $"Web search found {hits.Length} results for \"{normalizedQuery}\".",
                    Content = BuildContent(normalizedQuery, hits),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotWebSearchResult
                {
                    Success = false,
                    Query = normalizedQuery,
                    Provider = ProviderName,
                    Summary = "Web search failed.",
                    ErrorMessage = ex.Message,
                };
            }
        }

        public static IReadOnlyList<CopilotWebSearchHit> ExtractDuckDuckGoHits(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);

            var results = new List<CopilotWebSearchHit>();
            var nodes = document.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' result ')]")
                ?? document.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' result__body ')]")
                ?? Enumerable.Empty<HtmlNode>();

            foreach (var node in nodes)
            {
                var link = node.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' result__a ')]")
                    ?? node.SelectSingleNode(".//a[@href]");
                if (link == null)
                    continue;

                var title = NormalizeText(HtmlEntity.DeEntitize(link.InnerText));
                var url = NormalizeResultUrl(link.GetAttributeValue("href", string.Empty));
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                    continue;

                var snippetNode = node.SelectSingleNode(".//*[contains(concat(' ', normalize-space(@class), ' '), ' result__snippet ')]")
                    ?? node.SelectSingleNode(".//*[contains(concat(' ', normalize-space(@class), ' '), ' result__extras__url ')]");
                var snippet = NormalizeText(HtmlEntity.DeEntitize(snippetNode?.InnerText ?? string.Empty));

                if (results.Any(item => string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)))
                    continue;

                results.Add(new CopilotWebSearchHit
                {
                    Rank = results.Count + 1,
                    Title = title,
                    Url = url,
                    Snippet = snippet,
                });
            }

            return results;
        }

        private static string BuildContent(string query, IReadOnlyList<CopilotWebSearchHit> hits)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Web Search Results]");
            builder.AppendLine($"Provider: {ProviderName}");
            builder.AppendLine($"Query: {query}");
            builder.AppendLine();

            foreach (var hit in hits)
            {
                builder.Append(hit.Rank)
                    .Append(". ")
                    .AppendLine(hit.Title);
                builder.Append("   URL: ").AppendLine(hit.Url);
                if (!string.IsNullOrWhiteSpace(hit.Snippet))
                    builder.Append("   Snippet: ").AppendLine(hit.Snippet);
            }

            return builder.ToString().TrimEnd();
        }

        private static string NormalizeResultUrl(string value)
        {
            var candidate = HtmlEntity.DeEntitize(value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                return string.Empty;

            if (candidate.StartsWith("//", StringComparison.Ordinal))
                candidate = "https:" + candidate;
            else if (candidate.StartsWith('/'))
                candidate = "https://duckduckgo.com" + candidate;

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                return string.Empty;

            var uddg = GetQueryValue(uri.Query, "uddg");
            if (!string.IsNullOrWhiteSpace(uddg) && Uri.TryCreate(uddg, UriKind.Absolute, out var decodedUri))
                uri = decodedUri;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return uri.ToString();
        }

        private static string GetQueryValue(string query, string name)
        {
            var normalized = (query ?? string.Empty).TrimStart('?');
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            foreach (var part in normalized.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = part.IndexOf('=');
                var key = index >= 0 ? part[..index] : part;
                if (!string.Equals(WebUtility.UrlDecode(key), name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = index >= 0 ? part[(index + 1)..] : string.Empty;
                return WebUtility.UrlDecode(value) ?? string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeText(string value)
        {
            var normalized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);

            return normalized;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-WebSearch/1.0");
            return client;
        }
    }
}
