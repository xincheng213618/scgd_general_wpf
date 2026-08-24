using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWebPageSecurityTests
{
    public static TheoryData<string> BlockedIpv4Addresses => new()
    {
        "0.0.0.0",
        "0.255.255.255",
        "10.0.0.0",
        "10.255.255.255",
        "100.64.0.0",
        "100.127.255.255",
        "127.0.0.0",
        "127.255.255.255",
        "168.63.129.16",
        "169.254.0.0",
        "169.254.255.255",
        "172.16.0.0",
        "172.31.255.255",
        "192.0.0.0",
        "192.0.0.9",
        "192.0.0.10",
        "192.0.0.255",
        "192.0.2.0",
        "192.0.2.255",
        "192.31.196.0",
        "192.31.196.255",
        "192.52.193.0",
        "192.52.193.255",
        "192.88.99.0",
        "192.88.99.255",
        "192.168.0.0",
        "192.168.255.255",
        "192.175.48.0",
        "192.175.48.255",
        "198.18.0.0",
        "198.19.255.255",
        "198.51.100.0",
        "198.51.100.255",
        "203.0.113.0",
        "203.0.113.255",
        "224.0.0.0",
        "239.255.255.255",
        "240.0.0.0",
        "255.255.255.255",
    };

    public static TheoryData<string> PublicIpv4BoundaryAddresses => new()
    {
        "1.1.1.1",
        "9.255.255.255",
        "11.0.0.0",
        "100.63.255.255",
        "100.128.0.0",
        "126.255.255.255",
        "128.0.0.0",
        "168.63.129.15",
        "168.63.129.17",
        "169.253.255.255",
        "169.255.0.0",
        "172.15.255.255",
        "172.32.0.0",
        "191.255.255.255",
        "192.0.1.0",
        "192.0.3.0",
        "192.31.195.255",
        "192.31.197.0",
        "192.52.192.255",
        "192.52.194.0",
        "192.88.98.255",
        "192.88.100.0",
        "192.167.255.255",
        "192.169.0.0",
        "192.175.47.255",
        "192.175.49.0",
        "198.17.255.255",
        "198.20.0.0",
        "198.51.99.255",
        "198.51.101.0",
        "203.0.112.255",
        "203.0.114.0",
        "223.255.255.255",
    };

    public static TheoryData<string> NonCanonicalBlockedIpv4Hosts => new()
    {
        "2130706433",
        "0x7f000001",
        "017700000001",
        "127.1",
        "127.0.1",
        "0177.0.0.01",
        "0x7f.1",
        "3232235777",
        "0xc0a80101",
        "0300.0250.0001.0001",
        "192.168.257",
        "2852039166",
        "0xa9fea9fe",
        "0251.0376.0251.0376",
        "169.254.43518",
        "2822734096",
        "0xa83f8110",
        "127.0.0.1.",
        "localhost.",
        "foo.localhost.",
        "１２７.０.０.１",
        "127。0。0。1",
        "127．0．0．1",
        "127｡0｡0｡1",
        "０x７f０００００１",
    };

    public static TheoryData<string> NonCanonicalPublicIpv4Hosts => new()
    {
        "16843009",
        "0x01010101",
        "01.01.01.01",
    };

    public static TheoryData<string, string> BlockedNetworkSpecificNat64Addresses => new()
    {
        { "2001:4860:c000:aa::", "2001:4860:a00:1::" },
        { "2001:4860:12c0:0:aa::", "2001:4860:120a:0:1::" },
        { "2001:4860:1234:c000:0:aa00::", "2001:4860:1234:a00:0:100::" },
        { "2001:4860:1234:56c0:0:aa:beef:cafe", "2001:4860:1234:560a:0:1::" },
        { "2001:4860:1234:5678:c0:0:ab00:0", "2001:4860:1234:5678:a:0:1be:ef01" },
        { "2001:4860:1234:5678:0:abcd:c000:aa", "2001:4860:1234:5678:0:abcd:a00:1" },
    };

    public static TheoryData<string, string> PublicNetworkSpecificNat64Addresses => new()
    {
        { "2001:4860:c000:aa::", "2001:4860:808:808::" },
        { "2001:4860:12c0:0:aa::", "2001:4860:1208:808:8::" },
        { "2001:4860:1234:c000:0:aa00::", "2001:4860:1234:808:8:800::" },
        { "2001:4860:1234:56c0:0:aa:beef:cafe", "2001:4860:1234:5608:8:808::" },
        { "2001:4860:1234:5678:c0:0:ab00:0", "2001:4860:1234:5678:8:808:800:0" },
        { "2001:4860:1234:5678:0:abcd:c000:aa", "2001:4860:1234:5678:0:abcd:808:808" },
    };

    public static TheoryData<string, string> ConfiguredBlockedNetworkSpecificNat64Addresses => new()
    {
        { "2001:4860::/32", "2001:4860:a00:1::" },
        { "2001:4860:1200::/40", "2001:4860:120a:0:1::" },
        { "2001:4860:1234::/48", "2001:4860:1234:a00:0:100::" },
        { "2001:4860:1234:5600::/56", "2001:4860:1234:560a:0:1::" },
        { "2001:4860:1234:5678::/64", "2001:4860:1234:5678:a:0:1be:ef01" },
        { "2001:4860:1234:5678:0:abcd::/96", "2001:4860:1234:5678:0:abcd:a00:1" },
    };

    public static TheoryData<string, string> ConfiguredPublicNetworkSpecificNat64Addresses => new()
    {
        { "2001:4860::/32", "2001:4860:808:808::" },
        { "2001:4860:1200::/40", "2001:4860:1208:808:8::" },
        { "2001:4860:1234::/48", "2001:4860:1234:808:8:800::" },
        { "2001:4860:1234:5600::/56", "2001:4860:1234:5608:8:808::" },
        { "2001:4860:1234:5678::/64", "2001:4860:1234:5678:8:808:800:0" },
        { "2001:4860:1234:5678:0:abcd::/96", "2001:4860:1234:5678:0:abcd:808:808" },
    };

    public static TheoryData<string, string> ConfiguredMalformedNetworkSpecificNat64Addresses => new()
    {
        { "2001:4860::/32", "2001:4860:a00:1:100::" },
        { "2001:4860:1200::/40", "2001:4860:120a:0:101::" },
        { "2001:4860:1234::/48", "2001:4860:1234:a00:100:100::" },
        { "2001:4860:1234:5600::/56", "2001:4860:1234:560a:100:1::" },
        { "2001:4860:1234:5678::/64", "2001:4860:1234:5678:10a:0:1be:ef01" },
    };

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://[::1]")]
    [InlineData("http://[::]")]
    [InlineData("http://[::127.0.0.1]")]
    [InlineData("http://[::ffff:10.0.0.1]")]
    [InlineData("http://[::ffff:169.254.1.1]")]
    [InlineData("http://[::ffff:8.8.8.8]")]
    [InlineData("http://[0:0:0:0:0:ffff:0808:0808]")]
    [InlineData("http://[64:ff9b::127.0.0.1]")]
    [InlineData("http://[64:ff9b::10.0.0.1]")]
    [InlineData("http://[64:ff9b::192.0.2.1]")]
    [InlineData("http://[64:ff9b::168.63.129.16]")]
    [InlineData("http://[64:ff9b::192.31.196.1]")]
    [InlineData("http://[64:ff9b::192.52.193.1]")]
    [InlineData("http://[64:ff9b::192.175.48.1]")]
    [InlineData("http://[64:ff9b:1::1]")]
    [InlineData("http://[100::1]")]
    [InlineData("http://[100:0:0:1::1]")]
    [InlineData("http://[fd00::1]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[fe80::1%251]")]
    [InlineData("http://[fec0::1]")]
    [InlineData("http://[ff02::1]")]
    [InlineData("http://[2001::1]")]
    [InlineData("http://[2001:2::1]")]
    [InlineData("http://[2001:10::1]")]
    [InlineData("http://[2001:db8::1]")]
    [InlineData("http://[2001:1000::1]")]
    [InlineData("http://[2001:4e00::1]")]
    [InlineData("http://[2001:6000::1]")]
    [InlineData("http://[2001:c000::1]")]
    [InlineData("http://[2002:7f00:1::1]")]
    [InlineData("http://[2003:4000::1]")]
    [InlineData("http://[2420::1]")]
    [InlineData("http://[2610:200::1]")]
    [InlineData("http://[2611::1]")]
    [InlineData("http://[2620:200::1]")]
    [InlineData("http://[2621::1]")]
    [InlineData("http://[2640::1]")]
    [InlineData("http://[2810::1]")]
    [InlineData("http://[2a20::1]")]
    [InlineData("http://[2c10::1]")]
    [InlineData("http://[2d00::1]")]
    [InlineData("http://[3000::1]")]
    [InlineData("http://[3ffe::1]")]
    [InlineData("http://[3fff::1]")]
    [InlineData("http://[4000::1]")]
    [InlineData("http://[5f00::1]")]
    [InlineData("http://user:password@example.com")]
    [InlineData("ftp://example.com")]
    public void PotentiallyPublicWebPageUriRejectsNonPublicTargets(string value)
    {
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri));

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [MemberData(nameof(BlockedIpv4Addresses))]
    public void PotentiallyPublicWebPageUriRejectsSpecialPurposeIpv4Addresses(string address)
    {
        var uri = new Uri($"http://{address}/");

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [MemberData(nameof(PublicIpv4BoundaryAddresses))]
    public void PotentiallyPublicWebPageUriAllowsPublicIpv4BoundaryAddresses(string address)
    {
        var uri = new Uri($"https://{address}/");

        Assert.True(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [MemberData(nameof(NonCanonicalBlockedIpv4Hosts))]
    public void PotentiallyPublicWebPageUriRejectsNonCanonicalBlockedIpv4Hosts(string host)
    {
        Assert.True(Uri.TryCreate($"http://{host}/", UriKind.Absolute, out var uri));

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [MemberData(nameof(NonCanonicalPublicIpv4Hosts))]
    public void PotentiallyPublicWebPageUriAllowsNonCanonicalPublicIpv4Hosts(string host)
    {
        Assert.True(Uri.TryCreate($"https://{host}/", UriKind.Absolute, out var uri));

        Assert.True(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri));
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://1.1.1.1/status")]
    [InlineData("http://[64:ff9b::8.8.8.8]/")]
    [InlineData("https://[2001:200::1]/")]
    [InlineData("https://[2001:fff::1]/")]
    [InlineData("https://[2001:c00::1]/")]
    [InlineData("https://[2001:db7::1]/")]
    [InlineData("https://[2001:db9::1]/")]
    [InlineData("https://[2001:1200::1]/")]
    [InlineData("https://[2001:4dff::1]/")]
    [InlineData("https://[2001:5000::1]/")]
    [InlineData("https://[2001:5fff::1]/")]
    [InlineData("https://[2001:8000::1]/")]
    [InlineData("https://[2001:b000::1]/")]
    [InlineData("https://[2001:bfff::1]/")]
    [InlineData("https://[2003:3fff::1]/")]
    [InlineData("https://[2400::1]/")]
    [InlineData("https://[240f::1]/")]
    [InlineData("https://[2410::1]/")]
    [InlineData("https://[241f::1]/")]
    [InlineData("https://[2600::1]/")]
    [InlineData("https://[260f::1]/")]
    [InlineData("https://[2606:4700:4700::1111]/")]
    [InlineData("https://[2610:1ff::1]/")]
    [InlineData("https://[2620:1ff::1]/")]
    [InlineData("https://[263f::1]/")]
    [InlineData("https://[280f::1]/")]
    [InlineData("https://[2a00::1]/")]
    [InlineData("https://[2a0f::1]/")]
    [InlineData("https://[2a10::1]/")]
    [InlineData("https://[2a1f::1]/")]
    [InlineData("https://[2c0f::1]/")]
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
    public void RedirectResolutionRejectsHttpsToHttpDowngrade()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(
                new Uri("https://example.com/start"),
                new Uri("http://example.com/continued")));

        Assert.Contains("HTTPS", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedirectResolutionAllowsHttpToHttpsUpgrade()
    {
        var resolved = CopilotWebPageToolSupport.ResolveRedirectWebPageUri(
            new Uri("http://example.com/start"),
            new Uri("https://example.com/continued"));

        Assert.Equal("https://example.com/continued", resolved.ToString());
    }

    [Fact]
    public void WebPageUriPolicyRejectsZeroPortBeforeConnecting()
    {
        var target = new Uri("http://example.com:0/path");

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(target));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(
                new Uri("http://example.com/start"),
                target));

        Assert.Contains("port", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebPageUriPolicyRejectsOversizedRedirectTarget()
    {
        var target = new Uri("https://example.com/?value=" + new string('a', CopilotWebPageToolSupport.MaxWebPageUrlCharacters));

        Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(target));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.ResolveRedirectWebPageUri(
                new Uri("https://example.com/start"),
                target));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
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
                string.Empty,
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [MemberData(nameof(BlockedIpv4Addresses))]
    public async Task ConnectionGuardRejectsSpecialPurposeIpv4BeforeConnecting(string address)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("special-purpose.example", 443),
                (_, _) => Task.FromResult(new[] { IPAddress.Parse(address) }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                string.Empty,
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [MemberData(nameof(NonCanonicalBlockedIpv4Hosts))]
    public async Task ConnectionGuardRejectsNonCanonicalBlockedHostWithoutDnsLookup(string host)
    {
        var resolverCalls = 0;
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint(host, 443),
                (_, _) =>
                {
                    resolverCalls++;
                    return Task.FromResult(new[] { IPAddress.Parse("1.1.1.1") });
                },
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                string.Empty,
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, resolverCalls);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [MemberData(nameof(BlockedNetworkSpecificNat64Addresses))]
    public async Task ConnectionGuardRejectsBlockedIpv4EmbeddedInDiscoveredNat64Prefix(
        string discoveryAddress,
        string targetAddress)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("translated.example", 443),
                (host, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse(string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                        ? discoveryAddress
                        : targetAddress),
                }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                string.Empty,
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [MemberData(nameof(PublicNetworkSpecificNat64Addresses))]
    public async Task ConnectionGuardAllowsPublicIpv4EmbeddedInDiscoveredNat64Prefix(
        string discoveryAddress,
        string targetAddress)
    {
        var expectedAddress = IPAddress.Parse(targetAddress);
        IPEndPoint? connectedEndpoint = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("translated.example", 443),
            (host, _) => Task.FromResult(new[]
            {
                IPAddress.Parse(string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                    ? discoveryAddress
                    : targetAddress),
            }),
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            string.Empty,
            CancellationToken.None);

        Assert.NotNull(connectedEndpoint);
        Assert.Equal(expectedAddress, connectedEndpoint.Address);
    }

    [Theory]
    [MemberData(nameof(ConfiguredBlockedNetworkSpecificNat64Addresses))]
    public async Task ConnectionGuardUsesConfiguredNat64PrefixWhenDiscoveryFails(
        string configuredPrefix,
        string targetAddress)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("translated.example", 443),
                (host, _) => string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                    ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound))
                    : Task.FromResult(new[] { IPAddress.Parse(targetAddress) }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                configuredPrefix,
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Fact]
    public async Task RequestPreflightAppliesUpdatedPref64BeforeAPooledConnectionCanBeReused()
    {
        var uri = new Uri("https://translated.example/resource");
        var targetAddress = IPAddress.Parse("2001:4860:a00:1::");
        Task<IPAddress[]> ResolveAsync(string host, CancellationToken _)
        {
            return string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound))
                : Task.FromResult(new[] { targetAddress });
        }

        await CopilotWebPageToolSupport.EnsureAllowedWebPageUriAsync(
            uri,
            ResolveAsync,
            string.Empty,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.EnsureAllowedWebPageUriAsync(
                uri,
                ResolveAsync,
                "2001:4860::/32",
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestPreflightRejectsInvalidPref64BeforeResolving()
    {
        var resolverCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotWebPageToolSupport.EnsureAllowedWebPageUriAsync(
                new Uri("https://public.example/resource"),
                (_, _) =>
                {
                    resolverCalls++;
                    return Task.FromResult(new[] { IPAddress.Parse("1.1.1.1") });
                },
                "2001:4860::1/96",
                CancellationToken.None));

        Assert.Contains("Pref64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, resolverCalls);
    }

    [Theory]
    [MemberData(nameof(ConfiguredPublicNetworkSpecificNat64Addresses))]
    public async Task ConnectionGuardAllowsPublicIpv4EmbeddedInConfiguredNat64PrefixWhenDiscoveryFails(
        string configuredPrefix,
        string targetAddress)
    {
        var expectedAddress = IPAddress.Parse(targetAddress);
        IPEndPoint? connectedEndpoint = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("translated.example", 443),
            (host, _) => string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound))
                : Task.FromResult(new[] { expectedAddress }),
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            configuredPrefix,
            CancellationToken.None);

        Assert.NotNull(connectedEndpoint);
        Assert.Equal(expectedAddress, connectedEndpoint.Address);
    }

    [Theory]
    [MemberData(nameof(ConfiguredMalformedNetworkSpecificNat64Addresses))]
    public async Task ConnectionGuardRejectsMatchedNat64PrefixWithNonZeroUOctet(
        string configuredPrefix,
        string targetAddress)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("translated.example", 443),
                (host, _) => string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                    ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound))
                    : Task.FromResult(new[] { IPAddress.Parse(targetAddress) }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                configuredPrefix,
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [InlineData("2001:4860:a00:1::")]
    [InlineData("2606:4700:1234:5678:0:abcd:a00:1")]
    public async Task ConnectionGuardCombinesConfiguredAndDiscoveredNat64Prefixes(string targetAddress)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("translated.example", 443),
                (host, _) => Task.FromResult(string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                    ? new[]
                    {
                        IPAddress.Parse("2606:4700:1234:5678:0:abcd:c000:aa"),
                        IPAddress.Parse("2606:4700:1234:5678:0:abcd:c000:ab"),
                    }
                    : new[] { IPAddress.Parse(targetAddress) }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                "2001:4860::/32",
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [InlineData("not-a-prefix")]
    [InlineData("192.0.2.0/24")]
    [InlineData("2001:4860::/72")]
    [InlineData("2001:4860::1/96")]
    [InlineData("2001:4860:1234:5678:100::/96")]
    public async Task ConnectionGuardRejectsInvalidConfiguredNat64PrefixBeforeResolving(
        string configuredPrefix)
    {
        var resolverCalls = 0;
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("public.example", 443),
                (_, _) =>
                {
                    resolverCalls++;
                    return Task.FromResult(new[] { IPAddress.Parse("1.1.1.1") });
                },
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                configuredPrefix,
                CancellationToken.None));

        Assert.Contains("Pref64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, resolverCalls);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.TryAgain)]
    [InlineData(SocketError.NoRecovery)]
    public async Task ConnectionGuardAllowsNativePublicIpv6WhenNat64DiscoveryNameIsUnavailable(SocketError discoveryError)
    {
        var expectedAddress = IPAddress.Parse("2001:4860:4860::8888");
        IPEndPoint? connectedEndpoint = null;
        string? discoveryHost = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("native-ipv6.example", 443),
            (host, _) =>
            {
                if (!string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new[] { expectedAddress });

                discoveryHost = host;
                return Task.FromException<IPAddress[]>(new SocketException((int)discoveryError));
            },
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            string.Empty,
            CancellationToken.None);

        Assert.NotNull(connectedEndpoint);
        Assert.Equal(expectedAddress, connectedEndpoint.Address);
        Assert.Equal("ipv4only.arpa.", discoveryHost);
    }

    [Fact]
    public async Task ConnectionGuardUsesAlternateDiscoveryAddressToDisambiguateNat64Prefix()
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("translated.example", 443),
                (host, _) => Task.FromResult(string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                    ? new[]
                    {
                        IPAddress.Parse("2001:4860:c000:aa::c000:aa"),
                        IPAddress.Parse("2001:4860:c000:aa::c000:ab"),
                    }
                    : new[] { IPAddress.Parse("2001:4860:c000:aa::a00:1") }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                string.Empty,
                CancellationToken.None));

        Assert.Contains("NAT64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Fact]
    public async Task ConnectionGuardDoesNotKeepWeakerNat64CandidateAfterDisambiguation()
    {
        var expectedAddress = IPAddress.Parse("2001:4860:c000:aa::808:808");
        IPEndPoint? connectedEndpoint = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("translated.example", 443),
            (host, _) => Task.FromResult(string.Equals(host, "ipv4only.arpa.", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    IPAddress.Parse("2001:4860:c000:aa::c000:aa"),
                    IPAddress.Parse("2001:4860:c000:aa::c000:ab"),
                }
                : new[] { expectedAddress }),
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            string.Empty,
            CancellationToken.None);

        Assert.NotNull(connectedEndpoint);
        Assert.Equal(expectedAddress, connectedEndpoint.Address);
    }

    [Theory]
    [InlineData("2606:4700:4700::1111", "64:ff9b::10.0.0.1")]
    [InlineData("64:ff9b::8.8.8.8", "::ffff:192.168.1.1")]
    [InlineData("8.8.8.8", "::ffff:8.8.8.8")]
    [InlineData("8.8.8.8", "64:ff9b:1::1")]
    [InlineData("3fff::1", "8.8.8.8")]
    public async Task ConnectionGuardRejectsAnyBlockedIpv6DnsResultBeforeConnecting(string first, string second)
    {
        var connectCalls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("mixed.example", 443),
                (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse(first),
                    IPAddress.Parse(second),
                }),
                (_, _) =>
                {
                    connectCalls++;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                string.Empty,
                CancellationToken.None));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connectCalls);
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("64:ff9b::8.8.8.8")]
    [InlineData("2001:4860:4860::8888")]
    public async Task ConnectionGuardConnectsToTheExactValidatedAddress(string address)
    {
        var expectedAddress = IPAddress.Parse(address);
        IPEndPoint? connectedEndpoint = null;

        await using var stream = await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
            new DnsEndPoint("public.example", 8443),
            (_, _) => Task.FromResult(new[] { expectedAddress }),
            (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            string.Empty,
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
            string.Empty,
            CancellationToken.None);

        Assert.Collection(
            attempts,
            attempt => Assert.Equal(firstAddress, attempt.Address),
            attempt => Assert.Equal(secondAddress, attempt.Address));
    }

    [Fact]
    public async Task ConnectionGuardPreservesCancellationReportedAsASocketFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await CopilotWebPageToolSupport.ConnectToAllowedWebPageHostAsync(
                new DnsEndPoint("public.example", 443),
                (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse("1.1.1.1"),
                    IPAddress.Parse("8.8.8.8"),
                }),
                (_, _) =>
                {
                    attempts++;
                    cancellation.Cancel();
                    throw new SocketException((int)SocketError.OperationAborted);
                },
                string.Empty,
                cancellation.Token));

        Assert.Equal(1, attempts);
    }
}
