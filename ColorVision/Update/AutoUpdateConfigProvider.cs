#pragma warning disable CS8604,CA1822
using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Update
{
    public class AutoUpdateConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
            new ConfigSettingMetadata
            {
                Name = Properties.Resources.CheckUpdatesOnStartup, // Assuming this is for IsAutoUpdate
                Description =  Properties.Resources.CheckUpdatesOnStartup,
                Order = 998, // Adjusted order for clarity
                Type = ConfigSettingType.Bool,
                BindingName =nameof(AutoUpdateConfig.IsAutoUpdate),
                Source = AutoUpdateConfig.Instance,
            },
             new ConfigSettingMetadata
            {
                Name = Properties.Resources.CheckUpdatesOnStartup, // Example: Use a different resource string
                Description =  Properties.Resources.CheckUpdatesOnStartup, // Example: Use a different resource string
                Order = 999,
                Type = ConfigSettingType.Text,
                BindingName =nameof(AutoUpdateConfig.UpdatePath),
                Source = AutoUpdateConfig.Instance,
            }
        };
        }
    }
}
