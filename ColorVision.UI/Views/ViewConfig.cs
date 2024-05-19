using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using System.Windows.Controls;

namespace ColorVision.UI.Views
{

    public class ViewConfigSetting : IConfigSetting
    {
        public string Name => Resources.AutoSwitchSelectedView;

        public string Description => Resources.AutoSwitchSelectedView;

        public string BindingName => "IsAutoSelect";

        public object Source => ViewConfig.Instance;

        public ConfigSettingType Type => ConfigSettingType.Bool;

        public UserControl UserControl => throw new NotImplementedException();

        public ComboBox ComboBox => throw new NotImplementedException();
    }

    public class ViewConfig : ViewModelBase ,IConfig
    {
        public static ViewConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int LastViewCount { get => _LastViewCount; set { _LastViewCount = value; NotifyPropertyChanged(); } }
        private int _LastViewCount = 1;

    }
}
