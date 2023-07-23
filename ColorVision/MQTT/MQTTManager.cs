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
            ServiceHeartbeats = new ObservableCollection<IServiceHeartbeat>();

            MQTTCameras = new ObservableCollection<KeyValuePair<string, MQTTCamera>>();
            MQTTPGs = new ObservableCollection<KeyValuePair<string, MQTTPG>>();

            MQTTCamera Camera = new MQTTCamera("相机1");
            MQTTCameras.Add(new KeyValuePair<string, MQTTCamera>("camera", Camera));
            ServiceHeartbeats.Add(Camera);


            MQTTPG PG =  new MQTTPG("PG1");
            MQTTPGs.Add(new KeyValuePair<string, MQTTPG>("PG", PG));
            ServiceHeartbeats.Add(PG);


        }

        public ObservableCollection<KeyValuePair<string, MQTTCamera>> MQTTCameras { get; set; }
        public ObservableCollection<KeyValuePair<string, MQTTPG>> MQTTPGs { get; set; }

        public ObservableCollection<IServiceHeartbeat> ServiceHeartbeats { get; set; }


    }
}
