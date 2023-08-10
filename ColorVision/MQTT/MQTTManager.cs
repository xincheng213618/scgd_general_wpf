using HslCommunication.MQTT;
using HslCommunication.Profinet.Panasonic.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
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

        public event EventHandler DeviceSettingChanged;

        public ObservableCollection<MQTTCamera> MQTTCameras { get; set; }

        public ObservableCollection<MQTTPG> MQTTPGs { get; set; }

        public ObservableCollection<MQTTSpectrum> MQTTSpectrums { get; set; }

        public ObservableCollection<MQTTVISource> MQTTVISources { get; set; }

        public ObservableCollection<IHeartbeat> ServiceHeartbeats { get; set; }
         
        public MQTTManager()
        {
            MQTTControl = MQTTControl.GetInstance();
            ServiceHeartbeats = new ObservableCollection<IHeartbeat>();

            MQTTCameras = new ObservableCollection<MQTTCamera>();
            MQTTPGs = new ObservableCollection< MQTTPG>();
            MQTTSpectrums = new ObservableCollection<MQTTSpectrum>();
            MQTTVISources = new ObservableCollection<MQTTVISource>();
        }

        public void Reload()
        {
            DeviceSettingChanged?.Invoke(this, new EventArgs());
        }
        

    }
}
