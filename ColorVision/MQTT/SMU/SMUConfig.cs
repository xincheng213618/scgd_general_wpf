namespace ColorVision.MQTT.SMU
{
    public class SMUConfig : BaseDeviceConfig, IMQTTServiceConfig
    {
        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
    }
}
