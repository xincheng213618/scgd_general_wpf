using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class NumSet :ViewModelBase
    {
        public float ValMax { get => _ValMax; set { _ValMax = value; NotifyPropertyChanged(); } }
        private float _ValMax;

        public float ValMin { get => _ValMin; set { _ValMin = value; NotifyPropertyChanged(); } }
        private float _ValMin;

        public float Value { get => _Value; set { _Value = value; NotifyPropertyChanged(); } }
        private float _Value;


    }
}
