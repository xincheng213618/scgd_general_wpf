using ColorVision.Common.MVVM;

namespace ProjectKB.Config
{
    public class SPECConfig : ViewModelBase
    {
        public double MinLv { get => _MinLv; set { _MinLv = value; NotifyPropertyChanged(); } }
        private double _MinLv;
        public double MaxLv { get => _MaxLv; set { _MaxLv = value; NotifyPropertyChanged(); } }
        private double _MaxLv = 100;
        public double AvgLv { get => _AvgLv; set { _AvgLv = value; NotifyPropertyChanged(); } }
        private double _AvgLv = 10;
        /// <summary>
        /// 亮度一致性
        /// </summary>
        public double Uniformity { get => _Uniformity; set { _Uniformity = value; NotifyPropertyChanged(); } }
        private double _Uniformity = 10;
    }
}