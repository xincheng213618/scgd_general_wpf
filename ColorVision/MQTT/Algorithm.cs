using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public class Algorithm: BaseService
    {
        public Algorithm(string NickName = "相机1", string SendTopic = "Algorithm", string SubscribeTopic = "AlgorithmService") : base()
        {
            this.NickName = NickName;
            this.SendTopic = SendTopic;
            this.SubscribeTopic = SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl = MQTTControl.GetInstance();
            MsgReturnReceived+= Algorithm_MsgReturnChanged;
        }

        private void Algorithm_MsgReturnChanged(MsgReturn msg)
        {
            if (msg.Code == 0)
            {
                if (msg.EventName == "Init")
                {
                    ServiceID = msg.ServiceID;
                }
                else if (msg.EventName == "SetParam")
                {
                    //MessageBox.Show("SetParam");
                }
                else if (msg.EventName == "Open")
                {
                    //MessageBox.Show("Open");
                }
                else if (msg.EventName == "GetData")
                {
                    //MessageBox.Show("GetData");
                }
                else if (msg.EventName == "Close")
                {
                    //MessageBox.Show("Close");
                }
            }
        }
    }
}
