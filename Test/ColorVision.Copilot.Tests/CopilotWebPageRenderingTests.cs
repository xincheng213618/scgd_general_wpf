using System.Collections.Concurrent;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWebPageRenderingTests
{
    private static readonly Uri PageUri = new("https://public.example/page");

    [Theory]
    [InlineData(false, "path-case")]
    [InlineData(true, "path-case")]
    [InlineData(false, "query-case")]
    [InlineData(true, "query-case")]
    [InlineData(false, "authority-case")]
    [InlineData(true, "authority-case")]
    [InlineData(false, "exact-duplicate")]
    [InlineData(true, "exact-duplicate")]
    public async Task RequestPreparationAndFetchUrlPreserveDistinctTargetsAndFirstOccurrence(
        bool useFetchTool, string scenario)
    {
        var (first, second, expectedCount) = scenario switch
        {
            "path-case" => ("https://public.example/Report", "https://public.example/report", 2),
            "query-case" => ("https://public.example/page?id=ABC", "https://public.example/page?id=abc", 2),
            "authority-case" => ("HTTPS://PUBLIC.example/Page", "https://public.example/Page", 1),
            "exact-duplicate" => ("https://public.example/same", "https://public.example/same", 1),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        var expectedUrls = expectedCount == 1 ? new[] { first } : new[] { first, second };
        var capturedUrls = new ConcurrentQueue<string>();
        Task<CopilotFetchedWebPageContent> LoadPage(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            capturedUrls.Enqueue(url);
            return Task.FromResult(new CopilotFetchedWebPageContent(url, "Captured page", string.Empty,
                "Evidence for " + url));
        }

        var prompt = $"Compare {first} and {second}";
        string content;
        if (useFetchTool)
        {
            var result = await new CopilotFetchUrlTool(LoadPage).ExecuteAsync(
                new CopilotAgentRequest { UserText = prompt, Mode = CopilotAgentMode.Web },
                new CopilotAgentToolInput { Query = prompt }, CancellationToken.None);
            Assert.True(result.Success);
            content = result.Content;
            Assert.Contains($"input_urls_total: {expectedCount}", content, StringComparison.Ordinal);
            Assert.Contains($"input_urls_attempted: {expectedCount}", content, StringComparison.Ordinal);
            Assert.Contains("input_urls_omitted: 0", content, StringComparison.Ordinal);
            Assert.Contains("input_set_complete: true", content, StringComparison.Ordinal);
        }
        else
        {
            content = await new CopilotConversationRequestBuilder(LoadPage).BuildUserRequestContentAsync(
                prompt, liveContext: null, CancellationToken.None);
            Assert.Contains($"Prefetch scope: attempted the first {expectedCount} of {expectedCount} unique URL(s).",
                content, StringComparison.Ordinal);
        }

        Assert.Equal(expectedUrls.Order(StringComparer.Ordinal), capturedUrls.Order(StringComparer.Ordinal));
        var previousBlockPosition = -1;
        foreach (var url in expectedUrls)
        {
            var blockPosition = content.IndexOf("[Web Page Fetched] " + url, StringComparison.Ordinal);
            Assert.True(blockPosition > previousBlockPosition);
            Assert.Contains("Evidence for " + url, content, StringComparison.Ordinal);
            previousBlockPosition = blockPosition;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FetchUrlReadsCaseDistinctDiscoveredResourceOnce(bool queryCase)
    {
        const string requested = "https://public.example/Report?format=JSON";
        var discovered = queryCase
            ? "https://public.example/Report?format=json"
            : "https://public.example/report?format=JSON";
        var capturedUrls = new ConcurrentQueue<string>();
        Task<CopilotFetchedWebPageContent> LoadPage(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            capturedUrls.Enqueue(url);
            var page = new CopilotFetchedWebPageContent(url, "Captured page", string.Empty, "Evidence for " + url);
            return Task.FromResult(url == requested ? page with
            {
                RelatedResourceUrls =
                [
                    requested,
                    "https://PUBLIC.example/Report?format=JSON",
                    discovered,
                    discovered.Replace("public.example", "PUBLIC.example", StringComparison.Ordinal),
                ],
            } : page);
        }

        var result = await new CopilotFetchUrlTool(LoadPage).ExecuteAsync(
            new CopilotAgentRequest { UserText = "Read " + requested, Mode = CopilotAgentMode.Web },
            new CopilotAgentToolInput { Query = requested }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { requested, discovered }, capturedUrls);
        Assert.Contains("input_urls_total: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("input_set_complete: true", result.Content, StringComparison.Ordinal);
        Assert.Contains("discovered_urls_attempted: 1", result.Content, StringComparison.Ordinal);
        foreach (var url in new[] { requested, discovered })
        {
            Assert.Contains("[Web Page Fetched] " + url, result.Content, StringComparison.Ordinal);
            Assert.Contains("Evidence for " + url, result.Content, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaticLinksPreserveCaseDistinctResourcesAndPagesWithoutFragments(bool queryCase)
    {
        const string firstResource = "https://public.example/Report.json?format=JSON";
        var secondResource = queryCase
            ? "https://public.example/Report.json?format=json"
            : "https://public.example/report.json?format=JSON";
        var source = new Uri(queryCase
            ? "https://public.example/Report?view=ABC"
            : "https://public.example/PAGE");
        var firstPage = queryCase ? "https://public.example/Report?view=Abc" : "https://public.example/Page";
        var secondPage = queryCase ? "https://public.example/Report?view=abc" : "https://public.example/page";
        var page = CopilotWebPageToolSupport.ExtractWebPageContent(source, $"""
            <html><body>
            <a href="{firstResource.Replace("public.example", "PUBLIC.example", StringComparison.Ordinal)}#top">First JSON</a>
            <a href="{firstResource}#duplicate">Duplicate JSON</a>
            <a href="{secondResource}#bottom">Second JSON</a>
            <a href="{firstPage.Replace("public.example", "PUBLIC.example", StringComparison.Ordinal)}#top">First page</a>
            <a href="{firstPage}#duplicate">Duplicate page</a>
            <a href="{secondPage}#bottom">Second page</a>
            </body></html>
            """);

        Assert.Equal(new[] { firstResource, secondResource }, page.DiscoveredResourceUrls);
        Assert.Equal(new[]
        {
            new CopilotWebPageLink(firstPage, "First page"),
            new CopilotWebPageLink(secondPage, "Second page"),
        }, page.DiscoveredPageLinks);
    }

    [Fact]
    public async Task EmptyApplicationShellUsesRenderedBodyAndReportsItsSource()
    {
        var calls = 0;
        var page = await CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(PageUri, "text/html",
            "<html><body><div id='app'></div><script src='/app.js'></script></body></html>",
            (uri, token) => { calls++; Assert.Equal(PageUri, uri); return Task.FromResult(new CopilotFetchedWebPageContent(uri.AbsoluteUri, "Rendered", "", "The application is ready.")); }, CancellationToken.None);
        Assert.Equal(1, calls);
        Assert.True(page.BrowserRendered);
        Assert.Contains("application is ready", page.Content);
        Assert.Contains("browser-rendered DOM", CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page));
    }

    [Theory]
    [InlineData("text/html", "<html><body>Static readable text</body></html>")]
    [InlineData("application/json", "{\"value\":42}")]
    public async Task StaticAndStructuredResourcesDoNotStartBrowser(string mediaType, string content)
    {
        var page = await CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(PageUri, mediaType, content,
            (_, _) => throw new InvalidOperationException("Browser should not start"), CancellationToken.None);
        Assert.False(page.BrowserRendered);
        Assert.Null(page.RenderingNotice);
    }

    [Fact]
    public async Task BrowserFailureRetainsStaticEvidenceAndStatesItsLimit()
    {
        var page = await CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(PageUri, "text/html",
            "<html><body>Loading<script src='/app.js'></script></body></html>",
            (_, _) => throw new InvalidOperationException("Runtime unavailable"), CancellationToken.None);
        Assert.Equal("Loading", page.Content);
        Assert.False(page.BrowserRendered);
        Assert.True(page.IsSparseExtraction);
        Assert.Contains("Runtime unavailable", page.RenderingNotice);
    }

    [Fact]
    public async Task CancellationDoesNotBecomeSuccessfulStaticFallback()
    {
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(
            PageUri, "text/html", "<body>Loading<script></script></body>",
            (_, token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); throw new Exception(); }, cancellation.Token));
    }

    [Fact]
    public async Task EmptyBodyAndFailedBrowserExplainBothStages()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(
            PageUri, "text/html", "<body><script></script></body>",
            (_, _) => throw new InvalidOperationException("Runtime unavailable"), CancellationToken.None));
        Assert.Contains("静态正文为空", error.Message);
        Assert.Contains("Runtime unavailable", error.Message);
    }

    [Theory]
    [InlineData("/Report", "/report")]
    [InlineData("/page?view=ABC", "/page?view=abc")]
    [InlineData("/app#/routeA", "/app#/routeB")]
    public void RenderedLinksKeepUrlAndTextAndExcludeOtherOrigins(string firstTarget, string secondTarget)
    {
        var firstUrl = "https://public.example" + firstTarget;
        var secondUrl = "https://public.example" + secondTarget;
        var links = new List<CopilotWebPageLink>
        {
            new("https://elsewhere.example/", "External"),
            new("https://PUBLIC.example" + firstTarget, "First target"),
        };
        links.AddRange(Enumerable.Repeat(new CopilotWebPageLink(firstUrl, "Duplicate target"),
            CopilotWebPageToolSupport.MaxDiscoveredPageLinks - 1));
        links.Add(new CopilotWebPageLink(secondUrl, "Second target"));
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            url = PageUri.AbsoluteUri, title = "Title", text = "Rendered text", description = "Description",
            links = links.Select(link => new { url = link.Url, text = link.Text }),
        });
        var page = CopilotWebPageBrowserRenderer.ParseRenderedPage(json);

        Assert.Equal(new[]
        {
            new CopilotWebPageLink(firstUrl, "First target"),
            new CopilotWebPageLink(secondUrl, "Second target"),
        }, page.DiscoveredPageLinks);
        Assert.Equal(PageUri.AbsoluteUri, page.Url);
        Assert.Equal("Rendered text", page.Content);
        Assert.True(page.BrowserRendered);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("file:///C:/Windows/win.ini")]
    public async Task BrowserRejectsOutOfScopeNavigationBeforeCreatingRuntime(string url)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotWebPageBrowserRenderer.RenderAsync(new Uri(url), CancellationToken.None));
    }
}
