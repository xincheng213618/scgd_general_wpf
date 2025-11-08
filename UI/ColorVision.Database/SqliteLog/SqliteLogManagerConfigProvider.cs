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
                                Name = Properties.Resources.LogDatabase,
                                Description = Properties.Resources.LogDatabase,
                                Order = 99,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(SqliteLogManagerConfig.IsEnabled),
                                Source =SqliteLogManager.Config
                            },
            };
        }
    }
}
