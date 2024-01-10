using ColorVision.MVVM;

namespace ColorVision.SettingUp
{
    public class ViewConfig : ViewModelBase
    {
        private static ViewConfig _instance;
        private static readonly object _locker = new();
        public static ViewConfig GetInstance() { lock (_locker) { return _instance ??= new ViewConfig(); } }

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
    }
}
