using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class ShutterConfig:ViewModelBase
    {
        [LocalizedDisplayName(nameof(Resources.Serial)), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr = "COM1";

        [LocalizedDisplayName(nameof(Resources.BaudRate)), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        public string OpenCmd { get => _OpenCmd; set { _OpenCmd = value; OnPropertyChanged(); } }
        private string _OpenCmd = "a";
        public string CloseCmd { get => _CloseCmd; set { _CloseCmd = value; OnPropertyChanged(); } }
        private string _CloseCmd = "b";

        [LocalizedDisplayName(nameof(Resources.DelayMs))]
        public int DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private int _DelayTime = 1000;
    }
}
