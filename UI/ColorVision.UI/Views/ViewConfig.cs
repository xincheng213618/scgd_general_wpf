using ColorVision.Common.MVVM;
using ColorVision.UI.Properties;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;
using System.Windows;

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

    public class ViewConfig : ViewModelBase, IConfig
    {
        public static ViewConfig Instance => ConfigService.Instance.GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int ViewMaxCount { get => _ViewMaxCount; set { _ViewMaxCount = value; NotifyPropertyChanged(); } }
        private int _ViewMaxCount = 1;

    }
}
