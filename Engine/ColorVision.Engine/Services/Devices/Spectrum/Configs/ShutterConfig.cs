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

        [DisplayName("SpectrumFilterWheelMeasurementPort"), PropertyVisibility(nameof(UseFilterWheel))]
        public int MeasurementPort { get => measurementPort; set { measurementPort = value; OnPropertyChanged(); } }
        private int measurementPort = 5;

        [DisplayName("SpectrumFilterWheelDarkPort"), PropertyVisibility(nameof(UseFilterWheel))]
        public int DarkPort { get => darkPort; set { darkPort = value; OnPropertyChanged(); } }
        private int darkPort;

        [LocalizedDisplayName(nameof(Resources.Serial)), PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr = "COM1";

        [LocalizedDisplayName(nameof(Resources.BaudRate)), PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [DisplayName("SpectrumOpenShutter"), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string OpenCmd { get => _OpenCmd; set { _OpenCmd = value; OnPropertyChanged(); } }
        private string _OpenCmd = "a";
        [DisplayName("SpectrumCloseShutter"), PropertyVisibility(nameof(UseFilterWheel), true)]
        public string CloseCmd { get => _CloseCmd; set { _CloseCmd = value; OnPropertyChanged(); } }
        private string _CloseCmd = "b";

        [LocalizedDisplayName(nameof(Resources.DelayMs)), PropertyVisibility(nameof(UseFilterWheel), true)]
        public int DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private int _DelayTime = 1000;
    }
}
