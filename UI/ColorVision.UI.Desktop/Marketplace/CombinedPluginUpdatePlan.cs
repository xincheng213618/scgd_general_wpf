using ColorVision.UI.Marketplace;

namespace ColorVision.UI.Desktop.Marketplace
{
    public class CombinedPluginUpdatePlan
    {
        public List<CombinedPluginUpdateItem> Updates { get; } = new();
        public List<string> SkippedIncompatiblePlugins { get; } = new();
        public bool HasUpdates => Updates.Count > 0;
    }

    public class CombinedPluginUpdateItem
    {
        public required PluginInfoVM Plugin { get; init; }
        public required MarketplacePluginVersionInfo VersionInfo { get; init; }
    }
}