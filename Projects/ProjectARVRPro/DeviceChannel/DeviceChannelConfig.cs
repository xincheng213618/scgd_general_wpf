using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// 设备通道类型
    /// </summary>
    public enum DeviceChannelType
    {
        [Description("雷鸟串口")]
        ThunderbirdSerial,
        [Description("通用串口")]
        GenericSerial,
        [Description("Socket")]
        Socket
    }

    /// <summary>
    /// 设备通道配置 — 持久化通道连接参数
    /// </summary>
    public class DeviceChannelConfig : ViewModelBase
    {
        [DisplayName("通道名称")]
        public string Name { get => _Name; set { if (_Name != value) { _Name = value; OnPropertyChanged(); } } }
        private string _Name = string.Empty;

        [DisplayName("通道类型")]
        public DeviceChannelType ChannelType { get => _ChannelType; set { if (_ChannelType != value) { _ChannelType = value; OnPropertyChanged(); } } }
        private DeviceChannelType _ChannelType = DeviceChannelType.ThunderbirdSerial;

        [DisplayName("启用")]
        public bool IsEnabled { get => _IsEnabled; set { if (_IsEnabled != value) { _IsEnabled = value; OnPropertyChanged(); } } }
        private bool _IsEnabled = true;

        // ─── 串口参数 ────────────────────────────────────

        [DisplayName("串口名称")]
        public string SerialPortName { get => _SerialPortName; set { if (_SerialPortName != value) { _SerialPortName = value; OnPropertyChanged(); } } }
        private string _SerialPortName = string.Empty;

        [DisplayName("波特率")]
        public int BaudRate { get => _BaudRate; set { if (_BaudRate != value) { _BaudRate = value; OnPropertyChanged(); } } }
        private int _BaudRate = 115200;

        [DisplayName("超时(ms)")]
        public int TimeoutMs { get => _TimeoutMs; set { if (_TimeoutMs != value) { _TimeoutMs = value; OnPropertyChanged(); } } }
        private int _TimeoutMs = 1000;

        // ─── Socket 参数 ─────────────────────────────────

        [DisplayName("Socket地址")]
        public string Host { get => _Host; set { if (_Host != value) { _Host = value; OnPropertyChanged(); } } }
        private string _Host = string.Empty;

        [DisplayName("Socket端口")]
        public int Port { get => _Port; set { if (_Port != value) { _Port = value; OnPropertyChanged(); } } }
        private int _Port;

        /// <summary>
        /// 创建深拷贝
        /// </summary>
        public DeviceChannelConfig Clone()
        {
            return new DeviceChannelConfig
            {
                Name = Name,
                ChannelType = ChannelType,
                IsEnabled = IsEnabled,
                SerialPortName = SerialPortName,
                BaudRate = BaudRate,
                TimeoutMs = TimeoutMs,
                Host = Host,
                Port = Port
            };
        }
    }
}
