using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class NumSet :ViewModelBase
    {
        public float ValMax { get => _ValMax; set { _ValMax = value; OnPropertyChanged(); } }
        private float _ValMax;

        public float ValMin { get => _ValMin; set { _ValMin = value; OnPropertyChanged(); } }
        private float _ValMin;

        public float Value { get => _Value; set { _Value = value; OnPropertyChanged(); } }
        private float _Value;


    }
}
