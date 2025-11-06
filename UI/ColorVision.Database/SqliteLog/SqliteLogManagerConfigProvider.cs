using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogManagerConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = ColorVision.Database.Properties.Resources.LogDatabase,
                                Description = ColorVision.Database.Properties.Resources.LogDatabase,
                                Order = 99,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(SqliteLogManagerConfig.IsEnabled),
                                Source =SqliteLogManager.Config
                            },
            };
        }
    }
}
