#pragma warning disable CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotDocsSearchHit(
        string PageUrl,
        string PageTitle,
        string SectionTitle,
        string TitlePath,
        string RelativePath,
        string FullUrl,
        string Excerpt,
        int Score);

    internal sealed class CopilotDocsSearchQueryResult
    {
        public string Query { get; init; } = string.Empty;

        public string SearchIndexUrl { get; init; } = string.Empty;

        public IReadOnlyList<CopilotDocsSearchHit> Hits { get; init; } = Array.Empty<CopilotDocsSearchHit>();

        public int DistinctPageCount { get; init; }

        public int TotalMatchCount { get; init; }
    }

    internal static class CopilotDocsToolSupport
    {
        public const string PublishedDocsRootUrl = "https://xincheng213618.github.io/scgd_general_wpf/";
        public const string DocsSearchIndexUrl = PublishedDocsRootUrl + "docs-search-index.json";

        private const int MaxReturnedPages = 3;
        private const int MaxHitsPerPage = 2;
        private const int MaxExcerptChars = 360;
        private const int MaxSearchIndexBytes = 32 * 1024 * 1024;
        private static readonly TimeSpan SearchIndexCacheDuration = TimeSpan.FromMinutes(10);

        private static readonly string[] DirectIntentKeywords =
        {
            "colorvision",
            "视彩",
            "文档",
            "手册",
            "帮助",
            "说明",
            "教程",
            "指南",
            "软件",
        };

        private static readonly string[] ProductSurfaceKeywords =
        {
            "菜单",
            "选项",
            "主题",
            "语言",
            "插件",
            "设备",
            "相机",
            "校准",
            "smu",
            "主窗口",
            "界面",
            "快捷键",
            "搜索框",
            "首次运行",
            "安装",
            "启动",
            "图像编辑器",
            "工作流程",
            "流程",
            "终端",
            "属性编辑器",
            "日志",
            "导出",
            "导入",
            "数据库",
            "更新",
            "部署",
            "扩展",
            "开发",
            "架构",
            "api",
            "故障",
            "排查",
        };

        private static readonly string[] QuestionKeywords =
        {
            "怎么",
            "如何",
            "怎样",
            "为什么",
            "在哪",
            "哪里",
            "什么",
            "怎么办",
            "介绍",
            "使用",
            "打开",
            "配置",
            "设置",
            "查看",
            "排查",
            "说明",
            "教程",
            "失败",
            "报错",
            "错误",
            "异常",
            "无法",
            "不能",
            "找不到",
            "打不开",
            "没反应",
            "没生效",
            "guide",
            "help",
            "how",
            "where",
            "what",
        };

        private static readonly Regex SearchTermRegex = new(@"(?<term>[\u4e00-\u9fff]{2,16}|[A-Za-z0-9_][A-Za-z0-9_\.\-]{1,63})", RegexOptions.Compiled);
        private static readonly Regex NormalizationRegex = new(@"[\s\p{P}\p{S}]+", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly SemaphoreSlim SearchIndexLock = new(1, 1);

        private static CopilotDocsSearchIndex? _cachedSearchIndex;
        private static DateTimeOffset _cachedSearchIndexUtc = DateTimeOffset.MinValue;

        public static bool HasDocumentationIntent(string text)
        {
            var source = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
                return false;

            if (DirectIntentKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;

            var hasProductSurface = ProductSurfaceKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            var hasQuestion = QuestionKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            return hasProductSurface && hasQuestion;
        }

        public static string ResolveQuery(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var toolQuery = (toolInput?.Query ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(toolQuery))
                return toolQuery;

            return (request?.UserText ?? string.Empty).Trim();
        }

        public static async Task<CopilotDocsSearchQueryResult> SearchAsync(string query, CancellationToken cancellationToken)
        {
            var normalizedQuery = NormalizeSearchText(query);
            var terms = ExtractSearchTerms(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery) || terms.Count == 0)
            {
                return new CopilotDocsSearchQueryResult
                {
                    Query = query ?? string.Empty,
                    SearchIndexUrl = DocsSearchIndexUrl,
                };
            }

            var searchIndex = await LoadSearchIndexAsync(cancellationToken);
            var rankedHits = searchIndex.Entries
                .Select(entry => CreateHit(entry, normalizedQuery, terms))
                .Where(hit => hit.HasValue)
                .Select(hit => hit!.Value)
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => hit.FullUrl, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var selectedGroups = rankedHits
                .GroupBy(hit => hit.PageUrl, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Max(hit => hit.Score))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(MaxReturnedPages)
                .ToArray();

            var selectedHits = selectedGroups
                .SelectMany(group => group
                    .GroupBy(hit => hit.FullUrl, StringComparer.OrdinalIgnoreCase)
                    .Select(entryGroup => entryGroup.First())
                    .OrderByDescending(hit => hit.Score)
                    .ThenBy(hit => hit.FullUrl, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxHitsPerPage))
                .ToArray();

            return new CopilotDocsSearchQueryResult
            {
                Query = query ?? string.Empty,
                SearchIndexUrl = DocsSearchIndexUrl,
                Hits = selectedHits,
                DistinctPageCount = selectedGroups.Length,
                TotalMatchCount = rankedHits.Length,
            };
        }

        public static string BuildContextBlock(CopilotDocsSearchQueryResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var builder = new StringBuilder();
            builder.AppendLine("[Online Documentation Search]");
            builder.AppendLine($"Query: {result.Query}");
            builder.AppendLine($"Index URL: {result.SearchIndexUrl}");
            builder.AppendLine($"Matched Pages: {result.DistinctPageCount}");
            builder.AppendLine($"Candidate Snippets: {result.Hits.Count}");
            builder.AppendLine();

            var groupedHits = result.Hits
                .GroupBy(hit => hit.PageUrl, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (var pageIndex = 0; pageIndex < groupedHits.Length; pageIndex++)
            {
                var group = groupedHits[pageIndex];
                var firstHit = group.First();
                builder.Append(pageIndex + 1)
                    .Append(". Page: ")
                    .AppendLine(firstHit.PageTitle);
                builder.Append("   Section: ")
                    .AppendLine(firstHit.SectionTitle);
                builder.Append("   Page URL: ")
                    .AppendLine(firstHit.PageUrl);
                builder.Append("   Source Path: ")
                    .AppendLine(firstHit.RelativePath);

                foreach (var hit in group)
                {
                    builder.Append("   - Title Path: ")
                        .AppendLine(hit.TitlePath);
                    builder.Append("     Snippet URL: ")
                        .AppendLine(hit.FullUrl);
                    builder.Append("     Excerpt: ")
                        .AppendLine(hit.Excerpt);
                }

                if (pageIndex < groupedHits.Length - 1)
                    builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static async Task<CopilotDocsSearchIndex> LoadSearchIndexAsync(CancellationToken cancellationToken)
        {
            if (_cachedSearchIndex != null && DateTimeOffset.UtcNow - _cachedSearchIndexUtc < SearchIndexCacheDuration)
                return _cachedSearchIndex;

            await SearchIndexLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedSearchIndex != null && DateTimeOffset.UtcNow - _cachedSearchIndexUtc < SearchIndexCacheDuration)
                    return _cachedSearchIndex;

                using var response = await HttpClient.GetAsync(
                    DocsSearchIndexUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException("The online documentation index has not been published yet. Confirm that the latest GitHub Pages deployment has completed.");

                response.EnsureSuccessStatusCode();
                var json = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                    response.Content,
                    MaxSearchIndexBytes,
                    "Documentation search index",
                    cancellationToken);
                CopilotDocsSearchIndex? searchIndex;
                try
                {
                    searchIndex = JsonSerializer.Deserialize<CopilotDocsSearchIndex>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"The online documentation index format could not be recognized: {ex.Message}", ex);
                }

                if (searchIndex?.Entries == null || searchIndex.Entries.Count == 0)
                    throw new InvalidOperationException("The online documentation index is empty, so documentation questions cannot be answered right now.");

                _cachedSearchIndex = searchIndex;
                _cachedSearchIndexUtc = DateTimeOffset.UtcNow;
                return searchIndex;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("The online documentation index has not been published yet. Confirm that the latest GitHub Pages deployment has completed.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Could not access the online documentation index: {ex.Message}", ex);
            }
            finally
            {
                SearchIndexLock.Release();
            }
        }

        private static CopilotDocsSearchHit? CreateHit(CopilotDocsSearchEntry entry, string normalizedQuery, IReadOnlyList<string> terms)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Url))
                return null;

            var title = NormalizeSearchText(entry.Title);
            var titlePath = NormalizeSearchText(BuildTitlePath(entry));
            var text = NormalizeSearchText(string.IsNullOrWhiteSpace(entry.Text) ? entry.Summary : entry.Text);
            var sectionTitle = NormalizeSearchText(entry.SectionTitle);
            var relativePath = entry.RelativePath ?? string.Empty;

            var score = 0;
            if (title.Contains(normalizedQuery, StringComparison.Ordinal))
                score += 220;

            if (!string.Equals(titlePath, title, StringComparison.Ordinal) && titlePath.Contains(normalizedQuery, StringComparison.Ordinal))
                score += 140;

            if (text.Contains(normalizedQuery, StringComparison.Ordinal))
                score += 90;

            if (sectionTitle.Contains(normalizedQuery, StringComparison.Ordinal))
                score += 30;

            foreach (var term in terms)
            {
                if (title.Equals(term, StringComparison.Ordinal))
                    score += 140;
                else if (title.Contains(term, StringComparison.Ordinal))
                    score += 70;

                if (titlePath.Contains(term, StringComparison.Ordinal))
                    score += 45;

                if (text.Contains(term, StringComparison.Ordinal))
                    score += 18;

                if (!string.IsNullOrWhiteSpace(relativePath) && relativePath.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score += 12;
            }

            score += string.Equals(entry.Kind, "section", StringComparison.OrdinalIgnoreCase) ? 12 : 4;
            if (score <= 0)
                return null;

            var pageRelativeUrl = GetPageRelativeUrl(entry.Url);
            return new CopilotDocsSearchHit(
                BuildFullUrl(pageRelativeUrl),
                ResolvePageTitle(entry),
                entry.SectionTitle ?? string.Empty,
                BuildTitlePath(entry),
                entry.RelativePath ?? string.Empty,
                BuildFullUrl(entry.Url),
                CreateExcerpt(string.IsNullOrWhiteSpace(entry.Summary) ? entry.Text : entry.Summary),
                score);
        }

        private static string ResolvePageTitle(CopilotDocsSearchEntry entry)
        {
            if (entry.Titles != null && entry.Titles.Length > 0 && !string.IsNullOrWhiteSpace(entry.Titles[0]))
                return entry.Titles[0];

            return entry.Title ?? string.Empty;
        }

        private static string BuildTitlePath(CopilotDocsSearchEntry entry)
        {
            var titles = entry.Titles ?? Array.Empty<string>();
            var parts = titles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .ToList();

            if (!string.IsNullOrWhiteSpace(entry.Title) && (parts.Count == 0 || !string.Equals(parts[^1], entry.Title, StringComparison.OrdinalIgnoreCase)))
                parts.Add(entry.Title);

            if (parts.Count == 0)
                return entry.Title ?? string.Empty;

            return string.Join(" > ", parts);
        }

        private static string BuildFullUrl(string relativeUrl)
        {
            var normalized = (relativeUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return PublishedDocsRootUrl;

            return new Uri(new Uri(PublishedDocsRootUrl), normalized.TrimStart('/')).ToString();
        }

        private static string GetPageRelativeUrl(string url)
        {
            var normalized = (url ?? string.Empty).Trim();
            var hashIndex = normalized.IndexOf('#');
            return hashIndex >= 0 ? normalized[..hashIndex] : normalized;
        }

        private static string CreateExcerpt(string value)
        {
            var excerpt = NormalizeWhitespace(value);
            if (excerpt.Length <= MaxExcerptChars)
                return excerpt;

            return excerpt[..MaxExcerptChars].TrimEnd() + "...";
        }

        private static IReadOnlyList<string> ExtractSearchTerms(string query)
        {
            var normalizedQuery = NormalizeSearchText(query);
            var terms = new List<string>();
            if (!string.IsNullOrWhiteSpace(normalizedQuery))
                terms.Add(normalizedQuery);

            foreach (Match match in SearchTermRegex.Matches(query ?? string.Empty))
            {
                var term = NormalizeSearchText(match.Groups["term"].Value);
                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                    continue;

                terms.Add(term);
            }

            return terms
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(term => term.Length)
                .Take(8)
                .ToArray();
        }

        private static string NormalizeSearchText(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            normalized = NormalizationRegex.Replace(normalized, " ");
            return NormalizeWhitespace(normalized);
        }

        private static string NormalizeWhitespace(string value)
        {
            return string.Join(" ", (value ?? string.Empty)
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            })
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot-Docs/1.0");
            return client;
        }

        private sealed class CopilotDocsSearchIndex
        {
            public string BasePath { get; set; } = string.Empty;

            public List<CopilotDocsSearchEntry> Entries { get; set; } = new();
        }

        private sealed class CopilotDocsSearchEntry
        {
            public string Id { get; set; } = string.Empty;

            public string Kind { get; set; } = string.Empty;

            public string SectionKey { get; set; } = string.Empty;

            public string SectionTitle { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string[] Titles { get; set; } = Array.Empty<string>();

            public string Text { get; set; } = string.Empty;

            public string Summary { get; set; } = string.Empty;

            public string Url { get; set; } = string.Empty;

            public string RelativePath { get; set; } = string.Empty;
        }
    }
}
