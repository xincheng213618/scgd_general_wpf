using ColorVision.MVVM;

namespace ColorVision.SettingUp
{
    public class ViewConfig : ViewModelBase
    {
        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
    }
}
