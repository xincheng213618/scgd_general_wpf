using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.KB;

namespace ProjectKB
{
    public class KBItem : ViewModelBase
    {
        public KBKeyRect KBKeyRect { get; set; }

        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public double Lv { get => _Lv; set { _Lv = value; OnPropertyChanged(); } }
        private double _Lv;
        public double Cx { get => _Cx; set { _Cx = value; OnPropertyChanged(); } }
        private double _Cx;
        public double Cy { get => _Cy; set { _Cy = value; OnPropertyChanged(); } }
        private double _Cy;
        public double Lc { get => _Lc; set { _Lc = value; OnPropertyChanged(); } }
        private double _Lc;

        public bool Result { get => _Result; set { _Result = value; OnPropertyChanged(); } }
        private bool _Result = true;
    }
}