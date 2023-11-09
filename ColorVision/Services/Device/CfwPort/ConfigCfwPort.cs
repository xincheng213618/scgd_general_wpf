using ColorVision.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Services.Device.CfwPort
{
    public class ConfigCfwPort: BaseDeviceConfig
    {
        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 9600;

    }
}
