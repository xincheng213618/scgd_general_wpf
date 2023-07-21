using HslCommunication.Profinet.Panasonic.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public class MQTTManager
    {
        private static MQTTManager _instance;
        private static readonly object _locker = new();
        public static MQTTManager GetInstance() { lock (_locker) { return _instance ??= new MQTTManager(); } }

        public MQTTControl MQTTControl { get; set; }
        public MQTTManager()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTCameras = new ObservableCollection<KeyValuePair<string, MQTTCamera>>();
            MQTTCameras.Add(new KeyValuePair<string, MQTTCamera>("camera", new MQTTCamera("camera")));
        }

        public ObservableCollection<KeyValuePair<string, MQTTCamera>> MQTTCameras { get; set; }


    }
}
