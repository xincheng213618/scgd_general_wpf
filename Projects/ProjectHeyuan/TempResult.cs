using ColorVision.Common.MVVM;

namespace ColorVision.Projects.ProjectHeyuan
{
    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;
        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result = true;

        public NumSet X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private NumSet _X;
        public NumSet Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private NumSet _Y;
        public NumSet Lv { get => _Lv; set { _Lv = value; NotifyPropertyChanged(); } }
        private NumSet _Lv;
        public NumSet Dw { get => _dw; set { _dw = value; NotifyPropertyChanged(); } }
        private NumSet _dw;
    }


}
