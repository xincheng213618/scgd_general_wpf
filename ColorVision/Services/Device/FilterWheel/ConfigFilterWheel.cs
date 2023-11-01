using ColorVision.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Services.Device.FilterWheel
{
    public class ConfigFilterWheel: BaseDeviceConfig
    {
        public string szComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName;

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 9600;

    }
}
