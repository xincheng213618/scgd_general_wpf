using ColorVision.Properties;
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
                Order = 500, // Adjusted order for clarity
                BindingName =nameof(AutoUpdateConfig.IsAutoUpdate),
                Source = AutoUpdateConfig.Instance,
            },
             new ConfigSettingMetadata
            {
                Order = 500,
                BindingName =nameof(AutoUpdateConfig.UpdatePath),
                Source = AutoUpdateConfig.Instance,
            }
        };
        }
    }
}
