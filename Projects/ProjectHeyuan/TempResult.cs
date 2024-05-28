using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public NumSet X { get; set; }
        public NumSet Y { get; set; } 
        public NumSet Lv { get; set; }

    }
}
