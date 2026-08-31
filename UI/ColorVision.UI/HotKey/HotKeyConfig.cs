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
                                Name = HotkeyEditorText.Title,
                                Description = string.Empty, // The custom page owns its description and reset toolbar.
                                Type = ConfigSettingType.TabItem,
                                Source = HotKeyConfig.Instance,
                                ViewType = typeof(HotKeysSetting)
                            }
            };
        }
    }

    public class HotKeyConfig :IConfig
    {
        public static HotKeyConfig Instance => ConfigService.Instance.GetRequiredService<HotKeyConfig>();

        public ObservableCollection<HotkeySetting> Hotkeys { get; set; } = new ObservableCollection<HotkeySetting>();
    }
}
