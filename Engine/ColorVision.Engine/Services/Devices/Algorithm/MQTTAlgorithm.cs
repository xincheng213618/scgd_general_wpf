namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public MQTTAlgorithm(ConfigAlgorithm config) : base(config)
        {
            DeviceStatus = DeviceStatusType.Unknown;
        }
    }
}
