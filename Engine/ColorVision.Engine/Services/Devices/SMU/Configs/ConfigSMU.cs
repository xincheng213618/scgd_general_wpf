using ColorVision.Engine.PropertyEditor;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs
{

    public class ConfigSMU : DeviceServiceConfig
    {
        [DisplayName("自动连接")]
        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;

        [DisplayName("驱动类型"), Description("2450 使用 Keithley_2400 驱动时，请在仪器端选择 SCPI 2400 命令集并重启；2604 使用 Keithley_2600。")]
        public Pss_Type DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); OnPropertyChanged(nameof(SupportsNplc)); OnPropertyChanged(nameof(Nplc)); } }
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

        [DisplayName("测量延时（ms）"), Description("设备默认的测量前等待时间，以毫秒传给驱动。本地模式修改后在下一次普通点测或扫描生效，无需重连；普通本地源表节点可单独覆盖，高级操作使用本地窗口的延时。")]
        public double DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private double _DelayTime;

        [DisplayName("积分时间（NPLC）"), PropertyVisibility(nameof(SupportsNplc))]
        [Description("以电源周期为单位，仅本地模式普通点测和扫描使用；打开连接时应用，修改后下一次普通测量也会应用。2400：0.01～10，2600：0.001～10；值越大积分时间越长，高级窗口使用独立设置。")]
        public double Nplc { get => nplc ?? DefaultNplc(DevType); set { nplc = value; OnPropertyChanged(); } }
        private double? nplc;

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore, Browsable(false)]
        public bool SupportsNplc => DevType is Pss_Type.Keithley_2400 or Pss_Type.Keithley_2600;

        internal static double DefaultNplc(Pss_Type type) => type == Pss_Type.Keithley_2600 ? 1 : 0.01;

        internal static void ValidateNplc(Pss_Type type, double nplc)
        {
            if (type is not (Pss_Type.Keithley_2400 or Pss_Type.Keithley_2600)) return;
            double minimum = type == Pss_Type.Keithley_2600 ? 0.001 : 0.01;
            if (!double.IsFinite(nplc) || nplc < minimum || nplc > 10)
                throw new ArgumentException(EngineLocalization.Format($"本地源表积分时间（NPLC）必须是 {minimum:G}～10 之间的有限值。"));
        }
    }
}
