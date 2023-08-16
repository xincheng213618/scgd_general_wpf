using ColorVision.MQTT.Camera;
using ColorVision.MQTT.PG;
using ColorVision.MQTT.SMU;
using ColorVision.MQTT.Spectrum;
using HslCommunication.MQTT;
using HslCommunication.Profinet.Panasonic.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ColorVision.MQTT
{
    public class MQTTManager
    {
        private static MQTTManager _instance;
        private static readonly object _locker = new();
        public static MQTTManager GetInstance() { lock (_locker) { return _instance ??= new MQTTManager(); } }
        public MQTTControl MQTTControl { get; set; }

        public StackPanel MQTTStackPanel { get; set; }

        public ObservableCollection<BaseService> Services { get; set; }

        public MQTTManager()
        {
            MQTTControl = MQTTControl.GetInstance();
            Services = new ObservableCollection<BaseService>();
            MQTTStackPanel = new StackPanel();
        }
    }
}
