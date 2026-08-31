using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        private const string Nat64DiscoveryHost = "ipv4only.arpa.";
        private static readonly int[] Nat64PrefixLengths = [32, 40, 48, 56, 64, 96];
        private static readonly Regex HttpUrlRegex = new("https?://[^\\s\\\"'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] UrlTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '\uFF0C', '\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', '\uFF09', '\u3011', '\u300B', '\u3001' };
        private static readonly HashSet<string> NonPageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".7z", ".avi", ".bmp", ".css", ".csv", ".doc", ".docx", ".exe", ".gif", ".gz",
            ".ico", ".jpeg", ".jpg", ".js", ".mp3", ".mp4", ".pdf", ".png", ".ppt", ".pptx",
            ".rar", ".svg", ".tar", ".webm", ".webp", ".woff", ".woff2", ".xls", ".xlsx", ".zip",
        };
        private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

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

        public static Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken)
        {
            return LoadWebPageContentAsync(
                url,
                static (host, token) => Dns.GetHostAddressesAsync(host, token),
                static () => CreateHttpHandler(),
                static () => CopilotConfig.Instance.WebPagePref64Prefixes,
                cancellationToken);
        }

        internal static async Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(
            string url,
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            Func<HttpMessageHandler> createHttpHandler,
            Func<string?> getConfiguredPref64Prefixes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(resolveAddressesAsync);
            ArgumentNullException.ThrowIfNull(createHttpHandler);
            ArgumentNullException.ThrowIfNull(getConfiguredPref64Prefixes);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            cancellationToken = deadline.Token;
            var currentUri = NormalizeAndValidateWebPageUri(url);
            for (var redirectCount = 0; ; redirectCount++)
            {
                await EnsureAllowedWebPageUriAsync(
                    currentUri,
                    resolveAddressesAsync,
                    getConfiguredPref64Prefixes(),
                    cancellationToken);

                using var httpClient = CreateHttpClient(createHttpHandler());
                using var request = CreateWebPageRequestMessage(currentUri);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
            var validated = ValidateWebPageUri(resolved);
            if (string.Equals(currentUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(validated.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The web page redirect cannot downgrade an HTTPS connection to HTTP.");
            }
            return validated;
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
            ArgumentNullException.ThrowIfNull(uri);
            var rejectionReason = GetWebPageUriRejectionReason(uri);
            if (rejectionReason != null)
                throw new InvalidOperationException(rejectionReason);

            return uri;
        }

        internal static bool IsPotentiallyPublicWebPageUri(Uri? uri)
        {
            return GetWebPageUriRejectionReason(uri) == null;
        }

        private static string? GetWebPageUriRejectionReason(Uri? uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
                return "The web page URL is not valid.";
            if (uri.AbsoluteUri.Length > MaxWebPageUrlCharacters)
                return $"The web page URL exceeds the {MaxWebPageUrlCharacters:N0}-character limit.";
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return "Only http/https web page URLs are allowed.";
            }
            if (uri.Port is < 1 or > 65535)
                return "The web page URL port must be between 1 and 65535.";
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
                return "Web page URLs containing embedded credentials are not allowed.";

            var normalizedHost = NormalizeWebPageHostForPolicy(uri.IdnHost);
            if (string.IsNullOrWhiteSpace(normalizedHost))
                return "The web page URL host is not valid.";
            if (uri.IsLoopback || IsLocalhostWebPageHost(normalizedHost))
                return "Fetching localhost or loopback URLs is not allowed.";

            var parsedAddress = ParseWebPageAddress(normalizedHost);
            return parsedAddress != null && IsBlockedWebPageAddress(parsedAddress)
                ? "Fetching private, local, or reserved IP addresses is not allowed."
                : null;
        }

        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;
        }

        internal static async Task EnsureAllowedWebPageUriAsync(
            Uri uri,
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            string? configuredPref64Prefixes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(resolveAddressesAsync);
            var configuredNat64Prefixes = ParseConfiguredPref64Prefixes(configuredPref64Prefixes);
            var addresses = await ResolveWebPageAddressesAsync(
                uri.IdnHost,
                resolveAddressesAsync,
                cancellationToken);
            await EnsureAllowedResolvedWebPageAddressesAsync(
                addresses,
                resolveAddressesAsync,
                configuredNat64Prefixes,
                cancellationToken);
        }

        internal static async ValueTask<Stream> ConnectToAllowedWebPageHostAsync(
            DnsEndPoint endpoint,
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            Func<IPEndPoint, CancellationToken, ValueTask<Stream>> connectAsync,
            string? configuredPref64Prefixes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(resolveAddressesAsync);
            ArgumentNullException.ThrowIfNull(connectAsync);
            if (string.IsNullOrWhiteSpace(endpoint.Host) || endpoint.Port is < 1 or > 65535)
                throw new InvalidOperationException("The web page connection endpoint is not valid.");
            var configuredNat64Prefixes = ParseConfiguredPref64Prefixes(configuredPref64Prefixes);

            var addresses = await ResolveWebPageAddressesAsync(
                endpoint.Host,
                resolveAddressesAsync,
                cancellationToken);
            await EnsureAllowedResolvedWebPageAddressesAsync(
                addresses,
                resolveAddressesAsync,
                configuredNat64Prefixes,
                cancellationToken);

            Exception? lastConnectionError = null;
            foreach (var address in addresses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await connectAsync(
                        new IPEndPoint(address, endpoint.Port),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lastConnectionError = ex;
                }
            }

            throw new HttpRequestException(
                "Could not connect to any validated target web page address.",
                lastConnectionError);
        }

        private static IReadOnlyList<WebPageNat64Prefix> ParseConfiguredPref64Prefixes(
            string? configuredPref64Prefixes)
        {
            if (CopilotWebPagePref64Configuration.TryParse(
                    configuredPref64Prefixes,
                    out var configuredNat64Prefixes,
                    out var pref64ConfigurationError))
            {
                return configuredNat64Prefixes;
            }

            throw new InvalidOperationException(
                $"The configured web page Pref64 prefixes are invalid: {pref64ConfigurationError}");
        }

        private static async Task EnsureAllowedResolvedWebPageAddressesAsync(
            IPAddress[] addresses,
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            IReadOnlyList<WebPageNat64Prefix> configuredNat64Prefixes,
            CancellationToken cancellationToken)
        {
            if (addresses.Length == 0)
                throw new InvalidOperationException("Could not resolve the target web page address.");
            if (addresses.Any(IsBlockedWebPageAddress))
                throw new InvalidOperationException("The target web page resolved to a local, private, or reserved IP address and was rejected.");
            if (!addresses.Any(static address => address.AddressFamily == AddressFamily.InterNetworkV6))
                return;

            var discoveredNat64Prefixes = await DiscoverNat64PrefixesAsync(resolveAddressesAsync, cancellationToken);
            var nat64Prefixes = MergeNat64Prefixes(configuredNat64Prefixes, discoveredNat64Prefixes);
            if (addresses.Any(address => IsBlockedNat64TranslatedWebPageAddress(address, nat64Prefixes)))
            {
                throw new InvalidOperationException(
                    "The target web page resolved through NAT64 to a local, private, or reserved IPv4 address and was rejected.");
            }
        }

        private static Task<IPAddress[]> ResolveWebPageAddressesAsync(
            string host,
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            CancellationToken cancellationToken)
        {
            var normalizedHost = NormalizeWebPageHostForPolicy(host);
            if (IsLocalhostWebPageHost(normalizedHost))
                return Task.FromResult(new[] { IPAddress.Loopback });

            var parsedAddress = ParseWebPageAddress(normalizedHost);
            if (parsedAddress != null)
                return Task.FromResult(new[] { parsedAddress });
            return resolveAddressesAsync(host, cancellationToken);
        }

        private static string NormalizeWebPageHostForPolicy(string host)
        {
            var normalized = (host ?? string.Empty).Trim();
            if (!IPAddress.TryParse(normalized, out _))
            {
                try
                {
                    normalized = new IdnMapping().GetAscii(normalized);
                }
                catch (ArgumentException)
                {
                    // DNS resolution remains guarded even when an invalid IDN cannot be normalized here.
                }
            }

            return normalized.TrimEnd('.');
        }

        private static bool IsLocalhostWebPageHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
        }

        private static IPAddress? ParseWebPageAddress(string host)
        {
            return IPAddress.TryParse(host, out var parsedAddress) ? parsedAddress : null;
        }

        private static bool IsBlockedWebPageAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
                return true;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv4-mapped values are API representations, not globally reachable IPv6 destinations.
                if (address.IsIPv4MappedToIPv6)
                    return true;
                if (address.Equals(IPAddress.IPv6Any)
                    || address.IsIPv6LinkLocal
                    || address.IsIPv6SiteLocal
                    || address.IsIPv6Multicast)
                    return true;

                var bytes = address.GetAddressBytes();
                if (bytes.Length != 16)
                    return true;

                // RFC 6052's well-known NAT64 prefix carries IPv4 in the final 32 bits.
                // Network-specific prefixes are discovered and checked at the connection boundary.
                if (bytes is [0x00, 0x64, 0xFF, 0x9B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, ..])
                    return IsBlockedWebPageAddress(new IPAddress(bytes.AsSpan(12, 4)));

                return !IsAllowedGlobalUnicastWebPageAddress(bytes);
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return true;

            var bytesV4 = address.GetAddressBytes();
            if (bytesV4.Length != 4)
                return true;

            return IsBlockedIpv4WebPageAddress(bytesV4);
        }

        private static bool IsBlockedIpv4WebPageAddress(ReadOnlySpan<byte> bytesV4)
        {
            // IANA special-purpose snapshot 2025-10-09. Protocol-only anycast blocks are not web targets.
            // Azure WireServer is also host-local inside Azure despite using an otherwise public address.
            return bytesV4[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                168 when bytesV4[1] == 63 && bytesV4[2] == 129 && bytesV4[3] == 16 => true,
                169 when bytesV4[1] == 254 => true,
                172 when bytesV4[1] >= 16 && bytesV4[1] <= 31 => true,
                192 when bytesV4[1] == 168 => true,
                192 when bytesV4[1] == 0 && bytesV4[2] == 0 => true,
                192 when bytesV4[1] == 0 && bytesV4[2] == 2 => true,
                192 when bytesV4[1] == 31 && bytesV4[2] == 196 => true,
                192 when bytesV4[1] == 52 && bytesV4[2] == 193 => true,
                192 when bytesV4[1] == 88 && bytesV4[2] == 99 => true,
                192 when bytesV4[1] == 175 && bytesV4[2] == 48 => true,
                198 when bytesV4[1] is 18 or 19 => true,
                198 when bytesV4[1] == 51 && bytesV4[2] == 100 => true,
                203 when bytesV4[1] == 0 && bytesV4[2] == 113 => true,
                100 when bytesV4[1] >= 64 && bytesV4[1] <= 127 => true,
                >= 224 => true,
                _ => false,
            };
        }

        private static async Task<IReadOnlyList<WebPageNat64Prefix>> DiscoverNat64PrefixesAsync(
            Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
            CancellationToken cancellationToken)
        {
            IPAddress[] discoveryAddresses;
            try
            {
                discoveryAddresses = await resolveAddressesAsync(Nat64DiscoveryHost, cancellationToken);
            }
            catch (SocketException)
            {
                return Array.Empty<WebPageNat64Prefix>();
            }

            return ExtractNat64Prefixes(discoveryAddresses);
        }

        private static IReadOnlyList<WebPageNat64Prefix> MergeNat64Prefixes(
            IReadOnlyList<WebPageNat64Prefix> configuredPrefixes,
            IReadOnlyList<WebPageNat64Prefix> discoveredPrefixes)
        {
            if (configuredPrefixes.Count == 0)
                return discoveredPrefixes;
            if (discoveredPrefixes.Count == 0)
                return configuredPrefixes;

            var merged = configuredPrefixes.ToList();
            foreach (var discoveredPrefix in discoveredPrefixes)
            {
                if (!merged.Any(prefix => prefix.Length == discoveredPrefix.Length
                    && prefix.Bytes.AsSpan().SequenceEqual(discoveredPrefix.Bytes)))
                {
                    merged.Add(discoveredPrefix);
                }
            }

            return merged;
        }

        private static WebPageNat64Prefix[] ExtractNat64Prefixes(IEnumerable<IPAddress> discoveryAddresses)
        {
            var candidates = new List<WebPageNat64PrefixCandidate>();
            Span<byte> embeddedIpv4 = stackalloc byte[4];
            var discoveryAddressIndex = 0;
            foreach (var address in discoveryAddresses)
            {
                if (address.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    discoveryAddressIndex++;
                    continue;
                }

                var bytes = address.GetAddressBytes();
                foreach (var prefixLength in Nat64PrefixLengths)
                {
                    if (!TryExtractNat64Ipv4Address(bytes, prefixLength, embeddedIpv4)
                        || !IsNat64DiscoveryIpv4Address(embeddedIpv4))
                    {
                        continue;
                    }

                    var prefixBytes = bytes.AsSpan(0, prefixLength / 8).ToArray();
                    var candidate = candidates.FirstOrDefault(prefix => prefix.Length == prefixLength
                        && prefix.Bytes.AsSpan().SequenceEqual(prefixBytes));
                    if (candidate == null)
                    {
                        candidate = new WebPageNat64PrefixCandidate(prefixBytes, prefixLength);
                        candidates.Add(candidate);
                    }

                    candidate.DiscoveryIpv4LastOctets.Add(embeddedIpv4[3]);
                    candidate.DiscoveryAddressIndexes.Add(discoveryAddressIndex);
                }

                discoveryAddressIndex++;
            }

            var corroboratedCandidates = candidates
                .Where(static candidate => candidate.DiscoveryIpv4LastOctets.Count == 2)
                .ToArray();
            if (corroboratedCandidates.Length == 0)
            {
                // A resolver may return only one of the two discovery addresses. Keep every
                // standard-layout candidate in that case so ambiguity cannot remove the real prefix.
                return candidates
                    .Select(static candidate => candidate.ToPrefix())
                    .ToArray();
            }

            var corroboratedAddressIndexes = corroboratedCandidates
                .SelectMany(static candidate => candidate.DiscoveryAddressIndexes)
                .ToHashSet();
            return candidates
                .Where(candidate => candidate.DiscoveryIpv4LastOctets.Count == 2
                    || candidate.DiscoveryAddressIndexes.Any(index => !corroboratedAddressIndexes.Contains(index)))
                .Select(static candidate => candidate.ToPrefix())
                .ToArray();
        }

        private static bool IsBlockedNat64TranslatedWebPageAddress(
            IPAddress address,
            IReadOnlyList<WebPageNat64Prefix> prefixes)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6 || prefixes.Count == 0)
                return false;

            var bytes = address.GetAddressBytes();
            Span<byte> embeddedIpv4 = stackalloc byte[4];
            foreach (var prefix in prefixes)
            {
                if (!bytes.AsSpan(0, prefix.Bytes.Length).SequenceEqual(prefix.Bytes))
                    continue;

                // A configured/discovered Pref64 is deny-only. Once it matches, malformed
                // RFC 6052 layout (including a non-zero u octet) must not fall back to being
                // treated as an unrelated public IPv6 address.
                if (!TryExtractNat64Ipv4Address(bytes, prefix.Length, embeddedIpv4)
                    || IsBlockedIpv4WebPageAddress(embeddedIpv4))
                    return true;
            }

            return false;
        }

        private static bool TryExtractNat64Ipv4Address(
            ReadOnlySpan<byte> ipv6Bytes,
            int prefixLength,
            Span<byte> destination)
        {
            if (ipv6Bytes.Length != 16 || destination.Length < 4 || !Nat64PrefixLengths.Contains(prefixLength))
                return false;
            if (ipv6Bytes[8] != 0)
                return false;

            var prefixBytes = prefixLength / 8;
            for (var index = 0; index < 4; index++)
            {
                var sourceIndex = prefixBytes + index;
                if (prefixLength < 96 && sourceIndex >= 8)
                    sourceIndex++;
                destination[index] = ipv6Bytes[sourceIndex];
            }

            return true;
        }

        private static bool IsNat64DiscoveryIpv4Address(ReadOnlySpan<byte> address)
        {
            return address.Length == 4
                && address[0] == 192
                && address[1] == 0
                && address[2] == 0
                && address[3] is 170 or 171;
        }

        private static bool IsAllowedGlobalUnicastWebPageAddress(ReadOnlySpan<byte> bytes)
        {
            // Compressed from IANA's ALLOCATED IPv6 global-unicast rows (2025-10-10 snapshot).
            // Protocol-specific 2001::/23 and 6to4 2002::/16 are intentionally not web targets.
            var firstHextet = (bytes[0] << 8) | bytes[1];
            var secondHextet = (bytes[2] << 8) | bytes[3];

            if (firstHextet == 0x2001)
            {
                return (secondHextet is >= 0x0200 and <= 0x0FFF && secondHextet != 0x0DB8)
                    || secondHextet is >= 0x1200 and <= 0x4DFF
                    || secondHextet is >= 0x5000 and <= 0x5FFF
                    || secondHextet is >= 0x8000 and <= 0xBFFF;
            }

            return firstHextet switch
            {
                0x2003 => secondHextet <= 0x3FFF,
                >= 0x2400 and <= 0x241F => true,
                >= 0x2600 and <= 0x260F => true,
                0x2610 => secondHextet <= 0x01FF,
                0x2620 => secondHextet <= 0x01FF,
                >= 0x2630 and <= 0x263F => true,
                >= 0x2800 and <= 0x280F => true,
                >= 0x2A00 and <= 0x2A1F => true,
                >= 0x2C00 and <= 0x2C0F => true,
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

        private static HttpClient CreateHttpClient(HttpMessageHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-Agent/1.0");
            return client;
        }

        internal static SocketsHttpHandler CreateHttpHandler()
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                // Production owns a fresh handler per outgoing request. Zero lifetimes keep
                // the same invariant if this handler is accidentally reused by another caller.
                PooledConnectionIdleTimeout = TimeSpan.Zero,
                PooledConnectionLifetime = TimeSpan.Zero,
                UseCookies = false,
                UseProxy = false,
                ConnectCallback = ConnectToAllowedWebPageHostAsync,
            };
        }

        internal static HttpRequestMessage CreateWebPageRequestMessage(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            // Prefer HTTP/2 for modern and h2-only HTTPS origins, while retaining HTTP/1.1
            // fallback. A fresh handler/client owns each outgoing request, so negotiation
            // cannot reintroduce a connection validated under an earlier DNS/Pref64 policy.
            var request = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            };
            return request;
        }

        private static ValueTask<Stream> ConnectToAllowedWebPageHostAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            return ConnectToAllowedWebPageHostAsync(
                context.DnsEndPoint,
                static (host, token) => Dns.GetHostAddressesAsync(host, token),
                ConnectSocketAsync,
                CopilotConfig.Instance.WebPagePref64Prefixes,
                cancellationToken);
        }

        private static async ValueTask<Stream> ConnectSocketAsync(
            IPEndPoint endpoint,
            CancellationToken cancellationToken)
        {
            var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(endpoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private sealed class WebPageNat64PrefixCandidate
        {
            public byte[] Bytes { get; }

            public int Length { get; }

            public HashSet<byte> DiscoveryIpv4LastOctets { get; } = new();

            public HashSet<int> DiscoveryAddressIndexes { get; } = new();

            public WebPageNat64PrefixCandidate(byte[] bytes, int length)
            {
                Bytes = bytes;
                Length = length;
            }

            public WebPageNat64Prefix ToPrefix() => new(Bytes, Length);
        }
    }
}
