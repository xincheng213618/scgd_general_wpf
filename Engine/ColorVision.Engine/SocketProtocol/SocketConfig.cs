using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;

namespace ColorVision.Engine
{
    public class SocketConfig : ViewModelBase,IConfig
    {
        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public event EventHandler<bool> IsSocketServiceChanged;
        public bool IsSocketService { get => _IsSocketService; set { _IsSocketService = value; NotifyPropertyChanged(); IsSocketServiceChanged?.Invoke(this, _IsSocketService); } }
        private bool _IsSocketService;

        /// <summary>
        /// IP地址
        /// </summary>
        [DisplayName("IP地址")]
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口")]
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port = 6666;

        public int BufferLength { get => _BufferLength; set { _BufferLength = value; NotifyPropertyChanged(); } }
        private int _BufferLength = 1024;

        public SocketConfig()
        {
        }
    }
}
