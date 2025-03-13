using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.PG.Templates;
using ColorVision.Engine.Messages;
using MQTTnet.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.PG
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


    public class MQTTPG : MQTTDeviceService<ConfigPG>
    {
        public Dictionary<string, Dictionary<string, string>> PGCategoryLib { get; }

        public MQTTPG(ConfigPG pGConfig) : base(pGConfig)
        {
            Config = pGConfig;

            SendTopic = Config.SendTopic;
            SubscribeTopic = Config.SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

            PGCategoryLib = new Dictionary<string, Dictionary<string, string>>();
            ReLoadCategoryLib();


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
                        }
                        else if (json.EventName == "SetParam")
                        {
                        }
                        else if (json.EventName == "Open")
                        {
                        }
                        else if (json.EventName == "GetData")
                        {
                        }
                        else if (json.EventName == "Close")
                        {
                        }
                        else if (json.EventName == "UnInit")
                        {
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


        public void PGStartPG() => SetParam(new List<ParamFunction>() { new() { Name = PGParam.StartKey } });
        public void PGStopPG() => SetParam(new List<ParamFunction>() { new() { Name = PGParam.StopKey } });
        public void PGReSetPG() => SetParam(new List<ParamFunction>() { new() { Name = PGParam.ReSetKey } });
        public void PGSwitchUpPG() => SetParam(new List<ParamFunction>() { new() { Name = PGParam.SwitchUpKey } });
        public void PGSwitchDownPG() => SetParam(new List<ParamFunction>() { new() { Name = PGParam.SwitchDownKey } });
        public void PGSwitchFramePG(int index) => SetParam(new List<ParamFunction>() { new() { Name = PGParam.SwitchFrameKey, Params = new Dictionary<string, object>() { { "index", index } } } });


        public bool SetParam(List<ParamFunction> Functions)
        {
            MsgSend msg = new()
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
            //Dictionary<string, string> cmd = new Dictionary<string, string>() { { "CM_StartPG", "open\r" }, { "CM_StopPG", "close\r" }, { "CM_ReSetPG", "reset\r" } , { "CM_SwitchUpPG", "Key UP\r" }, { "CM_SwitchDownPG", "Key DN\r" }, { "CM_SwitchFramePG", "pat {0}\r" } };
            //Dictionary<string, string> cmd;
            //if (!PGCategoryLib.TryGetValue(Config.Category,out cmd))
            //    cmd = new Dictionary<string, string>();

            MsgSend msg = new()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                //CIEParams = communicateType == CommunicateType.Serial ?
                //new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szComName", value1 }, { "BaudRate", value2 }, { "PGCustomCmd", cmd } } :
                //new Dictionary<string, object>() { { "eCOM_Type", (int)communicateType }, { "szIPAddress", value1 }, { "nPort", value2 },{ "PGCustomCmd", cmd } }
            };

            PublishAsyncClient(msg);
            return true;
        }


        public bool GetData()
        {
            MsgSend msg = new()
            {
                EventName = "GetData",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }

        public void ReLoadCategoryLib()
        {
            PGCategoryLib.Clear();
            foreach (var item in TemplatePGParam.Params)
            {
                PGCategoryLib.Add(item.Key, item.Value.ConvertToMap());
            }
            PGCategoryLib.Add("GXCLCM", new Dictionary<string, string>() { { "CM_StartPG", "open\r" }, { "CM_StopPG", "close\r" }, { "CM_ReSetPG", "reset\r" }, { "CM_SwitchUpPG", "Key UP\r" }, { "CM_SwitchDownPG", "Key DN\r" }, { "CM_SwitchFramePG", "pat {0}\r" } });
            PGCategoryLib.Add("SkyCode", new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } });

        }

        public void CustomPG(string text)
        {
            text = text.Replace("\\r","\r");
            text = text.Replace("\\n","\n");
            SetParam(new List<ParamFunction>() { new() { Name = PGParam.CustomKey, Params = new Dictionary<string, object>() { { "cmdTxt", text } } } });
        }

    }
}
