using ColorVision.Common.MVVM;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;
        public bool Result { get => _Result; set { _Result = value; OnPropertyChanged(); } }
        private bool _Result = true;

        public NumSet X { get => _X; set { _X = value; OnPropertyChanged(); } }
        private NumSet _X;
        public NumSet Y { get => _Y; set { _Y = value; OnPropertyChanged(); } }
        private NumSet _Y;
        public NumSet Lv { get => _Lv; set { _Lv = value; OnPropertyChanged(); } }
        private NumSet _Lv;
        public NumSet Dw { get => _dw; set { _dw = value; OnPropertyChanged(); } }
        private NumSet _dw;
    }


}
