using HslCommunication.MQTT;
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

        public event EventHandler DeviceSettingChanged;

        public ObservableCollection<MQTTCamera> MQTTCameras { get; set; }
        public ObservableCollection<MQTTPG> MQTTPGs { get; set; }
        public ObservableCollection<MQTTSpectrum> MQTTSpectrums { get; set; }

        public ObservableCollection<MQTTVISource> MQTTVISources { get; set; }

        public ObservableCollection<Algorithm> Algorithms { get; set; }
        public ObservableCollection<IHeartbeat> ServiceHeartbeats { get; set; }

        public MQTTManager()
        {
            MQTTControl = MQTTControl.GetInstance();
            ServiceHeartbeats = new ObservableCollection<IHeartbeat>();

            MQTTCameras = new ObservableCollection<MQTTCamera>();
            MQTTPGs = new ObservableCollection< MQTTPG>();
            MQTTSpectrums = new ObservableCollection<MQTTSpectrum>();
            MQTTVISources = new ObservableCollection<MQTTVISource>();
            Algorithms = new ObservableCollection<Algorithm>();


            CameraConfig cameraConfig = new CameraConfig
            {
                SendTopic = "Camera",
                SubscribeTopic = "CameraService",
                Name = "相机0",
                ID = "58366c49967393afe",
                CameraType = CameraType.CVQ,
                TakeImageMode = TakeImageMode.Normal
            };
            cameraConfig.Name = "CV";
            cameraConfig.ImageBpp = 8;


            MQTTCamera Camera = new MQTTCamera(cameraConfig);
            MQTTCameras.Add(Camera);
            ServiceHeartbeats.Add(Camera);

            CameraConfig cameraConfig1 = new CameraConfig
            {
                SendTopic = "Camera",
                SubscribeTopic = "CameraService",

                Name = "相机1",
                ID = "e29b14429bc375b1",
                CameraType = CameraType.LVQ,
                TakeImageMode = TakeImageMode.Normal,
                ImageBpp = 8
            };
            cameraConfig1.Name = "BV";

            MQTTCamera Camera1 = new MQTTCamera(cameraConfig1);
            MQTTCameras.Add(Camera1);
            ServiceHeartbeats.Add(Camera1);
        }

        public void Reload()
        {
            DeviceSettingChanged?.Invoke(this, new EventArgs());
        }
        

    }
}
