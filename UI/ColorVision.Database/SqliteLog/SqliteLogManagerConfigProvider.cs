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
                    Order = 1,
                    BindingName = nameof(SqliteLogManagerConfig.IsEnabled),
                    Source = SqliteLogManager.Config,
                },
                new ConfigSettingMetadata
                {
                    Order = 2,
                    BindingName = nameof(SqliteLogManagerConfig.IsCompressionEnabled),
                    Source = SqliteLogManager.Config,
                },
                new ConfigSettingMetadata
                {
                    Order = 3,
                    BindingName = nameof(SqliteLogManagerConfig.MaxFileSizeInMB),
                    Source = SqliteLogManager.Config,
                }
            };
        }
    }
}