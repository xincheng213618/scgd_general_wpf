using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.KB;

namespace ProjectARVR
{
    public class KBItem : ViewModelBase
    {
        public KBKeyRect KBKeyRect { get; set; }

        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public double Lv { get => _Lv; set { _Lv = value; NotifyPropertyChanged(); } }
        private double _Lv;
        public double Cx { get => _Cx; set { _Cx = value; NotifyPropertyChanged(); } }
        private double _Cx;
        public double Cy { get => _Cy; set { _Cy = value; NotifyPropertyChanged(); } }
        private double _Cy;
        public double Lc { get => _Lc; set { _Lc = value; NotifyPropertyChanged(); } }
        private double _Lc;

        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result = true;
    }
}