using ColorVision.Common.MVVM;

namespace ColorVision.Settings
{
    public class ViewConfig : ViewModelBase
    {
        public static ViewConfig GetInstance() =>ConfigHandler.GetInstance().SoftwareConfig.ViewConfig;

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
    }
}
