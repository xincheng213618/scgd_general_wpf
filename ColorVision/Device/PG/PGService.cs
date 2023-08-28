using ColorVision.MQTT;
using MQTTnet.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Device.PG
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


    public class PGService : BaseService<PGConfig>
    {
        public event HeartbeatEventHandler HeartbeatEvent;

        public PGService(PGConfig pGConfig) : base(pGConfig)
        {
            Config = pGConfig;

            SendTopic = Config.SendTopic;
            SubscribeTopic = Config.SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
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
                        else if (json.EventName == "Heartbeat")
                        {
                            HeartbeatParam heartbeat = JsonConvert.DeserializeObject<HeartbeatParam>(JsonConvert.SerializeObject(json.Data));
                            if (heartbeat != null && json.ServiceName.Equals(Config.Code, System.StringComparison.Ordinal))
                            {
                                Application.Current.Dispatcher.Invoke(() => HeartbeatEvent?.Invoke(heartbeat));
                            }
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


        public bool Init(PGType pGType, CommunicateType communicateType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() { { "ePg_Type", (int)pGType }, { "eCOM_Type", (int)communicateType } }

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

        public void PGStartPG() => SetParam(new List<ParamFunction>() { new ParamFunction() { Name = "CM_StartPG" } });
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
                ServiceName = Config.Code,
                Params = Functions
            };
            PublishAsyncClient(msg);
            return true;
        }



        public bool Open(CommunicateType communicateType, string value1, int value2)
        {
            //Dictionary<string, string> cmd = new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } };
            Dictionary<string, string> cmd = new Dictionary<string, string>() { { "CM_StartPG", "open\r" }, { "CM_StopPG", "close\r" }, { "CM_ReSetPG", "reset\r" } , { "CM_SwitchUpPG", "key UP\r" }, { "CM_SwitchDownPG", "key DN\r" }, { "CM_SwitchFramePG", "pat {0}\r" } };
            MsgSend msg = new MsgSend()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = communicateType == CommunicateType.Serial ?
                new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szComName", value1 }, { "BaudRate", value2 }, { "PGCustomCmd", cmd } } :
                new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szIPAddress", value1 }, { "nPort", value2 },{ "PGCustomCmd", cmd } }
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
                EventName = "Close",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
