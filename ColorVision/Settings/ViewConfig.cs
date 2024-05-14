using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Settings
{
    public class ViewConfig : ViewModelBase ,IConfig
    {
        public static ViewConfig GetInstance() => ConfigHandler.GetInstance().GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
    }
}
