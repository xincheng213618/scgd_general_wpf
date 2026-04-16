using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;

namespace WindowsServicePlugin.ServiceManager
{
    public class MqttServiceConfig : ViewModelBase, IConfigSecure
    {
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public static MqttServiceConfig Instance => ConfigService.Instance.GetRequiredService<MqttServiceConfig>();

        public string Host { get => _host; set { _host = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim(); OnPropertyChanged(); } }
        private string _host = "127.0.0.1";

        public int Port
        {
            get => _port;
            set
            {
                _port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                OnPropertyChanged();
            }
        }
        private int _port = 1883;

        public string UserName { get => _userName; set { _userName = value ?? string.Empty; OnPropertyChanged(); } }
        private string _userName = string.Empty;

        public string Password { get => _password; set { _password = value ?? string.Empty; OnPropertyChanged(); } }
        private string _password = string.Empty;

        [JsonIgnore]
        public string ServiceName { get => _serviceName; set { _serviceName = string.IsNullOrWhiteSpace(value) ? "mosquitto" : value; OnPropertyChanged(); } }
        private string _serviceName = "mosquitto";

        [JsonIgnore]
        public string Status { get => _status; set { _status = value ?? string.Empty; OnPropertyChanged(); } }
        private string _status = "未知";

        [JsonIgnore]
        public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); } }
        private bool _isInstalled;

        [JsonIgnore]
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
        private bool _isRunning;

        [JsonIgnore]
        public string ExePath { get => _exePath; set { _exePath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _exePath = string.Empty;

        public void Encryption()
        {
            Password = Cryptography.AESEncrypt(Password, ConfigAESKey, ConfigAESVector);
        }

        public void Decrypt()
        {
            Password = Cryptography.AESDecrypt(Password, ConfigAESKey, ConfigAESVector);
        }
    }
}