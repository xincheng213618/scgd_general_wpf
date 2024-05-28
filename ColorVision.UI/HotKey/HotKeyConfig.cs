using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using ColorVision.UI.Views;
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
        public static HotKeyConfig Instance => ConfigHandler.GetInstance().GetRequiredService<HotKeyConfig>();

        public ObservableCollection<HotKeys> Hotkeys { get; set; } = new ObservableCollection<HotKeys>();
    }
}
