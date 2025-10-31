using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Batch
{
    public class BatchProcessMeta:ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); } }
        private string _TemplateName;

        public IBatchProcess BatchProcess { get => _BatchProcess; set { _BatchProcess = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessTypeName)); } }
        private IBatchProcess _BatchProcess;

        public string ProcessTypeName => BatchProcess?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => BatchProcess?.GetType().FullName ?? string.Empty;
    }
}
