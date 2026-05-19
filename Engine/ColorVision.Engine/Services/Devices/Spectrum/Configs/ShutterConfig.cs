using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class ShutterConfig:ViewModelBase
    {
        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "Serial"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr = "COM1";

        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        public string OpenCmd { get => _OpenCmd; set { _OpenCmd = value; OnPropertyChanged(); } }
        private string _OpenCmd = "a";
        public string CloseCmd { get => _CloseCmd; set { _CloseCmd = value; OnPropertyChanged(); } }
        private string _CloseCmd = "b";

        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "DelayMs")]
        public int DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private int _DelayTime = 1000;
    }
}
