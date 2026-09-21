using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWebPageRenderingTests
{
    private static readonly Uri PageUri = new("https://public.example/page");

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

    [Fact]
    public void RenderedLinksKeepUrlAndTextAndExcludeOtherOrigins()
    {
        var page = CopilotWebPageBrowserRenderer.ParseRenderedPage("""
            {"url":"https://public.example/page","title":"Title","text":"Rendered text","description":"Description",
             "links":[{"url":"https://public.example/about","text":"About"},{"url":"https://elsewhere.example/","text":"External"}]}
            """);
        var link = Assert.Single(page.DiscoveredPageLinks);
        Assert.Equal("https://public.example/about", link.Url);
        Assert.Equal("About", link.Text);
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
