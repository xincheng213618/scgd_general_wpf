using ColorVision.ToolPlugins.UpdateRelay;
using ColorVision.UI.Marketplace;

namespace ColorVision.UI.Tests;

public sealed class UpdateRelayConfigurationTests
{
    [Fact]
    public void ClientAddressBuildsTheUrlForTheSelectedPort()
    {
        Assert.Equal("http://192.168.31.86:9999", UpdateRelayConfiguration.GetClientUrl(" 192.168.31.86 ", 9999));
    }

    [Fact]
    public void DefaultHttpPortUsesTheSameAddressAsTheSavedUpdateSources()
    {
        string url = UpdateRelayConfiguration.GetClientUrl("192.168.31.86", 80);
        string[] saved = MarketplaceConfig.ParseServiceBaseUrls("http://192.168.31.86:80");

        Assert.Equal("http://192.168.31.86", url);
        Assert.Null(UpdateRelayConfiguration.BuildClientSourceSetting(saved, url, add: true));
        Assert.Equal(string.Empty, UpdateRelayConfiguration.BuildClientSourceSetting(saved, url, add: false));
    }

    [Theory]
    [InlineData("192.168.031.86")]
    [InlineData("3232243542")]
    [InlineData("http://192.168.31.86")]
    [InlineData("192.168.31.256")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::ffff:192.168.31.86")]
    public void ClientAddressRejectsNonCanonicalOrUnusableIPv4(string address)
    {
        Assert.Throws<ArgumentException>(() => UpdateRelayConfiguration.GetClientUrl(address, 9998));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void ClientAddressRejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UpdateRelayConfiguration.GetClientUrl("192.168.31.86", port));
    }

    [Fact]
    public void AddingAndRemovingRelayPreservesTheOtherUpdateSourcesAndTheirOrder()
    {
        string mirror = "https://mirror.example.test";
        string official = MarketplaceConfig.DefaultServiceBaseUrl;
        string offline = "file://server/Updates";
        string relay = "http://192.168.31.86:9998";
        string[] existing = [mirror, official, offline];
        string[] withRelay = [mirror, official, offline, relay];

        Assert.Equal(string.Join("; ", withRelay), UpdateRelayConfiguration.BuildClientSourceSetting(existing, relay, add: true));
        Assert.Null(UpdateRelayConfiguration.BuildClientSourceSetting(withRelay, relay, add: true));
        Assert.Equal(string.Join("; ", existing), UpdateRelayConfiguration.BuildClientSourceSetting(withRelay, relay, add: false));
        Assert.Null(UpdateRelayConfiguration.BuildClientSourceSetting(existing, relay, add: false));
        Assert.Equal(official, UpdateRelayConfiguration.BuildClientSourceSetting([official, relay], relay, add: false));
    }
}
