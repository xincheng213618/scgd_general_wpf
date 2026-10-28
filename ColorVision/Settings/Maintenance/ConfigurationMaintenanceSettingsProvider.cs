using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Settings.Maintenance;

public sealed class ConfigurationMaintenanceSettingsProvider : IConfigSettingProvider
{
    public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
    {
        yield return new ConfigSettingMetadata
        {
            Name = MaintenanceText.Get("ConfigurationMaintenanceTitle"),
            Description = MaintenanceText.Get("ConfigurationDescription"),
            Group = ConfigSettingConstants.Configuration,
            Order = 860,
            Type = ConfigSettingType.TabItem,
            ViewType = typeof(ConfigurationMaintenanceControl)
        };
    }
}
