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
            MQTTSpectrums = new ObservableCollection<KeyValuePair<string, MQTTSpectrum>>();
            MQTTVISources = new ObservableCollection<KeyValuePair<string, MQTTVISource>>();
            Algorithms = new ObservableCollection<KeyValuePair<string, Algorithm>>();
            MQTTCamera Camera = new MQTTCamera("相机1", "Camera", "CameraService");
            MQTTCameras.Add(new KeyValuePair<string, MQTTCamera>("camera", Camera));
            ServiceHeartbeats.Add(Camera);

            MQTTCamera Camera1 = new MQTTCamera("相机2", "Camera1", "Camera1Service");
            MQTTCameras.Add(new KeyValuePair<string, MQTTCamera>("camera", Camera1));
            ServiceHeartbeats.Add(Camera1);


            MQTTPG PG =  new MQTTPG("PG1");
            MQTTPGs.Add(new KeyValuePair<string, MQTTPG>("PG", PG));
            ServiceHeartbeats.Add(PG);

            MQTTSpectrum Spectrum = new MQTTSpectrum("MQTTSpectrum");
            MQTTSpectrums.Add(new KeyValuePair<string, MQTTSpectrum>("Spectrum", Spectrum));
            ServiceHeartbeats.Add(Spectrum);

            MQTTVISource VISource = new MQTTVISource("源表", "Pss_Sx", "Pss_SxService");
            MQTTVISources.Add(new KeyValuePair<string, MQTTVISource>("源表", VISource));
            ServiceHeartbeats.Add(VISource);

            Algorithm Algorithm = new Algorithm("Algorithm");
            Algorithms.Add(new KeyValuePair<string, Algorithm>("Algorithm", Algorithm));
            ServiceHeartbeats.Add(Algorithm);

            
        }

        public ObservableCollection<KeyValuePair<string, MQTTCamera>> MQTTCameras { get; set; }
        public ObservableCollection<KeyValuePair<string, MQTTPG>> MQTTPGs { get; set; }
        public ObservableCollection<KeyValuePair<string, MQTTSpectrum>> MQTTSpectrums { get; set; }

        public ObservableCollection<KeyValuePair<string, MQTTVISource>> MQTTVISources { get; set; }

        public ObservableCollection<KeyValuePair<string, Algorithm>> Algorithms { get; set; }


        public ObservableCollection<IServiceHeartbeat> ServiceHeartbeats { get; set; }


    }
}
