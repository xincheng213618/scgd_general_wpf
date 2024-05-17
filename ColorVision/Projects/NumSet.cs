using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class NumSet :ViewModelBase
    {
        public string White { get => _White; set { _White = value; NotifyPropertyChanged(); } }
        private string _White;

        public string Blue { get => _Blue; set { _Blue = value; NotifyPropertyChanged(); } }
        private string _Blue;

        public string Red { get => _Red; set { _Red = value; NotifyPropertyChanged(); } }
        private string _Red;
        public string Orange { get => _Orange; set { _Orange = value; NotifyPropertyChanged(); } }
        private string _Orange;
    }
}
