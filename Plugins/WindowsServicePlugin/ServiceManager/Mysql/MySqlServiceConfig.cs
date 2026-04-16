using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;

namespace WindowsServicePlugin.ServiceManager
{
    public class MySqlServiceConfig : ViewModelBase, IConfigSecure
    {
        public const string RootProfileName = "RootPath";
        public const string BusinessProfileName = "CVPath";
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public static MySqlServiceConfig Instance => ConfigService.Instance.GetRequiredService<MySqlServiceConfig>();

        public string Host { get => _host; set { _host = NormalizeHost(value); OnPropertyChanged(); } }
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
        private int _port = 3306;

        public string RootPassword { get => _rootPassword; set { _rootPassword = value ?? string.Empty; OnPropertyChanged(); } }
        private string _rootPassword = string.Empty;

        [JsonIgnore]
        public string RootNewPassword { get => _rootNewPassword; set { _rootNewPassword = value ?? string.Empty; OnPropertyChanged(); } }
        private string _rootNewPassword = string.Empty;

        public string AppUser { get => _appUser; set { _appUser = string.IsNullOrWhiteSpace(value) ? "cv" : value.Trim(); OnPropertyChanged(); } }
        private string _appUser = "cv";

        public string AppPassword { get => _appPassword; set { _appPassword = value ?? string.Empty; OnPropertyChanged(); } }
        private string _appPassword = string.Empty;

        public string Database { get => _database; set { _database = string.IsNullOrWhiteSpace(value) ? "color_vision_4xx" : value.Trim(); OnPropertyChanged(); } }
        private string _database = "color_vision_4xx";

        public string InstallBasePath { get => _installBasePath; set { _installBasePath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _installBasePath = string.Empty;

        [JsonIgnore]
        public string ServiceName { get => _serviceName; set { _serviceName = string.IsNullOrWhiteSpace(value) ? "MySQL" : value; OnPropertyChanged(); } }
        private string _serviceName = "MySQL";

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
        public string Version { get => _version; set { _version = value ?? string.Empty; OnPropertyChanged(); } }
        private string _version = string.Empty;

        [JsonIgnore]
        public string ExePath { get => _exePath; set { _exePath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _exePath = string.Empty;

        public void Encryption()
        {
            RootPassword = Cryptography.AESEncrypt(RootPassword, ConfigAESKey, ConfigAESVector);
            AppPassword = Cryptography.AESEncrypt(AppPassword, ConfigAESKey, ConfigAESVector);
        }

        public void Decrypt()
        {
            RootPassword = Cryptography.AESDecrypt(RootPassword, ConfigAESKey, ConfigAESVector);
            AppPassword = Cryptography.AESDecrypt(AppPassword, ConfigAESKey, ConfigAESVector);
        }

        private static string NormalizeHost(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
        }

    }
}
