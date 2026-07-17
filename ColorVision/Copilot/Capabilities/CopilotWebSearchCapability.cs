using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        public CopilotToolFailureKind FailureKind { get; init; }

        public IReadOnlyList<CopilotWebSearchHit> Hits { get; init; } = Array.Empty<CopilotWebSearchHit>();

        public CopilotCapabilityResult ToCapabilityResult()
        {
            return new CopilotCapabilityResult
            {
                Success = Success,
                Summary = Summary,
                Content = Content,
                ErrorMessage = ErrorMessage,
                FailureKind = FailureKind,
            };
        }
    }

    public static class CopilotWebSearchCapability
    {
        private const int MaxResults = 8;
        public const int MaxSearchResponseBytes = 512 * 1024;
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly SearchProvider[] Providers =
        {
            new("DuckDuckGo HTML", query => "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query), ExtractDuckDuckGoHits),
            new("DuckDuckGo Lite", query => "https://lite.duckduckgo.com/lite/?q=" + Uri.EscapeDataString(query), ExtractDuckDuckGoLiteHits),
            new("Bing RSS", query => "https://www.bing.com/search?q=" + Uri.EscapeDataString(query) + "&format=rss", ExtractBingRssHits),
        };

        public static async Task<CopilotWebSearchResult> SearchAsync(string? query, CancellationToken cancellationToken)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return new CopilotWebSearchResult
                {
                    Success = false,
                    Query = string.Empty,
                    Provider = string.Empty,
                    Summary = "Missing web search query.",
                    ErrorMessage = "The web search tool requires a non-empty query.",
                    FailureKind = CopilotToolFailureKind.Validation,
                };
            }

            var failures = new List<(string Provider, string Message, CopilotToolFailureKind Kind)>();
            foreach (var provider in Providers)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, provider.BuildUrl(normalizedQuery));
                    using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var payload = await ReadSearchResponseAsync(response, cancellationToken);
                    var hits = provider.ExtractHits(payload)
                        .Take(MaxResults)
                        .ToArray();
                    if (hits.Length == 0)
                    {
                        failures.Add((provider.Name, "No result title and URL pairs could be extracted.", CopilotToolFailureKind.NotFound));
                        continue;
                    }

                    return new CopilotWebSearchResult
                    {
                        Success = true,
                        Query = normalizedQuery,
                        Provider = provider.Name,
                        Hits = hits,
                        Summary = $"Web search found {hits.Length} results for \"{normalizedQuery}\" via {provider.Name}.",
                        Content = BuildContent(provider.Name, normalizedQuery, hits),
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add((provider.Name, ex.Message, CopilotToolFailureClassifier.Classify(ex)));
                }
            }

            var failureKind = failures.Count > 0 && failures.All(failure => failure.Kind == CopilotToolFailureKind.NotFound)
                ? CopilotToolFailureKind.NotFound
                : failures.LastOrDefault(failure => failure.Kind != CopilotToolFailureKind.NotFound).Kind;
            if (failureKind == CopilotToolFailureKind.None)
                failureKind = CopilotToolFailureKind.Unspecified;
            return new CopilotWebSearchResult
            {
                Success = false,
                Query = normalizedQuery,
                Provider = string.Join(" -> ", failures.Select(failure => failure.Provider)),
                Summary = "Web search failed across all configured providers.",
                ErrorMessage = string.Join(" | ", failures.Select(failure => $"{failure.Provider}: {failure.Message}")),
                FailureKind = failureKind,
            };
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

        public static IReadOnlyList<CopilotWebSearchHit> ExtractDuckDuckGoLiteHits(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);
            var results = new List<CopilotWebSearchHit>();
            var links = document.DocumentNode.SelectNodes("//a[contains(concat(' ', normalize-space(@class), ' '), ' result-link ')]")
                ?? Enumerable.Empty<HtmlNode>();
            foreach (var link in links)
            {
                var title = NormalizeText(HtmlEntity.DeEntitize(link.InnerText));
                var url = NormalizeResultUrl(link.GetAttributeValue("href", string.Empty));
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)
                    || results.Any(item => string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var row = link.Ancestors("tr").FirstOrDefault();
                var snippetNode = row?.SelectSingleNode("following-sibling::tr[1]//*[contains(concat(' ', normalize-space(@class), ' '), ' result-snippet ')]");
                results.Add(new CopilotWebSearchHit
                {
                    Rank = results.Count + 1,
                    Title = title,
                    Url = url,
                    Snippet = NormalizeText(HtmlEntity.DeEntitize(snippetNode?.InnerText ?? string.Empty)),
                });
            }
            return results;
        }

        public static IReadOnlyList<CopilotWebSearchHit> ExtractBingRssHits(string xml)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(xml ?? string.Empty, LoadOptions.None);
            }
            catch (System.Xml.XmlException)
            {
                return Array.Empty<CopilotWebSearchHit>();
            }

            var results = new List<CopilotWebSearchHit>();
            foreach (var item in document.Descendants().Where(element => element.Name.LocalName == "item"))
            {
                var title = NormalizeHtmlText(item.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value ?? string.Empty);
                var url = NormalizeResultUrl(item.Elements().FirstOrDefault(element => element.Name.LocalName == "link")?.Value ?? string.Empty);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)
                    || results.Any(hit => string.Equals(hit.Url, url, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var description = item.Elements().FirstOrDefault(element => element.Name.LocalName == "description")?.Value ?? string.Empty;
                results.Add(new CopilotWebSearchHit
                {
                    Rank = results.Count + 1,
                    Title = title,
                    Url = url,
                    Snippet = NormalizeHtmlText(description),
                });
            }
            return results;
        }

        private static string BuildContent(string providerName, string query, IReadOnlyList<CopilotWebSearchHit> hits)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Web Search Results]");
            builder.AppendLine($"Provider: {providerName}");
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

        private static string NormalizeHtmlText(string value)
        {
            var document = new HtmlDocument();
            document.LoadHtml(value ?? string.Empty);
            return NormalizeText(HtmlEntity.DeEntitize(document.DocumentNode.InnerText));
        }

        private static async Task<string> ReadSearchResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                response.Content,
                MaxSearchResponseBytes,
                "Search response",
                cancellationToken);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                MaxAutomaticRedirections = 5,
            })
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-WebSearch/1.0");
            return client;
        }

        private sealed record SearchProvider(
            string Name,
            Func<string, string> BuildUrl,
            Func<string, IReadOnlyList<CopilotWebSearchHit>> ExtractHits);
    }
}
