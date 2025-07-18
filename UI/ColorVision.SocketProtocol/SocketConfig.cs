using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.SocketProtocol
{
    public class SocketConfig : ViewModelBase,IConfig
    {
        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public event EventHandler<bool> ServerEnabledChanged;

        [DisplayName("服务器启用状态")]
        public bool IsServerEnabled  { get => _IsServerEnabled; set { _IsServerEnabled = value; NotifyPropertyChanged(); ServerEnabledChanged?.Invoke(this, _IsServerEnabled); } }
        private bool _IsServerEnabled;

        /// <summary>
        /// IP地址
        /// </summary>
        [DisplayName("IP地址")]
        public string IPAddress { get => _IPAddress; set { _IPAddress = value; NotifyPropertyChanged(); } }
        private string _IPAddress = "0.0.0.0";

        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口")]
        public int ServerPort
        {
            get => _ServerPort; 
            set
            {
                _ServerPort = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _ServerPort = 6666;

        [DisplayName(nameof(SocketBufferSize))] 
        public int SocketBufferSize { get => _SocketBufferSize; set { _SocketBufferSize = value; NotifyPropertyChanged(); } }
        private int _SocketBufferSize = 10240;

        public SocketConfig()
        {
        }
    }
}
