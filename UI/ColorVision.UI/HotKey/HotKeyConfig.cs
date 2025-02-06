using ColorVision.UI.Properties;
using System.Collections.ObjectModel;

namespace ColorVision.UI.HotKey
{

    public class HotKeyConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resources.HotKeys,
                                Description = Resources.HotKeys,
                                Type = ConfigSettingType.TabItem,
                                Source = HotKeyConfig.Instance,
                                UserControl = new HotKeysSetting()
                            }
            };
        }
    }

    public class HotKeyConfig :IConfig
    {
        public static HotKeyConfig Instance => ConfigService.Instance.GetRequiredService<HotKeyConfig>();

        public ObservableCollection<HotKeys> Hotkeys { get; set; } = new ObservableCollection<HotKeys>();
    }
}
