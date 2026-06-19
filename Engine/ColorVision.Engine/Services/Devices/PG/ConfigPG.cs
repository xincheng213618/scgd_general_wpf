#pragma warning disable CA1707
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Services.Devices.PG
{
    public enum CH341_Stream_Speed
    {
        CH341_20KHZ,
        CH341_100KHZ,
        CH341_400KHZ,
        CH341_750KHZ
    };


    public class ConfigPG : DeviceServiceConfig
    {
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;

        public string Category { get => _Category; set { _Category = value; OnPropertyChanged(); } }
        private string _Category = "SkyCode";

        [Display(Name = "Engine_PG_RegisterAddress", ResourceType = typeof(Properties.Resources))]
        public int RegisterAddress { get => _RegisterAddress; set { _RegisterAddress = value; OnPropertyChanged(); } }
        private int _RegisterAddress = 0x1b;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr;

        public int Port { get => _Port; set { _Port = value; OnPropertyChanged(); } }
        private int _Port;

        public CH341_Stream_Speed BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private CH341_Stream_Speed _BaudRate;
    }
}
