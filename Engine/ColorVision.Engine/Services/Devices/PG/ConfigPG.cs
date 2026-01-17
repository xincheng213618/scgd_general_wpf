using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.PG
{
    public class ConfigPG : DeviceServiceConfig
    {
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;

        public string Category { get => _Category; set { _Category = value; OnPropertyChanged(); } }
        private string _Category;

        [DisplayName("寄存器地址")]
        public int RegisterAddress { get => _RegisterAddress; set { _RegisterAddress = value; OnPropertyChanged(); } }
        private int _RegisterAddress = 0x1b;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr;

        public int Port { get => _Port; set { _Port = value; OnPropertyChanged(); } }
        private int _Port;




    }
}
