using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Engine.SqliteLog
{
    public class SqliteLogManagerConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "日志数据库",
                                Description = "日志数据库",
                                Order = 99,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(SqliteLogManagerConfig.IsEnabled),
                                Source =SqliteLogManager.Config
                            },
            };
        }
    }
}
