using System.Collections.Concurrent;
using System.Text;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlainTextKeepsLiteralMarkupWhitespaceAndEntitiesWithoutStartingBrowser(bool longContent)
    {
        var content = "    value = &lt;literal&gt;\r\n<script>ALARM=result-NG</script>\r\n<style>literal style</style>\r\n"
            + (longContent ? new string('x', 240) : "END");
        var renderCalls = 0;

        var page = await CopilotWebPageToolSupport.ExtractWithBrowserFallbackAsync(PageUri, "text/plain", content,
            (_, _) =>
            {
                renderCalls++;
                return Task.FromResult(new CopilotFetchedWebPageContent(PageUri.AbsoluteUri, "Rendered", "", "Replacement rendered text"));
            }, CancellationToken.None);

        Assert.Equal(content, page.Content);
        Assert.Equal(0, renderCalls);
        Assert.False(page.BrowserRendered);
        Assert.Null(page.RenderingNotice);
        Assert.Equal(content, CopilotWebPageToolSupport.BuildStoredWebPageContent(page));
        Assert.Contains(content, CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page), StringComparison.Ordinal);
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

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("file:///C:/Windows/win.ini")]
    public async Task BrowserRejectsOutOfScopeNavigationBeforeCreatingRuntime(string url)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotWebPageBrowserRenderer.RenderAsync(new Uri(url), CancellationToken.None));
    }
}
