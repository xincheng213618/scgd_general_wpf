using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT
{

    public class MQTTPG: BaseService
    {
        public enum PGType
        {
            [Description("GX09C_LCM")]
            GX09CLCM,
            [Description("SKYCODE")]
            SKYCODE,
        };

        public enum CommunicateType
        {
            [Description("TCP")]
            Tcp,
            [Description("串口")]
            Serial,
        };


        public MQTTPG()
        {
            MQTTControl = MQTTControl.GetInstance();
            SendTopic = "PG";
            SubscribeTopic = "PGService";
            MQTTControl.SubscribeCache(SubscribeTopic);
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
                    if (json.EventName == "Heartbeat")
                    {
                        LastAliveTime = DateTime.Now;
                        IsAlive = true;
                    }
                    if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                            ServiceID = json.ServiceID;
                            MessageBox.Show("Init");
                        }
                        else if (json.EventName == "SetParam")
                        {
                            MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            MessageBox.Show("Open");
                        }
                        else if (json.EventName == "GetData")
                        {
                            MessageBox.Show("GetData");
                        }
                        else if (json.EventName == "Close")
                        {
                            MessageBox.Show("Close");
                        }
                        else if (json.EventName == "UnInit")
                        {
                            MessageBox.Show("UnInit");
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


        public bool Init(PGType pGType,CommunicateType communicateType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() { { "ePg_Type", (int)pGType },{ "eCOM_Type",(int)communicateType } }
                
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

        public void PGStartPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_StartPG" }});
        public void PGStopPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_StopPG" } });
        public void PGReSetPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_ReSetPG" } });
        public void PGSwitchUpPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_SwitchUpPG" } });
        public void PGSwitchDownPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_SwitchDownPG" } });
        public void PGSwitchFramePG(int index) => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_SwitchFramePG", Params = new Dictionary<string, object>() { { "index", index } } } });


        public bool SetParam(List<ParamFunction> Functions)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = Functions
            };
            PublishAsyncClient(msg);
            return true;
        }



        public bool Open(CommunicateType communicateType,string value1,int value2)
        {
            MsgSend msg = new MsgSend()
            {
                EventName = "Open",
                Params = communicateType == CommunicateType.Serial ?
                new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szComName", value1 }, { "BaudRate", value2 } } :
                new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szIPAddress", value1 }, { "nPort", value2 } }
            };

            PublishAsyncClient(msg);
            return true;
        }


        public bool GetData()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
        }    
    }
}
