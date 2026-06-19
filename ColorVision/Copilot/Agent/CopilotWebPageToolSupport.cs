using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotFetchedWebPageContent(string Url, string Title, string Description, string Content);

    public static class CopilotWebPageToolSupport
    {
        public const int MaxWebPageDownloadBytes = 256 * 1024;
        public const int MaxWebPageContentChars = 12000;

        private static readonly Regex HttpUrlRegex = new("https?://[^\\s\\\"'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] UrlTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '\uFF0C', '\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', '\uFF09', '\u3011', '\u300B', '\u3001' };
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
                && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "https://" + normalized;
            }

            return normalized;
        }

        public static async Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken)
        {
            var uri = NormalizeAndValidateWebPageUri(url);
            await EnsureAllowedWebPageUriAsync(uri, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(mediaType)
                && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("text/plain", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The target URL returned an unsupported content type: {mediaType}");
            }

            var html = await ReadWebPageContentAsync(response, cancellationToken);
            return ExtractWebPageContent(uri, html);
        }

        public static string BuildFetchedWebPageContextBlock(CopilotFetchedWebPageContent page)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[Web Page Fetched] {page.Url}");
            builder.AppendLine($"Title: {page.Title}");

            if (!string.IsNullOrWhiteSpace(page.Description))
                builder.AppendLine($"Description: {page.Description}");

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
                "The application could not fetch real web page content. The answer must state that it cannot continue from actual page content and must not assume unavailable page information.",
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

            builder.Append(page.Content);
            return builder.ToString();
        }

        private static CopilotFetchedWebPageContent ExtractWebPageContent(Uri uri, string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);

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

            return new CopilotFetchedWebPageContent(uri.ToString(), title, description, content);
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
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("The web page URL is not valid.");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only http/https web page URLs are allowed.");
            }

            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fetching localhost or loopback URLs is not allowed.");

            return uri;
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
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                    return true;

                var bytes = address.GetAddressBytes();
                return bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC;
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
                100 when bytesV4[1] >= 64 && bytesV4[1] <= 127 => true,
                _ => false,
            };
        }

        private static async Task<string> ReadWebPageContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            var totalBytes = 0;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
                if (bytesRead <= 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > MaxWebPageDownloadBytes)
                    throw new InvalidOperationException($"Web page content exceeded the size limit ({MaxWebPageDownloadBytes / 1024} KB).");

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }

            buffer.Position = 0;
            using var reader = new StreamReader(buffer, Encoding.UTF8, true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-Agent/1.0");
            return client;
        }
    }
}
