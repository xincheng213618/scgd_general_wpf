using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.PG.Templates;
using ColorVision.Engine.Messages;
using MQTTnet;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ColorVision.Engine.Services.Devices.Camera;

namespace ColorVision.Engine.Services.Devices.PG
{

    public enum PGType
    {
        [Description("GX09C_LCM")]
        GX09CLCM,
        [Description("SKYCODE")]
        SKYCODE,
        [Description("COMM.GX09C_LCM")]
        COMGX09CLCM,
        [Description("COMM.SKYCODE")]
        COMSKYCODE,
        [Description("CH431.I2C")]
        CH431I2C,
        [Description("GECS.V2.4")]
        GECSV24
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
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
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



        public MsgRecord Open(CommunicateType communicateType, string value1, int value2)
        {
            if (!PGCategoryLib.TryGetValue(Config.Category, out Dictionary<string, string> cmd))
                cmd = new Dictionary<string, string>();

            var parameters = new Dictionary<string, object>
            {
                { "eCOM_Type", (int)communicateType },
                { "PGCustomCmd", cmd }
            };

            if (communicateType == CommunicateType.Serial)
            {
                parameters.Add("szComName", value1);
                parameters.Add("BaudRate", value2);
            }
            else
            {
                parameters.Add("szIPAddress", value1);
                parameters.Add("nPort", value2);
            }

            if (Config.Category == "CH431.I2C")
            {
                parameters.Add("RegisterAddress", Config.RegisterAddress);
                parameters.Add("CH341_Stream_Speed", Config.BaudRate);
            }

            MsgSend msg = new()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = parameters
            };

            return PublishAsyncClient(msg);
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

        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
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
            PGCategoryLib.Add("COMM.GXCLCM", new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } });
            PGCategoryLib.Add("COMM.SkyCode", new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } });
            PGCategoryLib.Add("CH431.I2C", new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } });
            PGCategoryLib.Add("GECS.V2.4", new Dictionary<string, string>() { { "CM_StartPG", "start\r" }, { "CM_StopPG", "stop\r" }, { "CM_SwitchUpPG", "Switch_UP\r" }, { "CM_SwitchDownPG", "Switch_DOWN\r" }, { "CM_SwitchFramePG", "frame {0}" } });

        }

        public void CustomPG(string text)
        {
            text = text.Replace("\\r","\r");
            text = text.Replace("\\n","\n");
            SetParam(new List<ParamFunction>() { new() { Name = PGParam.CustomKey, Params = new Dictionary<string, object>() { { "cmdTxt", text } } } });
        }

    }
}
