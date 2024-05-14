using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Settings
{
    public class ViewConfig : ViewModelBase ,IConfig
    {
        public static ViewConfig GetInstance() => ConfigHandler1.GetInstance().GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int LastViewCount { get => _LastViewCount; set { _LastViewCount = value; NotifyPropertyChanged(); } }
        private int _LastViewCount = 1;

    }
}
