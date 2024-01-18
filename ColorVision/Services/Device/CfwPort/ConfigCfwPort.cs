namespace ColorVision.Services.Device.CfwPort
{
    public class ConfigCfwPort: DeviceServiceConfig
    {
        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

    }
}
