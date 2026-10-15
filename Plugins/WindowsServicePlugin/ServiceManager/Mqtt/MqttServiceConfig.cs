using ColorVision.Common.MVVM;

namespace WindowsServicePlugin.ServiceManager
{
    public class MqttServiceConfig : ViewModelBase
    {
        public string ServiceName { get => _serviceName; set { _serviceName = string.IsNullOrWhiteSpace(value) ? "mosquitto" : value; OnPropertyChanged(); } }
        private string _serviceName = "mosquitto";

        public string Status { get => _status; set { _status = value ?? string.Empty; OnPropertyChanged(); } }
        private string _status = ServiceStatusText.Unknown;

        public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); } }
        private bool _isInstalled;

        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
        private bool _isRunning;

        public string ExePath { get => _exePath; set { _exePath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _exePath = string.Empty;

    }
}
