namespace ColorVision.MQTT.SMU
{
    public class SMUOpenParam
    {
        public bool IsNet { set; get; }
        public string DevName { set; get; }
    }
    public class SMUConfig : BaseDeviceConfig
    {
        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
    }
}
