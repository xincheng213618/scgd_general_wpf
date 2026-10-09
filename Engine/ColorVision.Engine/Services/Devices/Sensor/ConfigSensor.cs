namespace ColorVision.Engine.Services.Devices.Sensor
{
    public class ConfigSensor : DeviceServiceConfig
    {
        public string Category { get => _Category; set { _Category = value; OnPropertyChanged(); } }
        private string _Category = "Sensor.Default";

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;

        [System.ComponentModel.PropertyEditorType(typeof(System.ComponentModel.NetworkAddressPropertiesEditor))]
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr;

        public int Port { get => _Port; set { _Port = value; OnPropertyChanged(); } }
        private int _Port;

        [System.ComponentModel.DisplayName("自动连接")]
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;
    }
}
