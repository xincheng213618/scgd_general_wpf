using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public async Task ConnectionGuardRejectsBlockedDnsResultBeforeConnecting()
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("rebind.example", 443),
                static (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse("1.1.1.1"),
                    IPAddress.Loopback,
                }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Fact]
    public async Task ConnectionGuardConnectsToTheExactValidatedAddress()
    {
        var expectedAddress = IPAddress.Parse("1.1.1.1");
        IPEndPoint? connectedEndpoint = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("public.example", 8443),
            (_, _) => Task.FromResult(new[] { expectedAddress }),
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            CancellationToken.None);

        Assert.NotNull(connectedEndpoint);
        Assert.Equal(expectedAddress, connectedEndpoint.Address);
        Assert.Equal(8443, connectedEndpoint.Port);
    }

    [Fact]
    public async Task ConnectionGuardRetriesOnlyValidatedPublicAddresses()
    {
        var firstAddress = IPAddress.Parse("1.1.1.1");
        var secondAddress = IPAddress.Parse("8.8.8.8");
        var attempts = new System.Collections.Generic.List<IPEndPoint>();

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("public.example", 443),
            (_, _) => Task.FromResult(new[] { firstAddress, secondAddress }),
            (endpoint, _) =>
            {
                attempts.Add(endpoint);
                if (endpoint.Address.Equals(firstAddress))
                    throw new SocketException((int)SocketError.HostUnreachable);
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            CancellationToken.None);

        Assert.Collection(
            attempts,
            attempt => Assert.Equal(firstAddress, attempt.Address),
            attempt => Assert.Equal(secondAddress, attempt.Address));
    }
}
