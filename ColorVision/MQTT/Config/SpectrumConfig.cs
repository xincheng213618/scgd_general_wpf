namespace ColorVision.MQTT.Config
{
    public class SpectrumConfig : BaseDeviceConfig, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        private int _TimeLimit;
        public int TimeLimit  { get => _TimeLimit; set { _TimeLimit = value; NotifyPropertyChanged(); } }

        private float _TimeFrom;
        public float TimeFrom { get => _TimeFrom; set { _TimeFrom = value; NotifyPropertyChanged(); } }
    }

    public class PGConfig : BaseDeviceConfig, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
    }

    public class SMUConfig : BaseDeviceConfig, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
    }
}
