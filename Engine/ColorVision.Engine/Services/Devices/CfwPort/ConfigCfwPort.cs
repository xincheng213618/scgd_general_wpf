using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class ConfigCfwPort: DeviceServiceConfig
    {
        [DisplayName("SzComName"), PropertyEditorType(PropertyEditorType.TextSerialPort)]
        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        [DisplayName("BaudRate"), PropertyEditorType(PropertyEditorType.TextBaudRate)]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

        [DisplayName("Timeout")]
        public int Timeout { get => _Timeout; set { _Timeout = value; NotifyPropertyChanged(); } }
        private int _Timeout = 5000;

        [DisplayName("RetryCount")]
        public int RetryCount { get => _RetryCount; set { _RetryCount = value; NotifyPropertyChanged(); } }
        private int _RetryCount =3;

        [DisplayName("滤色轮数量(6,12)")]
        public int Ports { get => _Ports; set { _Ports = value; NotifyPropertyChanged(); } } 
        private int _Ports = 6;


        [DisplayName("自动连接")]
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen = true;
 
    }
}
