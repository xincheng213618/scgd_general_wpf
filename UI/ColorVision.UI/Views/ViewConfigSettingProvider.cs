using ColorVision.UI.Properties;

namespace ColorVision.UI.Views
{
    public class ViewConfigSettingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resources.AutoSwitchSelectedView,
                                Description = Resources.AutoSwitchSelectedView,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(ViewConfig.IsAutoSelect),
                                Source = ViewConfig.Instance
                            }
            };
        }
    }
}
