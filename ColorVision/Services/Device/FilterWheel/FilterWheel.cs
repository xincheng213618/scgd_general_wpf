using ColorVision.MQTT;

namespace ColorVision.Device.FilterWheel
{

    public class FilterWheel : BaseDevService<BaseDeviceConfig>
    {
        public FilterWheel(BaseDeviceConfig baseDeviceConfig) : base(baseDeviceConfig)
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
        }

    }
}
