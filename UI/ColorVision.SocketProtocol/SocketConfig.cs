using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.SocketProtocol
{
    public enum SocketPhraseType
    {
        Json,
        Text
    }

    public class SocketConfig : ViewModelBase,IConfig
    {
        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public event EventHandler<bool> ServerEnabledChanged;

        [DisplayName("IsServerEnabled")]
        public bool IsServerEnabled  { get => _IsServerEnabled; set { _IsServerEnabled = value; OnPropertyChanged(); ServerEnabledChanged?.Invoke(this, _IsServerEnabled); } }
        private bool _IsServerEnabled;

        /// <summary>
        /// IP地址
        /// </summary>
        [DisplayName("IPAddress")]
        public string IPAddress { get => _IPAddress; set { _IPAddress = value; OnPropertyChanged(); } }
        private string _IPAddress = "0.0.0.0";

        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("ServerPort")]
        public int ServerPort
        {
            get => _ServerPort; 
            set
            {
                _ServerPort = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                OnPropertyChanged();
            }
        }
        private int _ServerPort = 6666;

        [DisplayName(nameof(SocketBufferSize))] 
        public int SocketBufferSize { get => _SocketBufferSize; set { _SocketBufferSize = value; OnPropertyChanged(); } }
        private int _SocketBufferSize = 10240;

        [DisplayName(nameof(SocketPhraseType))]
        public SocketPhraseType SocketPhraseType { get => _SocketPhraseType; set { _SocketPhraseType = value; OnPropertyChanged(); } }
        private SocketPhraseType _SocketPhraseType = SocketPhraseType.Json;

        public SocketConfig()
        {
        }
    }
}
