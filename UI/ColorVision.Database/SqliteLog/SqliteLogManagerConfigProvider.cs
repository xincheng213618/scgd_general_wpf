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
                                Order = 99,
                                BindingName = nameof(SqliteLogManagerConfig.IsEnabled),
                                Source =SqliteLogManager.Config
                            },
            };
        }
    }
}
