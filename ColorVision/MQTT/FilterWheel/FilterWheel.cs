using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;

namespace ColorVision.MQTT.FilterWheel
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
