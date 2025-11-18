using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process
{
    public class ProcessMeta:ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string FlowTemplate { get => _FlowTemplate; set { _FlowTemplate = value; OnPropertyChanged(); } }
        private string _FlowTemplate;

        public IProcess Process { get => _Process; set { _Process = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessTypeName)); } }
        private IProcess _Process;

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public string ProcessTypeName => Process?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => Process?.GetType().FullName ?? string.Empty;
    }
}
