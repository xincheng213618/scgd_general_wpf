using ColorVision.Engine.PropertyEditor;
using cvColorVision;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs
{

    public class ConfigSMU : DeviceServiceConfig
    {
        [DisplayName("自动连接")]
        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;

        [DisplayName("驱动类型"), Description("2450 使用 Keithley_2400 驱动时，请在仪器端选择 SCPI 2400 命令集并重启；2604 使用 Keithley_2600。")]
        public Pss_Type DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DevType)); } }
        private Pss_Type _DevType;

        [DisplayName("网口连接"), Description("开启后填写源表 IP 地址；Keithley 网口连接使用固定 TCP 5025 端口。修改后关闭连接再重新打开。")]
        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); OnPropertyChanged(nameof(SerialPort)); OnPropertyChanged(nameof(IpAddress)); } }
        private bool _IsNet;

        [Browsable(false)]
        public string DevName { get => Id; set { Id = value; OnPropertyChanged(); OnPropertyChanged(nameof(SerialPort)); OnPropertyChanged(nameof(IpAddress)); } }

        [JsonIgnore, DisplayName("串口"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(IsNet), true)]
        public string SerialPort { get => DevName; set => DevName = value; }

        [JsonIgnore, DisplayName("源表 IP 地址"), PropertyVisibility(nameof(IsNet))]
        [Description("填写仪器 LAN 页面显示的 IP 地址，例如 192.168.1.100；不含协议或端口。Keithley 使用 TCP 5025，当前连接所在电脑须能访问该地址。")]
        [PropertyEditorType(typeof(IPAddressPropertiesEditor))]
        public string IpAddress { get => DevName; set => DevName = value; }


        [DisplayName("四线制测量")]
        public bool Is4Wire { get => _Is4Wire; set { _Is4Wire = value; OnPropertyChanged(); } }
        private bool _Is4Wire;

        [DisplayName("使用前面板端子")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront;

        [DisplayName("使用 A 通道"), Description("IsSrcA 是保留的兼容配置项；手动测量和流程的通道分别由显示配置和节点参数决定。")]
        public bool IsSrcA { get => _IsSrcA; set { _IsSrcA = value; OnPropertyChanged(); } }
        private bool _IsSrcA = true;

        [DisplayName("测量延时（ms）"), Description("DelayTime 以毫秒传给源表驱动，驱动转换为秒后用于源延时。修改后需重新连接。")]
        public double DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private double _DelayTime;
    }
}
