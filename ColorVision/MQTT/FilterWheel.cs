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

namespace ColorVision.MQTT
{
    /// <summary>
    /// 滤色轮
    /// </summary>
    public class FilterWheel:BaseService
    {

        public FilterWheel()
        {
            MQTTControl = MQTTControl.GetInstance();
            SendTopic = "FilterWheel";
            SubscribeTopic = "FilterWheelService";
            MQTTControl.SubscribeCache(SubscribeTopic);
        }
    }
}
