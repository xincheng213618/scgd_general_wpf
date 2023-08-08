namespace ColorVision.MQTT.Config
{
    public class SpectrumConfig : BaseDeviceConfig, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
    }
}
