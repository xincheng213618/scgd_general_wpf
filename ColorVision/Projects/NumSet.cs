using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class NumSet :ViewModelBase
    {
        public double White { get => _White; set { _White = value; NotifyPropertyChanged(); } }
        private double _White;

        public double Blue { get => _Blue; set { _Blue = value; NotifyPropertyChanged(); } }
        private double _Blue;

        public double Red { get => _Red; set { _Red = value; NotifyPropertyChanged(); } }
        private double _Red;
        public double Orange { get => _Orange; set { _Orange = value; NotifyPropertyChanged(); } }
        private double _Orange;
    }
}
