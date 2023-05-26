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
            MQTTControl.Connected += (s, e) => MQTTControlInit();
            Task.Run(() => MQTTControl.Connect());
        }


        private void MQTTControlInit()
        {
            SendTopic = "PG";
            SubscribeTopic = "PGService";
            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            //如果之前绑定了，先移除在添加
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.Connected -= (s, e) => MQTTControlInit();
        }


        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTMsgReturn json = JsonConvert.DeserializeObject<MQTTMsgReturn>(Msg);
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
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Init",
                Params = new InitParamMQTT
                {
                    PGType = (int)pGType,
                    CommunicateType = (int)communicateType,
                }
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        private class InitParamMQTT
        {
            [JsonProperty("ePg_Type")]
            public int PGType { get; set; }
            [JsonProperty("eCOM_Type")]
            public int CommunicateType { get; set; }

        }

        public bool UnInit()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public void PGStartPG() => SetParam(new SetParamFunctionMQTT() { FunctionName = "CM_StartPG" });
        public void PGStopPG() => SetParam(new SetParamFunctionMQTT() { FunctionName = "CM_StopPG" });
        public void PGReSetPG() => SetParam(new SetParamFunctionMQTT() { FunctionName = "CM_ReSetPG" });
        public void PGSwitchUpPG() => SetParam(new SetParamFunctionMQTT() { FunctionName = "CM_SwitchUpPG" });
        public void PGSwitchDownPG() => SetParam(new SetParamFunctionMQTT() { FunctionName = "CM_SwitchDownPG" });
        public void PGSwitchFramePG(int index) => SetParam(new SetParamFunctionSwitchMQTT() { FunctionName = "CM_SwitchFramePG",Index = index });


        public bool SetParam(SetParamFunctionMQTT setParamMQTT)
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam",
                Params = setParamMQTT
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }



        public bool Open(CommunicateType communicateType,string value1,int value2)
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg;
            if (communicateType == CommunicateType.Serial)
            {
                 mQTTMsg = new MQTTMsg
                {
                    EventName = "Open",
                    Params = new OpenParam1MQQT
                    {
                        CommunicateType = (int)communicateType,
                        ComName = value1,
                        BaudRate = value2,
                    }
                };
            }
            else
            {
                 mQTTMsg = new MQTTMsg
                {
                    EventName = "Open",
                    Params = new OpenParamMQQT
                    {
                        CommunicateType = (int)communicateType,
                        IP = value1,
                        Port = value2,
                    }
                };
            }

            PublishAsyncClient(mQTTMsg);
            return true;
        }


        private class OpenParamMQQT
        {
            [JsonProperty("eCOM_Type")]
            public int CommunicateType { get; set; }
            [JsonProperty("szIPAddress")]
            public string IP { get; set; }
            [JsonProperty("nPort")]
            public int Port { get; set; }
        }
        private class OpenParam1MQQT
        {
            [JsonProperty("eCOM_Type")]
            public int CommunicateType { get; set; }
            [JsonProperty("szComName")]
            public string ComName { get; set; }
            public int BaudRate { get; set; }
        }

        public bool GetData()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "GetData",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool Close()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Close"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }
        
        public class SetParamFunctionSwitchMQTT: SetParamFunctionMQTT
        {
            [JsonProperty("nIndex")]
            public int Index { get; set; }
        }





    }
}
