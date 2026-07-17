using ColorVision.UI.Marketplace;

namespace ColorVision.UI.Desktop.Marketplace
{
    public class CombinedPluginUpdatePlan
    {
        public Version? HostVersion { get; init; }
        public List<CombinedPluginUpdateItem> Updates { get; } = new();
        public List<string> SkippedIncompatiblePlugins { get; } = new();
        public bool HasUpdates => Updates.Count > 0;

        public CombinedPluginUpdatePlan CreateCompatibleSubset(Version hostVersion)
        {
            CombinedPluginUpdatePlan subset = new() { HostVersion = hostVersion };
            subset.Updates.AddRange(Updates.Where(item => PluginUpdateCompatibility.IsCompatibleWithHostVersion(item.VersionInfo.RequiresVersion, hostVersion)));
            subset.SkippedIncompatiblePlugins.AddRange(SkippedIncompatiblePlugins);
            subset.SkippedIncompatiblePlugins.AddRange(Updates
                .Where(item => !PluginUpdateCompatibility.IsCompatibleWithHostVersion(item.VersionInfo.RequiresVersion, hostVersion))
                .Select(item => item.Plugin.Name ?? item.Plugin.PackageName ?? "Unknown"));
            return subset;
        }
    }

    public class CombinedPluginUpdateItem
    {
        public required PluginInfoVM Plugin { get; init; }
        public required MarketplacePluginVersionInfo VersionInfo { get; init; }
    }
}
