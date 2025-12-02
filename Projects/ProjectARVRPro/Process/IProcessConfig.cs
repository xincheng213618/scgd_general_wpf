using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process
{
    public interface IProcessConfig
    {
        bool SaveCsv { get; set; }
    }

    public abstract class ProcessConfigBase : ViewModelBase, IProcessConfig
    {
        public bool SaveCsv { get => _SaveCsv; set { _SaveCsv = value; OnPropertyChanged(); } }
        private bool _SaveCsv = false;
    }
}
