using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class PluginUpdateCompatibilityTests
{
    [Fact]
    public void SelectsLatestVersionCompatibleWithCurrentHost()
    {
        MarketplacePluginDetail detail = new()
        {
            Versions =
            [
                new MarketplacePluginVersionInfo { Version = "2.0.0.0", RequiresVersion = "1.5.0.0" },
                new MarketplacePluginVersionInfo { Version = "1.9.0.0", RequiresVersion = "1.4.0.0" },
            ],
        };

        MarketplacePluginVersionInfo? selected = PluginUpdateCompatibility.SelectLatestCompatibleVersion(
            new Version(1, 8, 0, 0),
            detail,
            new Version(1, 4, 10, 90));

        Assert.NotNull(selected);
        Assert.Equal("1.9.0.0", selected.Version);
        Assert.Equal("1.4.0.0", selected.RequiresVersion);
    }

    [Fact]
    public void DetailLevelHostRequirementIsPreservedOnSelectedVersion()
    {
        MarketplacePluginDetail detail = new()
        {
            RequiresVersion = "1.4.10.80",
            Versions = [new MarketplacePluginVersionInfo { Version = "2.0.0.0" }],
        };

        MarketplacePluginVersionInfo? selected = PluginUpdateCompatibility.SelectLatestCompatibleVersion(
            new Version(1, 0, 0, 0),
            detail,
            new Version(1, 4, 10, 90));

        Assert.NotNull(selected);
        Assert.Equal("1.4.10.80", selected.RequiresVersion);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1.5.0.0")]
    public void RejectsInvalidOrNewerHostRequirements(string requiresVersion)
    {
        Assert.False(PluginUpdateCompatibility.IsCompatibleWithHostVersion(requiresVersion, new Version(1, 4, 10, 90)));
    }

    [Fact]
    public void UsesLatestMetadataFallbackOnlyWhenNoDetailedVersionExists()
    {
        MarketplacePluginDetail detail = new()
        {
            LatestVersion = "2.0.0.0",
            RequiresVersion = "1.4.0.0",
            Changelog = "Compatible release",
        };

        MarketplacePluginVersionInfo? selected = PluginUpdateCompatibility.SelectLatestCompatibleVersion(
            installedVersion: null,
            detail,
            new Version(1, 4, 10, 90));

        Assert.NotNull(selected);
        Assert.Equal("2.0.0.0", selected.Version);
        Assert.Equal("1.4.0.0", selected.RequiresVersion);
    }

    [Fact]
    public void DoesNotUseDetailFallbackToBypassAnIncompatibleDetailedVersion()
    {
        MarketplacePluginDetail detail = new()
        {
            LatestVersion = "2.0.0.0",
            Versions = [new MarketplacePluginVersionInfo { Version = "2.0.0.0", RequiresVersion = "1.5.0.0" }],
        };

        MarketplacePluginVersionInfo? selected = PluginUpdateCompatibility.SelectLatestCompatibleVersion(
            installedVersion: null,
            detail,
            new Version(1, 4, 10, 90));

        Assert.Null(selected);
    }

    [Fact]
    public void CompatibleSubsetExcludesPluginsThatRequireTargetApplicationVersion()
    {
        CombinedPluginUpdatePlan plan = new() { HostVersion = new Version(1, 4, 10, 90) };
        plan.Updates.Add(CreateUpdate("CurrentCompatible", null));
        plan.Updates.Add(CreateUpdate("TargetOnly", "1.4.10.90"));

        CombinedPluginUpdatePlan subset = plan.CreateCompatibleSubset(new Version(1, 4, 10, 85));

        Assert.Single(subset.Updates);
        Assert.Equal("CurrentCompatible", subset.Updates[0].Plugin.PackageName);
        Assert.Contains("TargetOnly", subset.SkippedIncompatiblePlugins);
    }

    private static CombinedPluginUpdateItem CreateUpdate(string pluginId, string? requiresVersion)
    {
        PluginInfoVM plugin = (PluginInfoVM)RuntimeHelpers.GetUninitializedObject(typeof(PluginInfoVM));
        plugin.PackageName = pluginId;
        plugin.Name = pluginId;
        return new CombinedPluginUpdateItem
        {
            Plugin = plugin,
            VersionInfo = new MarketplacePluginVersionInfo
            {
                Version = "2.0.0.0",
                RequiresVersion = requiresVersion,
            },
        };
    }
}
