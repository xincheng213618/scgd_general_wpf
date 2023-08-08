namespace ColorVision.MQTT.Config
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig : BaseDeviceConfig, IMQTTServiceConfig
    {

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); } }
        private CameraType _CameraType;


        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public int ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private int _ImageBpp;
        public int Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); } }
        private int _Channel;


    }
}
