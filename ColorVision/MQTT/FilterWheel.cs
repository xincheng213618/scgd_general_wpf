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
            MQTTControl.ConnectEx(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                            string CameraId = json.Data.CameraId;
                            ServiceID = json.ServiceID;
                        }
                        else if (json.EventName == "SetParam")
                        {
                            MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            MessageBox.Show("OpenCamera");
                        }
                        else if (json.EventName == "GatData")
                        {
                            string Filepath = json.Data.FilePath;
                        }
                        else if (json.EventName == "Close")
                        {
                            MessageBox.Show("CloseCamera");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            MessageBox.Show("Uninit");
                        }
                    }
                }
                catch
                {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        public bool Init(CameraType CameraType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool UnInit()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }

            MsgSend msg = new MsgSend
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
