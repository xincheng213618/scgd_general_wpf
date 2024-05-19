using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using System.Windows.Controls;

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
        public static ViewConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewConfig>();


        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int LastViewCount { get => _LastViewCount; set { _LastViewCount = value; NotifyPropertyChanged(); } }
        private int _LastViewCount = 1;

    }
}
