using ColorVision.MQTT.Config;
using ColorVision.MQTT.Control;
using Google.Protobuf.WellKnownTypes;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.OpenXmlFormats.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static ColorVision.MQTT.MQTTSpectrum;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT
{
    public delegate void MQTTSpectrumDataHandler(SpectumData? colorPara);
    public delegate void MQTTSpectrumHeartbeatHandler(HeartbeatParam heartbeat);

    public class MQTTSpectrum: BaseService
    {
        public event MQTTSpectrumDataHandler DataHandlerEvent;
        public event MQTTSpectrumHeartbeatHandler HeartbeatHandlerEvent;

        public MQTTSpectrum(string SendTopic = "Spectum/CMD/chen_sp1", string SubscribeTopic = "Spectum/STATUS/chen_sp1") : base()
        {
            SpectrumConfig = new SpectrumConfig();
            SpectrumConfig.SendTopic = SendTopic;
            SpectrumConfig.SubscribeTopic = SubscribeTopic;

            this.SendTopic = SendTopic;
            this.SubscribeTopic = SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }




        public SpectrumConfig SpectrumConfig { get; set; }


        public MQTTSpectrum(SpectrumConfig spectrumConfig)
        {
            this.SpectrumConfig = spectrumConfig;

            this.SendTopic = spectrumConfig.SendTopic;
            this.SubscribeTopic = spectrumConfig.SubscribeTopic;

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
                    //if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                            ServiceID = json.ServiceID;
                        }
                        else if (json.EventName == "SetParam")
                        {
                            //MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            //MessageBox.Show("Open");
                        }
                        else if (json.EventName == "GetData")
                        {
                            JObject data = json.Data;
                            SpectumData? colorParam = JsonConvert.DeserializeObject<SpectumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "GetDataAuto")
                        {
                            JObject data = json.Data;
                            SpectumData? colorParam = JsonConvert.DeserializeObject<SpectumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                //cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "Heartbeat")
                        {
                            HeartbeatParam heartbeat = JsonConvert.DeserializeObject<HeartbeatParam>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => HeartbeatHandlerEvent?.Invoke(heartbeat));
                        }
                        else if (json.EventName == "Close")
                        {
                            //MessageBox.Show("Close");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            //MessageBox.Show("Uninit");
                        }
                        else if (json.EventName == "GetParam")
                        {
                            AutoIntTimeParam param = JsonConvert.DeserializeObject<AutoIntTimeParam>(JsonConvert.SerializeObject(json.Data));
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

        public bool Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool UnInit()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public void GetParam()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetParam"
            };
            PublishAsyncClient(msg);
        }

        public bool SetParam(int iLimitTime, float fTimeB)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new AutoIntTimeParam()
                {
                    iLimitTime = iLimitTime,
                    fTimeB = fTimeB
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Open()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData(float IntTime, int AveNum, bool bUseAutoIntTime =false, bool bUseAutoDark =false)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new GetDataParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark
                }
            };
            PublishAsyncClient(msg);
            cmdMap.Add(msg.MsgID.ToString(), msg);
            return true;
        }

        public bool Close()
        {
            //if (ServiceID == 0)
            //{
            //    MessageBox.Show("请先初始化");
            //    return false;
            //}
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
        }

        internal bool InitDark(float IntTime, int AveNum)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "InitDark",
                Params = new InitDarkParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        internal void GetDataAuto(float IntTime, int AveNum, bool bUseAutoIntTime, bool bUseAutoDark)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetDataAuto",
                Params = new GetDataParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark
                }
            };
            PublishAsyncClient(msg);
            cmdMap.Add(msg.MsgID.ToString(), msg);
        }

        internal void GetDataAutoStop()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetDataAutoStop",
            };
            PublishAsyncClient(msg);
            cmdMap.Clear();
        }

        public class SpectumData
        {
            public int ID { get; set; }
            public ColorParam Data { get; set; }

            public SpectumData(int id, ColorParam data)
            {
                ID = id;
                Data = data;
            }
        }

        public class HeartbeatParam
        {
            [JsonProperty("isOpen")]
            public bool IsOpen { get; set; }
            [JsonProperty("isAutoGetData")]
            public bool IsAutoGetData { get; set; }
            [JsonProperty("time")]
            public string Time { get; set; }
        }
        public class AutoIntTimeParam
        {
            public int iLimitTime { get; set; }
            public float fTimeB { get; set; }
        }

        public class InitDarkParamMQTT
        {
            [JsonProperty("fIntTime")]
            public float IntTime { get; set; }
            [JsonProperty("iAveNum")]
            public int AveNum { get; set; }
        }

        public class GetDataParamMQTT
        {
            [JsonProperty("fIntTime")]
            public float IntTime { get; set; }
            [JsonProperty("iAveNum")]
            public int AveNum { get; set; }
            [JsonProperty("bUseAutoIntTime")]
            public bool BUseAutoIntTime { get; set; }
            [JsonProperty("bUseAutoDark")]
            public bool BUseAutoDark { get; set; }
        }
    }
}
