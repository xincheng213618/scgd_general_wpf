using System;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotWebPageSecurityTests
{
    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://2130706433")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://198.18.0.1")]
    [InlineData("http://192.0.2.1")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fd00::1]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[2001:db8::1]")]
    [InlineData("http://user:password@example.com")]
    [InlineData("ftp://example.com")]
    public void PotentiallyPublicWebPageUriRejectsNonPublicTargets(string value)
    {
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri));

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://1.1.1.1/status")]
    [InlineData("https://[2606:4700:4700::1111]/")]
    public void PotentiallyPublicWebPageUriAllowsHttpAndHttpsPublicTargets(string value)
    {
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri));

        Assert.True(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [InlineData("http://192.168.1.1/private")]
    [InlineData("http://[fd00::1]/private")]
    public void RedirectResolutionRejectsLiteralPrivateTargets(string value)
    {
        var current = new Uri("https://example.com/start");
        var location = new Uri(value);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CopilotWebPageToolSupport.ResolveRedirectWebPageUri(current, location));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchHitExtractionDropsPrivateAndCredentialTargets()
    {
        const string html = """
            <div class="result">
              <a class="result__a" href="http://192.168.1.1/admin">Private</a>
            </div>
            <div class="result">
              <a class="result__a" href="https://user:password@example.com/private">Credentials</a>
            </div>
            <div class="result">
              <a class="result__a" href="https://example.com/public">Public</a>
            </div>
            """;

        var hits = CopilotWebSearchCapability.ExtractDuckDuckGoHits(html);

        var hit = Assert.Single(hits);
        Assert.Equal("https://example.com/public", hit.Url);
    }
}
