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
        private static readonly char[] UrlTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '，', '。', '；', '：', '！', '？', '）', '】', '》', '、' };
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
                throw new InvalidOperationException($"目标地址返回的内容类型不受支持：{mediaType}");
            }

            var html = await ReadWebPageContentAsync(response, cancellationToken);
            return ExtractWebPageContent(uri, html);
        }

        public static string BuildFetchedWebPageContextBlock(CopilotFetchedWebPageContent page)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[网页抓取成功] {page.Url}");
            builder.AppendLine($"标题：{page.Title}");

            if (!string.IsNullOrWhiteSpace(page.Description))
                builder.AppendLine($"描述：{page.Description}");

            builder.AppendLine("正文：");
            builder.AppendLine(page.Content);
            return builder.ToString().TrimEnd();
        }

        public static string BuildFailedWebPageContextBlock(string url, string failureMessage)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"[网页抓取失败] {url}",
                $"失败原因：{failureMessage}",
                "应用未能抓取到真实网页内容。回答时必须说明无法基于真实网页内容继续分析，不能假设网页中存在未抓取到的信息。",
            });
        }

        public static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(page.Description))
            {
                builder.AppendLine($"描述：{page.Description}");
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
                throw new InvalidOperationException("未能提取网页正文。当前网页可能需要脚本渲染。");

            if (content.Length > MaxWebPageContentChars)
                content = content[..MaxWebPageContentChars] + Environment.NewLine + $"...<内容已截断，仅保留前 {MaxWebPageContentChars} 字符。>";

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
                throw new InvalidOperationException("网页地址格式不正确。");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("只允许抓取 http/https 网页地址。");
            }

            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("禁止抓取 localhost 或本机回环地址。");

            return uri;
        }

        private static async Task EnsureAllowedWebPageUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(uri.Host, out var parsedAddress))
            {
                if (IsBlockedWebPageAddress(parsedAddress))
                    throw new InvalidOperationException("禁止抓取内网、本地或保留 IP 地址。");

                return;
            }

            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (addresses.Length == 0)
                throw new InvalidOperationException("无法解析目标网页地址。");

            if (addresses.Any(IsBlockedWebPageAddress))
                throw new InvalidOperationException("目标网页地址解析到了本地、内网或保留 IP，已拒绝访问。");
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
                    throw new InvalidOperationException($"网页内容超过大小限制（{MaxWebPageDownloadBytes / 1024} KB）。");

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