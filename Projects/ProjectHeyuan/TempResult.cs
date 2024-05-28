using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;
        public NumSet X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private NumSet _X;
        public NumSet Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private NumSet _Y;
        public NumSet Lv { get => _Lv; set { _Lv = value; NotifyPropertyChanged(); } }
        private NumSet _Lv;
    }


}
