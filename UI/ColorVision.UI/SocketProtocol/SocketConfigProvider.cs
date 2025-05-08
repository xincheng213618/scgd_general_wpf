using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.UI.SocketProtocol
{
    public class SocketConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "通信协议",
                                Description = "通用协议配置",
                                Order =1,
                                Type = ConfigSettingType.Class,
                                Source = SocketConfig.Instance
                            },
            };
        }

    }
}
