using ColorVision.MQTT;

namespace ColorVision.Device.FilterWheel
{

    public class FilterWheel : BaseService<BaseDeviceConfig>
    {

        public FilterWheel(BaseDeviceConfig baseDeviceConfig) : base(baseDeviceConfig)
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
        }



    }
}
