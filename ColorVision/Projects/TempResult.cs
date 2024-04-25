using ColorVision.Common.MVVM;

namespace ColorVision.Projects
{
    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public NumSet NumSet { get; set; } = new NumSet();
    }
}
