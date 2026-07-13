#pragma warning disable CA1707,CA1861
using ColorVision.Copilot;
using System;
using System.IO;
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
