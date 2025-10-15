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
            },
            new ConfigSettingMetadata
            {
                Name = "使用新更新机制",
                Description = "启用新的独立更新器程序（推荐），禁用则使用传统 BAT 脚本方式",
                Order = 1000,
                Type = ConfigSettingType.Bool,
                BindingName = nameof(UpdateManagerConfig.UseNewUpdateMechanism),
                Source = UpdateManagerConfig.Instance,
            },
            new ConfigSettingMetadata
            {
                Name = "启用备份",
                Description = "更新前自动备份当前版本文件",
                Order = 1001,
                Type = ConfigSettingType.Bool,
                BindingName = nameof(UpdateManagerConfig.EnableBackup),
                Source = UpdateManagerConfig.Instance,
            }
        };
        }
    }
}
