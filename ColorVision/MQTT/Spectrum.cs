using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT
{
    public delegate void MQTTSpectrumDataHandler(ColorParam colorPara);

    public class MQTTSpectrum: BaseService
    {
        public event MQTTSpectrumDataHandler DataHandlerEvent;

        public MQTTSpectrum()
        {
            MQTTControl = MQTTControl.GetInstance();
            SendTopic = "Spectrum";
            SubscribeTopic = "SpectrumService";
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
                            JObject data = json.Data.COLOR_PARA;
                            ColorParam colorParam = JsonConvert.DeserializeObject<ColorParam>(JsonConvert.SerializeObject(data));
                            Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));

                        }
                        else if (json.EventName == "Close")
                        {
                            MessageBox.Show("Close");
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


        public bool SetParam()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Open()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "Open"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData(float IntTime, int AveNum, bool bUseAutoIntTime =false, bool bUseAutoDark =false)
        {
            //if (ServiceID == 0)
            //{
            //    MessageBox.Show("请先初始化");
            //    return false;
            //}
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
            return true;
        }

        public bool Close()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
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
