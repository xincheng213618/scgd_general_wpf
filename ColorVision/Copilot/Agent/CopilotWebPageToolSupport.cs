using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotWebPageLink(string Url, string Text);

    public readonly record struct CopilotFetchedWebPageContent(
        string Url,
        string Title,
        string Description,
        string Content,
        IReadOnlyList<string>? RelatedResourceUrls = null,
        bool IsSparseExtraction = false,
        IReadOnlyList<CopilotWebPageLink>? RelatedPageLinks = null)
    {
        public IReadOnlyList<string> DiscoveredResourceUrls => RelatedResourceUrls ?? Array.Empty<string>();

        public IReadOnlyList<CopilotWebPageLink> DiscoveredPageLinks => RelatedPageLinks ?? Array.Empty<CopilotWebPageLink>();
    }

    public static class CopilotWebPageToolSupport
    {
        public const int MaxWebPageDownloadBytes = 2 * 1024 * 1024;
        public const int MaxWebPageContentChars = 12000;
        public const int MaxWebPageRedirects = 5;
        public const int MaxDiscoveredPageLinks = 12;
        public const int MaxWebPageUrlCharacters = 8192;

        private static readonly Regex HttpUrlRegex = new("https?://[^\\s\\\"'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] UrlTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '\uFF0C', '\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', '\uFF09', '\u3011', '\u300B', '\u3001' };
        private static readonly HashSet<string> NonPageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".7z", ".avi", ".bmp", ".css", ".csv", ".doc", ".docx", ".exe", ".gif", ".gz",
            ".ico", ".jpeg", ".jpg", ".js", ".mp3", ".mp4", ".pdf", ".png", ".ppt", ".pptx",
            ".rar", ".svg", ".tar", ".webm", ".webp", ".woff", ".woff2", ".xls", ".xlsx", ".zip",
        };
        private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public static List<string> ExtractHttpUrls(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return results;

            foreach (Match match in HttpUrlRegex.Matches(text))
            {
                var candidate = match.Value.Trim().TrimEnd(UrlTrimCharacters);
                if (!string.IsNullOrWhiteSpace(candidate)
                    && !results.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        public static string NormalizeWebPageUrl(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(normalized, "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase))
            {
                normalized = "https://" + normalized;
            }

            return normalized;
        }

        public static async Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken)
        {
            var currentUri = NormalizeAndValidateWebPageUri(url);
            for (var redirectCount = 0; ; redirectCount++)
            {
                await EnsureAllowedWebPageUriAsync(currentUri, cancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (IsRedirectStatusCode(response.StatusCode))
                {
                    if (redirectCount >= MaxWebPageRedirects)
                        throw new InvalidOperationException($"The web page exceeded the redirect limit ({MaxWebPageRedirects}).");
                    currentUri = ResolveRedirectWebPageUri(currentUri, response.Headers.Location);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!IsSupportedWebContentType(mediaType))
                    throw new InvalidOperationException($"The target URL returned an unsupported content type: {mediaType}");

                var content = await ReadWebPageContentAsync(response, cancellationToken);
                return ExtractDownloadedContent(currentUri, mediaType, content);
            }
        }

        public static Uri ResolveRedirectWebPageUri(Uri currentUri, Uri? location)
        {
            ArgumentNullException.ThrowIfNull(currentUri);
            if (location == null)
                throw new InvalidOperationException("The web page returned a redirect without a Location header.");

            var resolved = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
            return ValidateWebPageUri(resolved);
        }

        public static string BuildFetchedWebPageContextBlock(CopilotFetchedWebPageContent page)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[Web Page Fetched] {page.Url}");
            builder.AppendLine($"Title: {page.Title}");

            if (!string.IsNullOrWhiteSpace(page.Description))
                builder.AppendLine($"Description: {page.Description}");

            if (page.IsSparseExtraction)
                builder.AppendLine("Extraction note: The downloaded page was large but exposed very little static text; it likely relies on script-rendered data.");

            if (page.DiscoveredResourceUrls.Count > 0)
            {
                builder.AppendLine("Discovered same-origin data resources:");
                foreach (var relatedUrl in page.DiscoveredResourceUrls)
                    builder.Append("- ").AppendLine(relatedUrl);
            }

            AppendDiscoveredPageLinks(builder, page.DiscoveredPageLinks);

            builder.AppendLine("Body:");
            builder.AppendLine(page.Content);
            return builder.ToString().TrimEnd();
        }

        public static string BuildFailedWebPageContextBlock(string url, string failureMessage)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"[Web Page Fetch Failed] {url}",
                $"Failure reason: {failureMessage}",
                "The application could not fetch real web page content. Do not assume unavailable page-specific facts, but answer from other available context or general knowledge when the question still allows it.",
            });
        }

        public static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(page.Description))
            {
                builder.AppendLine($"Description: {page.Description}");
                builder.AppendLine();
            }

            if (page.DiscoveredResourceUrls.Count > 0)
            {
                builder.AppendLine("Related data resources:");
                foreach (var relatedUrl in page.DiscoveredResourceUrls)
                    builder.Append("- ").AppendLine(relatedUrl);
                builder.AppendLine();
            }

            if (page.DiscoveredPageLinks.Count > 0)
            {
                AppendDiscoveredPageLinks(builder, page.DiscoveredPageLinks);
                builder.AppendLine();
            }

            builder.Append(page.Content);
            return builder.ToString();
        }

        public static CopilotFetchedWebPageContent ExtractDownloadedContent(Uri uri, string mediaType, string content)
        {
            ArgumentNullException.ThrowIfNull(uri);
            if (IsStructuredWebContentType(mediaType))
                return ExtractStructuredWebContent(uri, mediaType, content);
            return ExtractWebPageContent(uri, content);
        }

        internal static CopilotFetchedWebPageContent ExtractWebPageContent(Uri uri, string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);
            var relatedResourceUrls = ExtractRelatedResourceUrls(uri, document);
            var relatedPageLinks = ExtractRelatedPageLinks(uri, document);

            foreach (var removableNode in document.DocumentNode.SelectNodes("//script|//style|//noscript|//svg") ?? Enumerable.Empty<HtmlNode>())
            {
                removableNode.Remove();
            }

            var title = HtmlEntity.DeEntitize(document.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = uri.Host;

            var description = ExtractWebPageDescription(document);
            var bodyNode = document.DocumentNode.SelectSingleNode("//main")
                ?? document.DocumentNode.SelectSingleNode("//article")
                ?? document.DocumentNode.SelectSingleNode("//body")
                ?? document.DocumentNode;

            var lines = bodyNode
                .DescendantsAndSelf()
                .Where(node => node.NodeType == HtmlNodeType.Text)
                .Select(node => HtmlEntity.DeEntitize(node.InnerText))
                .Select(NormalizeWebPageLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var content = string.Join(Environment.NewLine, lines).Trim();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Could not extract readable web page body text. The page may require script rendering.");

            if (content.Length > MaxWebPageContentChars)
                content = content[..MaxWebPageContentChars] + Environment.NewLine + $"...<content truncated; kept the first {MaxWebPageContentChars} characters.>";

            var sparseExtraction = (html?.Length ?? 0) >= 20_000 && content.Length < 500;
            return new CopilotFetchedWebPageContent(uri.ToString(), title, description, content, relatedResourceUrls, sparseExtraction, relatedPageLinks);
        }

        private static CopilotFetchedWebPageContent ExtractStructuredWebContent(Uri uri, string mediaType, string content)
        {
            var normalized = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("The structured web resource was empty.");

            try
            {
                if (IsJsonContentType(mediaType))
                {
                    using var document = JsonDocument.Parse(normalized);
                    normalized = JsonSerializer.Serialize(document.RootElement, IndentedJsonOptions);
                }
                else
                {
                    normalized = XDocument.Parse(normalized, LoadOptions.PreserveWhitespace).ToString();
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The target URL returned malformed JSON.", ex);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new InvalidOperationException("The target URL returned malformed XML.", ex);
            }

            if (normalized.Length > MaxWebPageContentChars)
                normalized = TruncateStructuredWebContent(normalized);

            var title = Path.GetFileName(uri.AbsolutePath.TrimEnd('/'));
            if (string.IsNullOrWhiteSpace(title))
                title = uri.Host;
            return new CopilotFetchedWebPageContent(
                uri.ToString(),
                title,
                $"Structured web resource ({mediaType}).",
                normalized);
        }

        private static string TruncateStructuredWebContent(string content)
        {
            const string marker = "\n...<structured content truncated; preserved beginning and end.>\n";
            var retainedCharacters = MaxWebPageContentChars - marker.Length;
            var headCharacters = retainedCharacters * 2 / 3;
            var tailCharacters = retainedCharacters - headCharacters;
            return content[..headCharacters] + marker + content[^tailCharacters..];
        }

        private static List<string> ExtractRelatedResourceUrls(Uri pageUri, HtmlDocument document)
        {
            var results = new List<string>();
            var nodes = document.DocumentNode.SelectNodes("//a[@href]|//link[@href]") ?? Enumerable.Empty<HtmlNode>();
            foreach (var node in nodes)
            {
                var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", string.Empty)).Trim();
                if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(pageUri, href, out var candidate))
                    continue;
                if (!IsSameOrigin(pageUri, candidate) || !IsStructuredResourceLink(node, candidate))
                    continue;

                var normalized = candidate.GetLeftPart(UriPartial.Path);
                if (!results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    results.Add(normalized);
                if (results.Count >= 8)
                    break;
            }
            return results;
        }

        private static List<CopilotWebPageLink> ExtractRelatedPageLinks(Uri pageUri, HtmlDocument document)
        {
            var results = new List<CopilotWebPageLink>();
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentPageUrl = RemoveFragment(pageUri).AbsoluteUri;
            var nodes = document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>();
            foreach (var node in nodes)
            {
                var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", string.Empty)).Trim();
                if (string.IsNullOrWhiteSpace(href)
                    || href.Length > 2048
                    || node.Attributes["download"] != null
                    || !Uri.TryCreate(pageUri, href, out var candidate)
                    || !IsSameOrigin(pageUri, candidate)
                    || IsStructuredResourceLink(node, candidate)
                    || !IsBrowsablePageUri(candidate))
                {
                    continue;
                }

                var normalizedUri = RemoveFragment(candidate);
                var normalizedUrl = normalizedUri.AbsoluteUri;
                if (string.Equals(normalizedUrl, currentPageUrl, StringComparison.OrdinalIgnoreCase)
                    || !visitedUrls.Add(normalizedUrl))
                {
                    continue;
                }

                var label = NormalizeWebPageLine(node.InnerText);
                if (string.IsNullOrWhiteSpace(label))
                    label = NormalizeWebPageLine(node.GetAttributeValue("aria-label", string.Empty));
                if (string.IsNullOrWhiteSpace(label))
                    label = NormalizeWebPageLine(node.GetAttributeValue("title", string.Empty));
                if (string.IsNullOrWhiteSpace(label))
                    label = Path.GetFileName(normalizedUri.AbsolutePath.TrimEnd('/'));
                if (string.IsNullOrWhiteSpace(label))
                    label = normalizedUri.Host;
                if (label.Length > 160)
                    label = label[..159] + "…";

                results.Add(new CopilotWebPageLink(normalizedUrl, label));
                if (results.Count >= MaxDiscoveredPageLinks)
                    break;
            }

            return results;
        }

        private static bool IsBrowsablePageUri(Uri uri)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
                return true;
            return !NonPageExtensions.Contains(extension);
        }

        private static Uri RemoveFragment(Uri uri)
        {
            var builder = new UriBuilder(uri) { Fragment = string.Empty };
            return builder.Uri;
        }

        private static void AppendDiscoveredPageLinks(StringBuilder builder, IReadOnlyList<CopilotWebPageLink> links)
        {
            if (links == null || links.Count == 0)
                return;

            builder.AppendLine("Discovered same-origin pages (follow only when relevant):");
            foreach (var link in links.Take(MaxDiscoveredPageLinks))
                builder.Append("- ").Append(link.Text).Append(": ").AppendLine(link.Url);
        }

        private static bool IsStructuredResourceLink(HtmlNode node, Uri candidate)
        {
            var extension = Path.GetExtension(candidate.AbsolutePath);
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".rss", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".atom", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var relation = node.GetAttributeValue("rel", string.Empty);
            var type = node.GetAttributeValue("type", string.Empty);
            return relation.Contains("alternate", StringComparison.OrdinalIgnoreCase)
                && (IsStructuredWebContentType(type) || type.Contains("rss", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSameOrigin(Uri left, Uri right)
        {
            return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
                && left.Port == right.Port;
        }

        private static bool IsSupportedWebContentType(string mediaType)
        {
            return string.IsNullOrWhiteSpace(mediaType)
                || mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("text/plain", StringComparison.OrdinalIgnoreCase)
                || IsStructuredWebContentType(mediaType);
        }

        private static bool IsStructuredWebContentType(string mediaType)
        {
            return IsJsonContentType(mediaType)
                || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("rss", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("atom", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJsonContentType(string mediaType)
        {
            return mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractWebPageDescription(HtmlDocument document)
        {
            var descriptionNode = document.DocumentNode.SelectSingleNode("//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='description']")
                ?? document.DocumentNode.SelectSingleNode("//meta[translate(@property, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='og:description']")
                ?? document.DocumentNode.SelectSingleNode("//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='twitter:description']");

            return HtmlEntity.DeEntitize(descriptionNode?.GetAttributeValue("content", string.Empty) ?? string.Empty).Trim();
        }

        private static string NormalizeWebPageLine(string value)
        {
            var normalized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length == 0)
                return string.Empty;

            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        private static Uri NormalizeAndValidateWebPageUri(string url)
        {
            var normalized = NormalizeWebPageUrl(url);
            if (normalized.Length > MaxWebPageUrlCharacters)
                throw new InvalidOperationException($"The web page URL exceeds the {MaxWebPageUrlCharacters:N0}-character limit.");
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("The web page URL is not valid.");

            return ValidateWebPageUri(uri);
        }

        private static Uri ValidateWebPageUri(Uri uri)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only http/https web page URLs are allowed.");
            }

            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fetching localhost or loopback URLs is not allowed.");
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
                throw new InvalidOperationException("Web page URLs containing embedded credentials are not allowed.");

            return uri;
        }

        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;
        }

        private static async Task EnsureAllowedWebPageUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(uri.Host, out var parsedAddress))
            {
                if (IsBlockedWebPageAddress(parsedAddress))
                    throw new InvalidOperationException("Fetching private, local, or reserved IP addresses is not allowed.");

                return;
            }

            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (addresses.Length == 0)
                throw new InvalidOperationException("Could not resolve the target web page address.");

            if (addresses.Any(IsBlockedWebPageAddress))
                throw new InvalidOperationException("The target web page resolved to a local, private, or reserved IP address and was rejected.");
        }

        private static bool IsBlockedWebPageAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
                return true;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv4MappedToIPv6)
                    return IsBlockedWebPageAddress(address.MapToIPv4());
                if (address.Equals(IPAddress.IPv6Any)
                    || address.IsIPv6LinkLocal
                    || address.IsIPv6SiteLocal
                    || address.IsIPv6Multicast)
                    return true;

                var bytes = address.GetAddressBytes();
                return bytes.Length != 16
                    || (bytes[0] & 0xFE) == 0xFC
                    || bytes is [0x20, 0x01, 0x0D, 0xB8, ..];
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return true;

            var bytesV4 = address.GetAddressBytes();
            if (bytesV4.Length != 4)
                return true;

            return bytesV4[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 when bytesV4[1] == 254 => true,
                172 when bytesV4[1] >= 16 && bytesV4[1] <= 31 => true,
                192 when bytesV4[1] == 168 => true,
                192 when bytesV4[1] == 0 && bytesV4[2] == 0 => true,
                192 when bytesV4[1] == 0 && bytesV4[2] == 2 => true,
                192 when bytesV4[1] == 88 && bytesV4[2] == 99 => true,
                198 when bytesV4[1] is 18 or 19 => true,
                198 when bytesV4[1] == 51 && bytesV4[2] == 100 => true,
                203 when bytesV4[1] == 0 && bytesV4[2] == 113 => true,
                100 when bytesV4[1] >= 64 && bytesV4[1] <= 127 => true,
                >= 224 => true,
                _ => false,
            };
        }

        private static async Task<string> ReadWebPageContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                response.Content,
                MaxWebPageDownloadBytes,
                "Web page content",
                cancellationToken);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            })
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-Agent/1.0");
            return client;
        }
    }
}
