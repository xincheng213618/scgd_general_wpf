using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Settings.Maintenance;

public sealed class StorageMaintenanceSettingsProvider : IConfigSettingProvider
{
    public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
    {
        yield return new ConfigSettingMetadata
        {
            Name = MaintenanceText.Title,
            Description = MaintenanceText.Description,
            Order = 850,
            Type = ConfigSettingType.TabItem,
            ViewType = typeof(StorageMaintenanceControl)
        };
    }
}

public sealed class StorageMaintenanceConfig : IConfig
{
    public int LogRetentionDays { get; set; } = 30;
    public int TemporaryRetentionDays { get; set; } = 7;
    public int PackageRetentionDays { get; set; } = 30;
}
