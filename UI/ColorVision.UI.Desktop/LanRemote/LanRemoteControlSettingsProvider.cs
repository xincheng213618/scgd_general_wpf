using System.Collections.Generic;

namespace ColorVision.UI.Desktop.LanRemote
{
    public sealed class LanRemoteControlSettingsProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new()
                {
                    Name = "局域网控制",
                    Description = "启用后，手机可以扫描二维码连接到这台电脑上的 ColorVision。",
                    Order = 35,
                    Type = ConfigSettingType.TabItem,
                    Source = LanRemoteControlConfig.Instance,
                    ViewType = typeof(LanRemoteControlSettingsControl)
                }
            };
        }
    }
}
