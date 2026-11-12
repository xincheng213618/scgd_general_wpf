using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services.Devices.CfwPort;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class ShutterConfig:ViewModelBase
    {
        [DisplayName("SpectrumUseFilterWheelShutter")]
        public bool UseFilterWheel { get => useFilterWheel; set { useFilterWheel = value; OnPropertyChanged(); } }
        private bool useFilterWheel;

        [DisplayName("SpectrumNdDevice"), PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCfwPort)), PropertyVisibility(nameof(UseFilterWheel))]
        public string FilterWheelDeviceCode { get => filterWheelDeviceCode; set { filterWheelDeviceCode = value; OnPropertyChanged(); } }
        private string filterWheelDeviceCode = string.Empty;

        [DisplayName("SpectrumFilterWheelMeasurementPort"), PropertyEditorType(typeof(SpectrumFilterWheelPositionEditor)), PropertyVisibility(nameof(UseFilterWheel))]
        public int MeasurementPort { get => measurementPort; set { measurementPort = value; OnPropertyChanged(); } }
        private int measurementPort = 5;

        [DisplayName("SpectrumFilterWheelDarkPort"), PropertyEditorType(typeof(SpectrumFilterWheelPositionEditor)), PropertyVisibility(nameof(UseFilterWheel))]
        public int DarkPort { get => darkPort; set { darkPort = value; OnPropertyChanged(); } }
        private int darkPort;

        [LocalizedDisplayName(nameof(Resources.Serial)), PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr = "COM1";

        [LocalizedDisplayName(nameof(Resources.BaudRate)), PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [DisplayName("打开指令"), Description("可填写 a 或 b；回执随实际指令判断：a 为 turn on，b 为 turn off。"), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string OpenCmd { get => _OpenCmd; set { _OpenCmd = value; OnPropertyChanged(); } }
        private string _OpenCmd = "a";
        [DisplayName("关闭指令"), Description("可填写 a 或 b；回执随实际指令判断：a 为 turn on，b 为 turn off。"), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string CloseCmd { get => _CloseCmd; set { _CloseCmd = value; OnPropertyChanged(); } }
        private string _CloseCmd = "b";

        [DisplayName("遮光使用打开指令（本地）"), Description("选中：遮光使用打开指令，通光使用关闭指令；取消：两者互换。只影响本地光闸，服务端继续使用既有配置和流程。"), PropertyVisibility(nameof(UseFilterWheel), true)]
        public bool OpenForDark { get => openForDark; set { openForDark = value; OnPropertyChanged(); } }
        private bool openForDark = true;

        internal string GetExpectedResponse(bool open)
        {
            string command = open ? OpenCmd : CloseCmd;
            return command?.Trim().ToLowerInvariant() switch { "a" => "turn on", "b" => "turn off", _ => open ? "turn on" : "turn off" };
        }

        [LocalizedDisplayName(nameof(Resources.DelayMs)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public int DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private int _DelayTime = 1000;
    }
}
