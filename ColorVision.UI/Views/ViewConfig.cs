using ColorVision.Common.MVVM;

namespace ColorVision.UI.Views
{
    public class ViewConfig : ViewModelBase ,IConfig
    {
        public static ViewConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int LastViewCount { get => _LastViewCount; set { _LastViewCount = value; NotifyPropertyChanged(); } }
        private int _LastViewCount = 1;

    }
}
