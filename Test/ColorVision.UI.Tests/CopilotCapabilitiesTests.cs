#pragma warning disable CA1707,CA1861
using ColorVision.Copilot;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCapabilitiesTests : IDisposable
{
    private readonly string _tempRoot;

    public CopilotCapabilitiesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVisionCopilotCapabilities_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void SearchFiles_WithMcpPlainTerms_FindsMatchingFile()
    {
        var filePath = Path.Combine(_tempRoot, "DeviceCamera.cs");
        File.WriteAllText(filePath, "public sealed class DeviceCamera { }");

        var result = CopilotSearchFilesCapability.Search(
            new[] { _tempRoot },
            "DeviceCamera",
            fallbackText: null,
            allowPlainSearchTerms: true,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Matches, match => string.Equals(match.FullPath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchFiles_AgentStyleWithoutFileIntent_KeepsExistingBehavior()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "DeviceCamera.cs"), "public sealed class DeviceCamera { }");

        var result = CopilotSearchFilesCapability.Search(
            new[] { _tempRoot },
            query: null,
            fallbackText: "DeviceCamera",
            allowPlainSearchTerms: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.Terms);
    }

    [Fact]
    public void GrepText_FindsMatchingLineAndSuggestedFile()
    {
        var filePath = Path.Combine(_tempRoot, "FlowNode.cs");
        File.WriteAllLines(filePath, new[]
        {
            "public sealed class FlowNode",
            "{",
            "    public string NodeName { get; set; } = string.Empty;",
            "}",
        });

        var result = CopilotGrepTextCapability.Search(
            new[] { _tempRoot },
            "NodeName",
            fallbackText: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Matches, match => match.LineNumber == 3 && match.LineText.Contains("NodeName", StringComparison.Ordinal));
        Assert.Contains(filePath, result.SuggestedReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GrepText_SkipsOversizedTextFiles()
    {
        var filePath = Path.Combine(_tempRoot, "oversized.log");
        File.WriteAllText(filePath, new string('x', 9 * 1024 * 1024) + "UNIQUE_MATCH_TERM");

        var result = CopilotGrepTextCapability.Search(
            new[] { _tempRoot },
            "UNIQUE_MATCH_TERM",
            fallbackText: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.ScannedTextFileCount);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void GrepText_PropagatesCancellationDuringFileScan()
    {
        var filePath = Path.Combine(_tempRoot, "many-lines.log");
        using (var writer = new StreamWriter(filePath))
        {
            for (var index = 0; index < 100_000; index++)
                writer.WriteLine("a searchable line without the requested value");
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        Assert.Throws<OperationCanceledException>(() => CopilotGrepTextCapability.Search(
            new[] { _tempRoot },
            "NEVER_MATCH_THIS_TERM",
            fallbackText: null,
            cancellation.Token));
    }

    [Fact]
    public void GrepText_WithChineseQuestion_FindsRelevantChineseTerms()
    {
        var filePath = Path.Combine(_tempRoot, "Calibration.md");
        File.WriteAllText(filePath, "畸变校正（Distortion）：镜头径向/切向畸变矫正。");

        var result = CopilotGrepTextCapability.Search(
            new[] { _tempRoot },
            "畸变校正是怎么实现的？",
            fallbackText: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("畸变校正", result.Patterns, StringComparer.Ordinal);
        Assert.Contains(result.Matches, match => string.Equals(match.FullPath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WebSearch_ExtractsDuckDuckGoResultUrls()
    {
        const string html = """
            <html>
              <body>
                <div class="result">
                  <a class="result__a" href="/l/?kh=-1&amp;uddg=https%3A%2F%2Fexample.com%2Fopencv-camera-calibration">OpenCV camera calibration</a>
                  <a class="result__snippet">Use intrinsic parameters and distortion coefficients.</a>
                </div>
              </body>
            </html>
            """;

        var hits = CopilotWebSearchCapability.ExtractDuckDuckGoHits(html);

        var hit = Assert.Single(hits);
        Assert.Equal("OpenCV camera calibration", hit.Title);
        Assert.Equal("https://example.com/opencv-camera-calibration", hit.Url);
        Assert.Contains("distortion coefficients", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BoundedHttpContentReader_DecodesDeclaredCharset()
    {
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes("café déjà vu"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain")
        {
            CharSet = "iso-8859-1",
        };

        var result = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
            content,
            maximumBytes: 1024,
            contentLabel: "Test response",
            CancellationToken.None);

        Assert.Equal("café déjà vu", result);
    }

    [Fact]
    public async Task BoundedHttpContentReader_RejectsBodyLargerThanDeclaredLimit()
    {
        using var content = new ByteArrayContent(new byte[1025]);
        content.Headers.ContentLength = 1;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotBoundedHttpContentReader.ReadAsStringAsync(
                content,
                maximumBytes: 1024,
                contentLabel: "Test response",
                CancellationToken.None));

        Assert.Contains("size limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BoundedHttpContentReader_PropagatesCancellationBeforeRead()
    {
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes("content"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CopilotBoundedHttpContentReader.ReadAsStringAsync(
                content,
                maximumBytes: 1024,
                contentLabel: "Test response",
                cancellation.Token));
    }

    [Fact]
    public void WebSearch_ExtractsDuckDuckGoLiteResultsAndSnippets()
    {
        const string html = """
            <html><body><table>
              <tr><td>1.</td><td><a class="result-link" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fquota">Quota radar</a></td></tr>
              <tr><td></td><td class="result-snippet">Current quota and reset information.</td></tr>
              <tr><td>2.</td><td><a class="result-link" href="https://example.com/quota">Duplicate quota radar</a></td></tr>
            </table></body></html>
            """;

        var hits = CopilotWebSearchCapability.ExtractDuckDuckGoLiteHits(html);

        var hit = Assert.Single(hits);
        Assert.Equal("Quota radar", hit.Title);
        Assert.Equal("https://example.com/quota", hit.Url);
        Assert.Contains("reset information", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSearch_ExtractsBingRssResultsAndRejectsMalformedXml()
    {
        const string rss = """
            <rss><channel>
              <item><title>Codex &amp; Radar</title><link>https://codexradar.com/</link><description><![CDATA[Model and <b>quota</b> information.]]></description></item>
              <item><title>Duplicate</title><link>https://codexradar.com/</link><description>Duplicate result.</description></item>
              <item><title>Unsafe</title><link>file:///C:/secret.txt</link><description>Unsafe result.</description></item>
            </channel></rss>
            """;

        var hits = CopilotWebSearchCapability.ExtractBingRssHits(rss);

        var hit = Assert.Single(hits);
        Assert.Equal("Codex & Radar", hit.Title);
        Assert.Equal("https://codexradar.com/", hit.Url);
        Assert.Equal("Model and quota information.", hit.Snippet);
        Assert.Empty(CopilotWebSearchCapability.ExtractBingRssHits("<rss>"));
    }

    [Fact]
    public async Task WebSearch_DeepReadsResultMatchingRequestedSite()
    {
        var fetchedUrl = string.Empty;
        var searchResult = new CopilotWebSearchResult
        {
            Success = true,
            Query = "site:target.example useful information",
            Provider = "test",
            Summary = "Found two results.",
            Content = "[Web Search Results]\n1. Other\n   URL: https://other.example/\n2. Target\n   URL: https://target.example/article",
            Hits =
            [
                new CopilotWebSearchHit { Rank = 1, Title = "Other", Url = "https://other.example/" },
                new CopilotWebSearchHit { Rank = 2, Title = "Target", Url = "https://target.example/article" },
            ],
        };
        var tool = new CopilotWebSearchTool(
            (_, _) => Task.FromResult(searchResult),
            (_, url, _) =>
            {
                fetchedUrl = url;
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = "FetchUrl",
                    Success = true,
                    Summary = "Fetched 2/2 web resources.",
                    Content = "[Web Page Fetched] https://target.example/article\nTARGET-BODY\n\n[Web Page Fetched] https://target.example/data.json\nTARGET-DATA",
                });
            });

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "https://target.example/ 寻找有价值的信息", Mode = CopilotAgentMode.Auto },
            new CopilotAgentToolInput { Query = "site:target.example useful information" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://target.example/article", fetchedUrl);
        Assert.Contains("[Selected Search Result Deep Read] https://target.example/article", result.Content, StringComparison.Ordinal);
        Assert.Contains("TARGET-BODY", result.Content, StringComparison.Ordinal);
        Assert.Contains("TARGET-DATA", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebSearch_PreservesSearchLeadsWhenSelectedDeepReadFails()
    {
        var searchResult = new CopilotWebSearchResult
        {
            Success = true,
            Query = "current information",
            Provider = "test",
            Summary = "Found one result.",
            Content = "[Web Search Results]\n1. Result\n   URL: https://result.example/",
            Hits = [new CopilotWebSearchHit { Rank = 1, Title = "Result", Url = "https://result.example/" }],
        };
        var tool = new CopilotWebSearchTool(
            (_, _) => Task.FromResult(searchResult),
            (_, _, _) => Task.FromResult(new CopilotToolResult
            {
                ToolName = "FetchUrl",
                Success = false,
                Summary = "Fetch failed.",
                ErrorMessage = "blocked",
                FailureKind = CopilotToolFailureKind.Transient,
            }));

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "请联网搜索当前信息", Mode = CopilotAgentMode.Auto },
            new CopilotAgentToolInput { Query = "current information" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("https://result.example/", result.Content, StringComparison.Ordinal);
        Assert.Contains("Deep Read Unavailable", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("blocked", result.Content, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public async Task WebSearch_SkipsUnsafeResultBeforeDeepRead()
    {
        var fetchedUrl = string.Empty;
        var searchResult = new CopilotWebSearchResult
        {
            Success = true,
            Query = "useful information",
            Provider = "test",
            Summary = "Found two results.",
            Content = "[Web Search Results]\n1. Unsafe\n   URL: http://127.0.0.1/private\n2. Public\n   URL: https://public.example/article",
            Hits =
            [
                new CopilotWebSearchHit { Rank = 1, Title = "Unsafe", Url = "http://127.0.0.1/private" },
                new CopilotWebSearchHit { Rank = 2, Title = "Public", Url = "https://public.example/article" },
            ],
        };
        var tool = new CopilotWebSearchTool(
            (_, _) => Task.FromResult(searchResult),
            (_, url, _) =>
            {
                fetchedUrl = url;
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = "FetchUrl",
                    Success = true,
                    Summary = "Fetched public result.",
                    Content = "[Web Page Fetched] https://public.example/article\nPUBLIC-BODY",
                });
            });

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "请搜索有价值的信息", Mode = CopilotAgentMode.Web },
            new CopilotAgentToolInput { Query = "useful information" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://public.example/article", fetchedUrl);
        Assert.Contains("PUBLIC-BODY", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebPage_SparseSpaDiscoversOnlySameOriginStructuredResources()
    {
        var html = $$"""
            <html>
              <head>
                <title>Codex Radar</title>
                <link rel="alternate" type="application/json" href="/current.json">
                <script>{{new string('x', 25_000)}}</script>
              </head>
              <body>
                <main>Only static card</main>
                <a href="/feed.xml">RSS</a>
                <a href="/iq/full/#latest">Full benchmark history</a>
                <a href="/iq/full/">Duplicate benchmark link</a>
                <a href="/assets/chart.png">Chart image</a>
                <a href="https://other.example/private.json">External data</a>
              </body>
            </html>
            """;

        var page = CopilotWebPageToolSupport.ExtractDownloadedContent(
            new Uri("https://codexradar.com/"),
            "text/html",
            html);

        Assert.True(page.IsSparseExtraction);
        Assert.Contains("Only static card", page.Content, StringComparison.Ordinal);
        Assert.Contains("https://codexradar.com/current.json", page.DiscoveredResourceUrls, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("https://codexradar.com/feed.xml", page.DiscoveredResourceUrls, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(page.DiscoveredResourceUrls, url => url.Contains("other.example", StringComparison.OrdinalIgnoreCase));
        var pageLink = Assert.Single(page.DiscoveredPageLinks);
        Assert.Equal("https://codexradar.com/iq/full/", pageLink.Url);
        Assert.Equal("Full benchmark history", pageLink.Text);
        var context = CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page);
        Assert.Contains("Discovered same-origin pages (follow only when relevant)", context, StringComparison.Ordinal);
        Assert.Contains("Full benchmark history: https://codexradar.com/iq/full/", context, StringComparison.Ordinal);
        Assert.DoesNotContain("chart.png", context, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebPage_BoundsDiscoveredNavigationLinks()
    {
        var links = string.Join(Environment.NewLine, Enumerable.Range(1, 20)
            .Select(index => $"<a href=\"/section/{index}\">Section {index}</a>"));
        var page = CopilotWebPageToolSupport.ExtractDownloadedContent(
            new Uri("https://example.com/"),
            "text/html",
            $"<html><body><main>Home</main>{links}</body></html>");

        Assert.Equal(CopilotWebPageToolSupport.MaxDiscoveredPageLinks, page.DiscoveredPageLinks.Count);
        Assert.Equal("https://example.com/section/1", page.DiscoveredPageLinks[0].Url);
        Assert.Equal("https://example.com/section/12", page.DiscoveredPageLinks[^1].Url);
    }

    [Fact]
    public void WebPage_ExtractsJsonAndXmlResourcesAsReadableEvidence()
    {
        var jsonPage = CopilotWebPageToolSupport.ExtractDownloadedContent(
            new Uri("https://codexradar.com/current.json"),
            "application/json",
            """{"model_iq":{"latest":{"passed":7}}}""");
        var rssPage = CopilotWebPageToolSupport.ExtractDownloadedContent(
            new Uri("https://codexradar.com/feed.xml"),
            "application/rss+xml",
            """<rss><channel><title>Reset updates</title></channel></rss>""");

        Assert.Equal("current.json", jsonPage.Title);
        Assert.Contains("model_iq", jsonPage.Content, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, jsonPage.Content, StringComparison.Ordinal);
        Assert.Equal("feed.xml", rssPage.Title);
        Assert.Contains("Reset updates", rssPage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebPage_LongStructuredResourcePreservesBeginningAndEnd()
    {
        var json = $$"""{"head":"HEAD-SENTINEL","padding":"{{new string('x', 20_000)}}","tail":"TAIL-SENTINEL"}""";

        var page = CopilotWebPageToolSupport.ExtractDownloadedContent(
            new Uri("https://codexradar.com/current.json"),
            "application/json",
            json);

        Assert.True(page.Content.Length <= CopilotWebPageToolSupport.MaxWebPageContentChars);
        Assert.Contains("HEAD-SENTINEL", page.Content, StringComparison.Ordinal);
        Assert.Contains("TAIL-SENTINEL", page.Content, StringComparison.Ordinal);
        Assert.Contains("structured content truncated", page.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchUrl_FollowsDiscoveredStructuredResourceFromRichPage()
    {
        var loadedUrls = new List<string>();
        var tool = new CopilotFetchUrlTool((url, _) =>
        {
            loadedUrls.Add(url);
            return Task.FromResult(url.EndsWith("current.json", StringComparison.OrdinalIgnoreCase)
                ? new CopilotFetchedWebPageContent(url, "current.json", "Structured data", "QUOTA-DATA-SENTINEL")
                : new CopilotFetchedWebPageContent(
                    url,
                    "Codex Radar",
                    "Rich static page",
                    new string('p', 2_000),
                    ["https://codexradar.com/current.json"],
                    IsSparseExtraction: false));
        });

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "Inspect https://codexradar.com/" },
            new CopilotAgentToolInput { Query = "https://codexradar.com/" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, loadedUrls.Count);
        Assert.Equal("https://codexradar.com/", loadedUrls[0]);
        Assert.Equal("https://codexradar.com/current.json", loadedUrls[1]);
        Assert.Contains("Fetched 2/2 web resources", result.Summary, StringComparison.Ordinal);
        Assert.Contains("QUOTA-DATA-SENTINEL", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchUrl_PreservesRootEvidenceWhenDiscoveredResourceFails()
    {
        var tool = new CopilotFetchUrlTool((url, _) =>
        {
            if (url.EndsWith("current.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Structured resource unavailable.");

            return Task.FromResult(new CopilotFetchedWebPageContent(
                url,
                "Codex Radar",
                "Root summary",
                "ROOT-EVIDENCE-SENTINEL",
                ["https://codexradar.com/current.json"],
                IsSparseExtraction: false));
        });

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "Inspect https://codexradar.com/" },
            new CopilotAgentToolInput { Query = "https://codexradar.com/" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("Fetched 1/2 web resources", result.Summary, StringComparison.Ordinal);
        Assert.Contains("ROOT-EVIDENCE-SENTINEL", result.Content, StringComparison.Ordinal);
        Assert.Contains("Structured resource unavailable", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchUrl_BoundsAutomaticStructuredResourceDiscovery()
    {
        var loadedUrls = new List<string>();
        var tool = new CopilotFetchUrlTool((url, _) =>
        {
            loadedUrls.Add(url);
            return Task.FromResult(url.EndsWith('/')
                ? new CopilotFetchedWebPageContent(
                    url,
                    "Root",
                    "Root summary",
                    "Root content",
                    [
                        "https://example.com/one.json",
                        "https://example.com/two.json",
                        "https://example.com/three.json",
                        "https://example.com/four.json",
                    ],
                    IsSparseExtraction: false)
                : new CopilotFetchedWebPageContent(url, "Data", "Structured data", url));
        });

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest { UserText = "Inspect https://example.com/" },
            new CopilotAgentToolInput { Query = "https://example.com/" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, loadedUrls.Count);
        Assert.DoesNotContain("three.json", loadedUrls, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("four.json", loadedUrls, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebEvidenceSourceLedger_AppendsBoundedReturnedUrlsOnlyWhenMissing()
    {
        var steps = new[]
        {
            new CopilotAgentStepRecord
            {
                ToolCall = new CopilotToolCall { ToolName = "FetchUrl" },
                Observation = new CopilotToolObservation
                {
                    Success = true,
                    Content = string.Join('\n',
                        "Unsafe: http://127.0.0.1/private",
                        "[Web Page Fetched] https://example.com/",
                        "Discovered but unread: https://example.com/ignored.json",
                        "[Web Page Fetched] https://example.com/current.json",
                        "[Web Page Fetched] https://example.com/feed.xml",
                        "[Web Page Fetched] https://example.com/fourth.json"),
                },
            },
        };
        ICopilotTool[] tools = [new CopilotFetchUrlTool()];

        var appendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(steps, tools, "Evidence-based answer.");

        Assert.Contains("来源：", appendix, StringComparison.Ordinal);
        Assert.Contains("<https://example.com/>", appendix, StringComparison.Ordinal);
        Assert.Contains("<https://example.com/current.json>", appendix, StringComparison.Ordinal);
        Assert.Contains("<https://example.com/feed.xml>", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored.json", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("fourth.json", appendix, StringComparison.Ordinal);
        Assert.Equal(
            string.Empty,
            CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(
                steps,
                tools,
                "Evidence-based answer: https://example.com/current.json"));
    }

    [Fact]
    public void WebEvidenceSourceLedger_IgnoresFailedWebAndNonWebEvidence()
    {
        var steps = new[]
        {
            new CopilotAgentStepRecord
            {
                ToolCall = new CopilotToolCall { ToolName = "FetchUrl" },
                Observation = new CopilotToolObservation { Success = false, Content = "https://failed.example/" },
            },
            new CopilotAgentStepRecord
            {
                ToolCall = new CopilotToolCall { ToolName = "SearchFiles" },
                Observation = new CopilotToolObservation { Success = true, Content = "https://local.example/" },
            },
        };
        ICopilotTool[] tools = [new CopilotFetchUrlTool(), new CopilotSearchFilesTool()];

        var appendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(steps, tools, "Answer without web evidence.");

        Assert.Equal(string.Empty, appendix);
    }

    [Fact]
    public void WebEvidenceSourceLedger_UsesActualSearchResultUrlsOnly()
    {
        var steps = new[]
        {
            new CopilotAgentStepRecord
            {
                ToolCall = new CopilotToolCall { ToolName = "WebSearch" },
                Observation = new CopilotToolObservation
                {
                    Success = true,
                    Content = string.Join('\n',
                        "[Web Search Results]",
                        "Provider note: https://search-provider.example/",
                        "1. Relevant result",
                        "   URL: https://result.example/article",
                        "   Snippet: Mentions https://unverified.example/ only as text.",
                        "[Selected Search Result Deep Read] https://deep.example/page",
                        "[Web Page Fetched] https://deep.example/page"),
                },
            },
        };
        ICopilotTool[] tools = [new CopilotWebSearchTool()];

        var appendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(steps, tools, "Search-based answer.");

        Assert.True(appendix.IndexOf("deep.example", StringComparison.Ordinal) < appendix.IndexOf("result.example", StringComparison.Ordinal));
        Assert.Contains("<https://deep.example/page>", appendix, StringComparison.Ordinal);
        Assert.Contains("<https://result.example/article>", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("search-provider.example", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("unverified.example", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void WebPage_RedirectResolutionRejectsUnsafeTargets()
    {
        var current = new Uri("https://example.com/articles/start");

        Assert.Equal(
            "https://example.com/data/current.json",
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, new Uri("/data/current.json", UriKind.Relative)).ToString());
        Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, new Uri("http://127.0.0.1/private")));
        Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, new Uri("file:///C:/Windows/win.ini")));
        Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, new Uri("https://user:secret@example.com/private")));
        Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, null));
    }

    [Theory]
    [InlineData("http://10.0.0.1/private")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://192.0.2.1/documentation")]
    [InlineData("http://[::ffff:10.0.0.1]/private")]
    [InlineData("http://[2001:db8::1]/documentation")]
    [InlineData("http://[ff02::1]/multicast")]
    [InlineData("file:///C:/Windows/win.ini")]
    public async Task WebPage_RejectsNonPublicAddressesBeforeSendingRequest(string url)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(url, CancellationToken.None));
    }

    [Fact]
    public async Task ReadLocalFile_ReadsSelectedLineRange()
    {
        var filePath = Path.Combine(_tempRoot, "settings.json");
        await File.WriteAllLinesAsync(filePath, new[]
        {
            "{",
            "  \"theme\": \"dark\",",
            "  \"language\": \"en-US\"",
            "}",
        });

        var result = await CopilotReadLocalFileCapability.ReadAsync(
            new[] { filePath },
            filePath,
            preferBatchReadAll: false,
            startLine: 2,
            endLine: 3,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("lines 2-3", result.Summary, StringComparison.Ordinal);
        Assert.Contains("theme", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("{", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadLocalFile_BoundsLargeSingleLineWithoutReadingItIntoResult()
    {
        var filePath = Path.Combine(_tempRoot, "large-single-line.txt");
        await File.WriteAllTextAsync(
            filePath,
            new string('x', CopilotLocalFileToolSupport.MaxReadCharacters + 5_000) + "TAIL_SHOULD_NOT_BE_INCLUDED");

        var result = await CopilotReadLocalFileCapability.ReadAsync(
            new[] { filePath },
            filePath,
            preferBatchReadAll: false,
            startLine: null,
            endLine: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("content truncated", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("TAIL_SHOULD_NOT_BE_INCLUDED", result.Content, StringComparison.Ordinal);
        Assert.True(result.Content.Length < CopilotLocalFileToolSupport.MaxReadCharacters + 500);
    }

    [Fact]
    public async Task ReadAttachedFile_BoundsLargeSingleLineAndRejectsBinaryContent()
    {
        var textFilePath = Path.Combine(_tempRoot, "attached-large.txt");
        var binaryFilePath = Path.Combine(_tempRoot, "attached-binary.dat");
        await File.WriteAllTextAsync(
            textFilePath,
            new string('a', CopilotLocalFileToolSupport.MaxReadCharacters + 2_000) + "ATTACHMENT_TAIL");
        await File.WriteAllBytesAsync(binaryFilePath, new byte[] { 1, 0, 2, 3 });

        var tool = new CopilotReadAttachedFileTool();
        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                Attachments =
                [
                    CopilotAttachmentItem.CreateFile(textFilePath),
                    CopilotAttachmentItem.CreateFile(binaryFilePath),
                ],
            },
            CopilotAgentToolInput.Empty,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("content truncated", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("ATTACHMENT_TAIL", result.Content, StringComparison.Ordinal);
        Assert.Contains("does not appear to be a directly readable text file", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadLocalFile_RejectsSelectedPathOutsideAllowedList()
    {
        var allowedPath = Path.Combine(_tempRoot, "allowed.txt");
        var outsidePath = Path.Combine(_tempRoot, "outside.txt");
        await File.WriteAllTextAsync(allowedPath, "allowed");
        await File.WriteAllTextAsync(outsidePath, "outside");

        var result = await CopilotReadLocalFileCapability.ReadAsync(
            new[] { allowedPath },
            outsidePath,
            preferBatchReadAll: false,
            startLine: null,
            endLine: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not in the current allowed read list", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ListDirectory_ListsEntriesAndSuggestsTextFiles()
    {
        var subDirectory = Path.Combine(_tempRoot, "Templates");
        Directory.CreateDirectory(subDirectory);
        var textFilePath = Path.Combine(_tempRoot, "README.md");
        var binaryFilePath = Path.Combine(_tempRoot, "image.bin");
        File.WriteAllText(textFilePath, "hello");
        File.WriteAllBytes(binaryFilePath, new byte[] { 0, 1, 2 });

        var result = CopilotListDirectoryCapability.List(new[] { _tempRoot }, _tempRoot, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("[Directory] Templates", result.Content, StringComparison.Ordinal);
        Assert.Contains("[File] README.md", result.Content, StringComparison.Ordinal);
        Assert.Contains(textFilePath, result.SuggestedReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(binaryFilePath, result.SuggestedReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListDirectory_BoundsDisplayedAndSuggestedFiles()
    {
        for (var index = 0; index < 100; index++)
            File.WriteAllText(Path.Combine(_tempRoot, $"file-{index:D3}.txt"), index.ToString());

        var result = CopilotListDirectoryCapability.List(new[] { _tempRoot }, _tempRoot, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("[Files] 60+", result.Content, StringComparison.Ordinal);
        Assert.Contains("directory content truncated", result.Content, StringComparison.Ordinal);
        Assert.Equal(10, result.SuggestedReadableLocalFilePaths.Count);
    }

    [Fact]
    public void DocsCapability_RecognizesDocumentationIntent()
    {
        Assert.True(CopilotDocsCapability.HasDocumentationIntent("插件加载失败怎么办"));
        Assert.False(CopilotDocsCapability.HasDocumentationIntent("explain async await"));
    }

    [Fact]
    public void ApplicationCapability_RecognizesThemeAndLanguageIntent()
    {
        Assert.True(CopilotApplicationCapability.HasThemeIntent("switch to dark theme"));
        Assert.True(CopilotApplicationCapability.HasLanguageIntent("change language to English"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
