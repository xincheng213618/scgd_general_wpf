using log4net;
using System.Reflection;

namespace ColorVision.UI.Desktop.Settings;

// The window and global search use the same metadata projection. No setting
// controls are created, and property values are not read or changed here.
internal static class SettingEntryCatalog
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SettingEntryCatalog));

    public static IReadOnlyList<SettingEntry> Create(IEnumerable<ConfigSettingMetadata> settings)
    {
        var entries = new List<SettingEntry>();
        ConfigSettingMetadata? applicationUpdate = null;
        ConfigSettingMetadata? pluginUpdate = null;
        foreach (ConfigSettingMetadata setting in settings)
        {
            try
            {
                if (setting.Type == ConfigSettingType.Property && setting.BindingName == "IsAutoUpdate")
                {
                    if (setting.Source?.GetType().Name == "AutoUpdateConfig")
                    {
                        applicationUpdate = setting;
                        continue;
                    }
                    if (setting.Source?.GetType().Name == "MarketplaceWindowConfig")
                    {
                        pluginUpdate = setting;
                        continue;
                    }
                }

                PropertyInfo? property = null;
                if (setting.Type == ConfigSettingType.Property)
                {
                    if (setting.Source == null || string.IsNullOrWhiteSpace(setting.BindingName)) continue;
                    property = setting.Source.GetType().GetProperty(setting.BindingName);
                    if (property == null)
                    {
                        Log.Warn($"Setting property not found: {setting.Source.GetType().Name}.{setting.BindingName}");
                        continue;
                    }
                }
                entries.Add(SettingMetadataResolver.CreateEntry(setting, property));
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to add setting: {setting.Name ?? setting.BindingName}: {ex.Message}");
            }
        }

        AddStartupUpdateSetting(entries, applicationUpdate, pluginUpdate);
        return entries;
    }

    private static void AddStartupUpdateSetting(List<SettingEntry> entries, ConfigSettingMetadata? application, ConfigSettingMetadata? plugin)
    {
        var targets = new List<AggregatedBoolSettingTarget>();
        foreach (ConfigSettingMetadata? setting in new[] { application, plugin })
        {
            if (setting?.Source == null || string.IsNullOrWhiteSpace(setting.BindingName)) continue;
            PropertyInfo? property = setting.Source.GetType().GetProperty(setting.BindingName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType == typeof(bool))
                targets.Add(new AggregatedBoolSettingTarget(setting.Source, property));
        }
        if (targets.Count == 0) return;

        var metadata = new ConfigSettingMetadata
        {
            Order = new[] { application?.Order, plugin?.Order }.Where(order => order.HasValue).Select(order => order!.Value).DefaultIfEmpty(500).Min(),
            Group = ConfigSettingConstants.Universal,
            Name = SettingResources.StartupCheckUpdates,
            Description = SettingResources.StartupCheckUpdatesDescription,
            Section = ConfigSettingConstants.SectionUpdates,
            Type = ConfigSettingType.Property,
            BindingName = nameof(AggregatedBoolSetting.IsChecked),
            Source = new AggregatedBoolSetting(targets)
        };
        SettingEntry entry = SettingMetadataResolver.CreateEntry(metadata, typeof(AggregatedBoolSetting).GetProperty(nameof(AggregatedBoolSetting.IsChecked)));
        entry.Id = "setting:startup-check-updates";
        entry.SearchText = string.Join(" ", entry.SearchText, SettingResources.StartupCheckUpdatesSearchAliases,
            "CheckUpdatesOnStartup CheckPluginUpdates AutoUpdateConfig MarketplaceWindowConfig IsAutoUpdate application plugin theme update startup detect").ToLowerInvariant();
        entries.Add(entry);
    }
}
