namespace ColorVision.Copilot.Tests;

public sealed class CopilotAnonymousHttpTransportTests
{
    [Fact]
    public void DocumentationHandlerIsStatelessAndDoesNotFollowRedirects()
    {
        using var handler = CopilotDocsToolSupport.CreateHttpHandler();

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void WebSearchHandlerIsStatelessAndDoesNotFollowRedirects()
    {
        using var handler = CopilotWebSearchCapability.CreateHttpHandler();

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void WebPageHandlerIsStatelessAndKeepsRedirectAndProxyGuards()
    {
        using var handler = CopilotWebPageToolSupport.CreateHttpHandler();

        Assert.False(handler.UseCookies);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.NotNull(handler.ConnectCallback);
    }
}
